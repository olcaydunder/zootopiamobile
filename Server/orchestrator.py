#!/usr/bin/env python3
"""
Zootopia Mobile match orchestrator (runs on the game server as the "zootopia" service).

- Keeps the newest dedicated-server build: polls the public GitHub release "son-server" and unpacks
  ZootopiaServer.tar.gz into builds/<asset id>/ (running matches keep using their own copy).
- Starts one Unity server process per match on its own UDP port and hands clients that port:
    POST /quick?mode=solo|duo|squad&version=V   -> join (or open) a public match
    POST /room/create?mode=...&version=V        -> open a private match, returns a 6-digit code
    GET  /room/<code>?version=V                 -> where that private match is
    POST /report   (from match processes, localhost only) {code, state, players}
    GET  /status                                -> health / what is running
- Updates itself: re-downloads orchestrator.py from the repo and restarts when it changed.

Only the Python standard library is used. Nothing secret is stored here.
"""
import hashlib
import json
import os
import random
import re
import shutil
import subprocess
import sys
import tarfile
import threading
import time
import urllib.request
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import parse_qs, urlparse

REPO = "olcaydunder/zootopiamobile"
RELEASE_TAG = "son-server"
ASSET_NAME = "ZootopiaServer.tar.gz"
RAW_SELF = "https://raw.githubusercontent.com/%s/main/Server/orchestrator.py" % REPO

BASE = os.path.dirname(os.path.abspath(__file__))
BUILDS = os.path.join(BASE, "builds")
LOGS = os.path.join(BASE, "logs")
API_PORT = 8080
FIRST_GAME_PORT = 7777
PORT_COUNT = 20
MAX_MATCHES = max(2, min(PORT_COUNT, (os.cpu_count() or 2)))   # ~1 core per match
MAX_HUMANS = {"solo": 16, "duo": 16, "squad": 16}
WAITING_TIMEOUT = 15 * 60       # an empty match that never started is closed after this
MATCH_MAX_AGE = 45 * 60         # hard limit for any match process
BUILD_POLL = 120
SELF_POLL = 600

lock = threading.RLock()
matches = {}                    # code -> dict
current_build = {"dir": None, "version": None, "asset": None}
create_times = {}               # client ip -> [timestamps] (simple rate limit)


def log(*args):
    line = time.strftime("%Y-%m-%d %H:%M:%S ") + " ".join(str(a) for a in args)
    print(line, flush=True)
    try:
        with open(os.path.join(LOGS, "orchestrator.log"), "a") as f:
            f.write(line + "\n")
    except OSError:
        pass


def http_get(url, timeout=30):
    req = urllib.request.Request(url, headers={"User-Agent": "zootopia-orchestrator", "Accept": "application/vnd.github+json"})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


# ----- Builds -----

def find_executable(folder):
    for root, _dirs, files in os.walk(folder):
        for name in files:
            if name.endswith(".x86_64"):
                return os.path.join(root, name)
    return None


def read_version(folder):
    for root, _dirs, files in os.walk(folder):
        if "VERSION" in files:
            with open(os.path.join(root, "VERSION")) as f:
                return f.read().strip()
    return "unknown"


def adopt_build(folder, asset_id):
    exe = find_executable(folder)
    if not exe:
        log("build has no .x86_64 executable:", folder)
        return False
    os.chmod(exe, 0o755)
    with lock:
        current_build.update({"dir": folder, "version": read_version(folder), "asset": asset_id, "exe": exe})
    log("using build", asset_id, "version", current_build["version"])
    return True


def update_build_once():
    try:
        rel = json.loads(http_get("https://api.github.com/repos/%s/releases/tags/%s" % (REPO, RELEASE_TAG)))
    except Exception as e:  # no release yet, offline, rate limited...
        log("release check failed:", e)
        return
    asset = next((a for a in rel.get("assets", []) if a.get("name") == ASSET_NAME), None)
    if not asset:
        log("release has no", ASSET_NAME, "yet")
        return
    asset_id = "%s-%s" % (asset["id"], re.sub(r"\D", "", asset.get("updated_at", "")))
    if current_build.get("asset") == asset_id:
        return
    folder = os.path.join(BUILDS, asset_id)
    if not os.path.isdir(folder):
        tmp = folder + ".tar.gz"
        log("downloading build", asset_id, asset.get("size"), "bytes")
        req = urllib.request.Request(asset["browser_download_url"], headers={"User-Agent": "zootopia-orchestrator"})
        with urllib.request.urlopen(req, timeout=600) as r, open(tmp, "wb") as f:
            shutil.copyfileobj(r, f)
        os.makedirs(folder, exist_ok=True)
        with tarfile.open(tmp) as t:
            for m in t.getmembers():   # refuse paths outside the folder
                if m.name.startswith("/") or ".." in m.name.split("/"):
                    raise ValueError("unsafe path in archive: " + m.name)
            t.extractall(folder)
        os.remove(tmp)
    if adopt_build(folder, asset_id):
        prune_builds()


def prune_builds():
    """Keeps the current build and any build a running match still uses."""
    with lock:
        keep = {current_build.get("dir")} | {m["build"] for m in matches.values()}
    for name in os.listdir(BUILDS):
        path = os.path.join(BUILDS, name)
        if os.path.isdir(path) and path not in keep:
            shutil.rmtree(path, ignore_errors=True)


def build_loop():
    # Re-adopt the newest local build after a restart.
    try:
        dirs = sorted((d for d in os.listdir(BUILDS) if os.path.isdir(os.path.join(BUILDS, d))),
                      key=lambda d: os.path.getmtime(os.path.join(BUILDS, d)))
        if dirs:
            adopt_build(os.path.join(BUILDS, dirs[-1]), dirs[-1])
    except OSError:
        pass
    while True:
        update_build_once()
        time.sleep(BUILD_POLL)


def self_update_loop():
    try:
        with open(__file__, "rb") as f:
            mine = hashlib.sha256(f.read()).hexdigest()
    except OSError:
        return
    while True:
        time.sleep(SELF_POLL)
        try:
            data = http_get(RAW_SELF)
            if hashlib.sha256(data).hexdigest() != mine and b"def main" in data:
                with lock:
                    busy = any(m["state"] != "ended" for m in matches.values())
                if busy:
                    continue   # never restart under running matches (they are children of this process)
                with open(__file__ + ".new", "wb") as f:
                    f.write(data)
                os.replace(__file__ + ".new", __file__)
                log("orchestrator updated, restarting")
                os.execv(sys.executable, [sys.executable, __file__])
        except Exception as e:
            log("self-update check failed:", e)


# ----- Matches -----

def free_port():
    used = {m["port"] for m in matches.values()}
    for p in range(FIRST_GAME_PORT, FIRST_GAME_PORT + PORT_COUNT):
        if p not in used:
            return p
    return None


def new_code():
    while True:
        code = "%06d" % random.randint(0, 999999)
        if code not in matches:
            return code


def start_match(mode, kind):
    """Starts a server process. Caller holds the lock. Returns the match or an error string."""
    if not current_build.get("exe"):
        return "Sunucu sürümü henüz hazır değil, birkaç dakika sonra tekrar dene"
    running = [m for m in matches.values() if m["state"] != "ended"]
    if len(running) >= MAX_MATCHES:
        return "Sunucu dolu, biraz sonra tekrar dene"
    port = free_port()
    if port is None:
        return "Sunucu dolu"
    code = new_code()
    logfile = os.path.join(LOGS, "match-%s.log" % code)
    args = [current_build["exe"], "-batchmode", "-nographics", "-server",
            "-port", str(port), "-code", code, "-mode", mode, "-matchType", kind,
            "-api", "http://127.0.0.1:%d" % API_PORT, "-logFile", logfile]
    proc = subprocess.Popen(args, cwd=os.path.dirname(current_build["exe"]),
                            stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    m = {"code": code, "port": port, "mode": mode, "kind": kind, "state": "waiting", "players": 0,
         "created": time.time(), "proc": proc, "build": current_build["dir"], "version": current_build["version"]}
    matches[code] = m
    log("started match", code, kind, mode, "port", port, "pid", proc.pid)
    return m


def reap_loop():
    while True:
        time.sleep(5)
        now = time.time()
        with lock:
            for code, m in list(matches.items()):
                proc = m["proc"]
                if proc.poll() is not None:
                    log("match ended", code, "exit", proc.returncode)
                    del matches[code]
                    continue
                too_old = now - m["created"] > MATCH_MAX_AGE
                idle = m["state"] == "waiting" and m["players"] == 0 and now - m["created"] > WAITING_TIMEOUT
                if too_old or idle:
                    log("stopping match", code, "too old" if too_old else "idle")
                    proc.terminate()
        # old logs
        try:
            for name in os.listdir(LOGS):
                path = os.path.join(LOGS, name)
                if name.startswith("match-") and now - os.path.getmtime(path) > 3 * 86400:
                    os.remove(path)
        except OSError:
            pass


def public_view(m):
    return {"ok": True, "port": m["port"], "code": m["code"], "mode": m["mode"], "kind": m["kind"],
            "state": m["state"], "players": m["players"], "version": m["version"]}


def rate_limited(ip):
    now = time.time()
    times = [t for t in create_times.get(ip, []) if now - t < 60]
    create_times[ip] = times
    if len(times) >= 6:
        return True
    times.append(now)
    return False


# ----- HTTP API -----

class Handler(BaseHTTPRequestHandler):
    server_version = "Zootopia/1"

    def log_message(self, fmt, *args):
        pass

    def reply(self, code, obj):
        body = json.dumps(obj, ensure_ascii=False).encode("utf-8")
        self.send_response(code)
        self.send_header("Content-Type", "application/json; charset=utf-8")
        self.send_header("Content-Length", str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def params(self):
        u = urlparse(self.path)
        q = {k: v[0] for k, v in parse_qs(u.query).items()}
        return u.path.rstrip("/") or "/", q

    def check_version(self, q):
        want = q.get("version", "")
        have = current_build.get("version")
        if have and want and want != have and "dev" not in (want, have):
            self.reply(409, {"ok": False, "error": "Sürüm uyuşmuyor: oyunu güncelle (yeni sürüm çıktıysa sunucu birkaç dakika içinde güncellenir)", "server": have})
            return False
        return True

    def do_GET(self):
        path, q = self.params()
        if path == "/status":
            with lock:
                self.reply(200, {"ok": True, "version": current_build.get("version"), "maxMatches": MAX_MATCHES,
                                 "matches": [public_view(m) for m in matches.values()]})
            return
        mm = re.fullmatch(r"/room/(\d{6})", path)
        if mm:
            if not self.check_version(q):
                return
            with lock:
                m = matches.get(mm.group(1))
                if not m or m["kind"] != "private" or m["state"] == "ended":
                    self.reply(404, {"ok": False, "error": "Oda bulunamadı"})
                elif m["state"] != "waiting":
                    self.reply(409, {"ok": False, "error": "Bu odada maç başladı"})
                else:
                    self.reply(200, public_view(m))
            return
        self.reply(404, {"ok": False, "error": "not found"})

    def do_POST(self):
        path, q = self.params()
        length = min(int(self.headers.get("Content-Length") or 0), 4096)
        body = self.rfile.read(length) if length else b""
        ip = self.client_address[0]

        if path == "/report":
            if ip not in ("127.0.0.1", "::1"):
                self.reply(403, {"ok": False})
                return
            try:
                data = json.loads(body or b"{}")
            except ValueError:
                self.reply(400, {"ok": False})
                return
            with lock:
                m = matches.get(str(data.get("code", "")))
                if m:
                    m["state"] = data.get("state", m["state"])
                    m["players"] = int(data.get("players", m["players"]))
            self.reply(200, {"ok": True})
            return

        mode = q.get("mode", "solo")
        if mode not in MAX_HUMANS:
            mode = "solo"
        if path == "/quick":
            if not self.check_version(q):
                return
            with lock:
                for m in sorted(matches.values(), key=lambda m: -m["players"]):
                    if m["kind"] == "quick" and m["mode"] == mode and m["state"] == "waiting" \
                            and m["players"] < MAX_HUMANS[mode] and m["version"] == current_build.get("version"):
                        self.reply(200, public_view(m))
                        return
                if rate_limited(ip):
                    self.reply(429, {"ok": False, "error": "Çok sık deneme, biraz bekle"})
                    return
                m = start_match(mode, "quick")
            self.reply(200, public_view(m)) if isinstance(m, dict) else self.reply(503, {"ok": False, "error": m})
            return
        if path == "/room/create":
            if not self.check_version(q):
                return
            with lock:
                if rate_limited(ip):
                    self.reply(429, {"ok": False, "error": "Çok sık deneme, biraz bekle"})
                    return
                m = start_match(mode, "private")
            self.reply(200, public_view(m)) if isinstance(m, dict) else self.reply(503, {"ok": False, "error": m})
            return
        self.reply(404, {"ok": False, "error": "not found"})


def main():
    os.makedirs(BUILDS, exist_ok=True)
    os.makedirs(LOGS, exist_ok=True)
    for target in (build_loop, reap_loop, self_update_loop):
        threading.Thread(target=target, daemon=True).start()
    log("orchestrator listening on", API_PORT, "max matches", MAX_MATCHES)
    ThreadingHTTPServer(("0.0.0.0", API_PORT), Handler).serve_forever()


if __name__ == "__main__":
    main()
