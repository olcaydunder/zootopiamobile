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
- Player accounts (SQLite, zootopia.db): friend codes, friends and invites, blocking, reports, bug reports, bans.
- Admin panel at /admin (password generated on the server into admin_password.txt; only the owner can read it).
- Updates itself: re-downloads orchestrator.py from the repo and restarts when it changed.

Only the Python standard library is used. Nothing secret is stored here.
"""
import collections
import hashlib
import hmac
import json
import secrets
import sqlite3
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
recent = collections.deque(maxlen=8)   # last finished matches, shown in /status (with errors, for remote checks)


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


def error_lines(path, limit=6):
    """A few error lines from the end of a match log (no player data: the game server does not log addresses)."""
    try:
        with open(path, "rb") as f:
            f.seek(0, os.SEEK_END)
            f.seek(max(0, f.tell() - 65536))
            text = f.read().decode("utf-8", "replace")
    except OSError:
        return ["log yok"]
    keys = ("Exception", "error while loading", "Error", "Segmentation", "Crash", "Sunucu]")
    lines = [l.strip()[:200] for l in text.splitlines() if any(k in l for k in keys)]
    return lines[-limit:]


def reap_loop():
    while True:
        time.sleep(5)
        now = time.time()
        with lock:
            for code, m in list(matches.items()):
                proc = m["proc"]
                if proc.poll() is not None:
                    log("match ended", code, "exit", proc.returncode)
                    errors = error_lines(os.path.join(LOGS, "match-%s.log" % code))
                    recent.append({"code": code, "exit": proc.returncode, "seconds": int(now - m["created"]),
                                   "state": m["state"], "players": m.get("max_players", m["players"]), "version": m["version"],
                                   "errors": errors})
                    record_match_end(m, now, errors)
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


# ----- Accounts (SQLite) -----

DB_PATH = os.path.join(BASE, "zootopia.db")
ADMIN_FILE = os.path.join(BASE, "admin.json")
ADMIN_FIRST_PASSWORD = os.path.join(BASE, "admin_password.txt")
CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"   # no 0/O, 1/I
ONLINE_SECONDS = 45
started_at = time.time()
db_lock = threading.RLock()
db = None
admin_sessions = {}      # token -> expiry
login_tries = {}         # ip -> [timestamps]
api_tries = {}           # (ip, kind) -> [timestamps]


def db_init():
    global db
    db = sqlite3.connect(DB_PATH, check_same_thread=False)
    db.row_factory = sqlite3.Row
    db.executescript("""
    PRAGMA journal_mode=WAL;
    CREATE TABLE IF NOT EXISTS accounts (
        id TEXT PRIMARY KEY, secret_hash TEXT NOT NULL, name TEXT NOT NULL,
        created REAL, last_seen REAL, last_ip TEXT, device TEXT, version TEXT,
        status TEXT DEFAULT '', room TEXT DEFAULT '', room_mode TEXT DEFAULT '', status_time REAL DEFAULT 0,
        matches INTEGER DEFAULT 0, banned INTEGER DEFAULT 0, ban_reason TEXT DEFAULT '', banned_at REAL DEFAULT 0);
    CREATE TABLE IF NOT EXISTS friends (a TEXT, b TEXT, state TEXT, created REAL, PRIMARY KEY (a, b));
    CREATE TABLE IF NOT EXISTS blocks (owner TEXT, target TEXT, created REAL, PRIMARY KEY (owner, target));
    CREATE TABLE IF NOT EXISTS invites (from_id TEXT, to_id TEXT, room TEXT, mode TEXT, created REAL, PRIMARY KEY (from_id, to_id));
    CREATE TABLE IF NOT EXISTS reports (id INTEGER PRIMARY KEY AUTOINCREMENT, reporter TEXT, target TEXT, reason TEXT,
        text TEXT, match TEXT, created REAL, status TEXT DEFAULT 'open');
    CREATE TABLE IF NOT EXISTS bugs (id INTEGER PRIMARY KEY AUTOINCREMENT, account TEXT, text TEXT, log TEXT,
        device TEXT, version TEXT, created REAL, status TEXT DEFAULT 'open');
    CREATE TABLE IF NOT EXISTS match_history (code TEXT, mode TEXT, kind TEXT, started REAL, ended REAL,
        players INTEGER, exit INTEGER, version TEXT, errors TEXT);
    """)
    db.commit()


def q_all(sql, args=()):
    with db_lock:
        return [dict(r) for r in db.execute(sql, args).fetchall()]


def q_one(sql, args=()):
    with db_lock:
        r = db.execute(sql, args).fetchone()
        return dict(r) if r else None


def q_run(sql, args=()):
    with db_lock:
        cur = db.execute(sql, args)
        db.commit()
        return cur


def hash_secret(secret):
    return hashlib.sha256(("zm-account:" + secret).encode()).hexdigest()


def clean_name(name):
    name = "".join(c for c in (name or "") if c.isprintable() and c not in "<>\"'&")
    name = name.strip()[:16]
    return name if len(name) >= 2 else "Oyuncu"


def new_account_id():
    while True:
        code = "".join(secrets.choice(CODE_ALPHABET) for _ in range(6))
        if not q_one("SELECT id FROM accounts WHERE id=?", (code,)):
            return code


def is_online(acc, now=None):
    return (now or time.time()) - (acc.get("last_seen") or 0) < ONLINE_SECONDS


def too_many(ip, kind, limit, window=60):
    now = time.time()
    key = (ip, kind)
    times = [t for t in api_tries.get(key, []) if now - t < window]
    api_tries[key] = times
    if len(times) >= limit:
        return True
    times.append(now)
    return False


def record_match_end(m, now, errors):
    try:
        q_run("INSERT INTO match_history VALUES (?,?,?,?,?,?,?,?,?)",
              (m["code"], m["mode"], m["kind"], m["created"], now, m.get("max_players", m["players"]),
               m["proc"].returncode, m["version"], "\n".join(errors)))
        q_run("DELETE FROM match_history WHERE ended < ?", (now - 30 * 86400,))
        q_run("UPDATE accounts SET status='', room='' WHERE status='match' AND room=?", (m["code"],))
    except Exception as e:
        log("match history failed:", e)


# ----- Admin -----

def admin_load():
    try:
        with open(ADMIN_FILE) as f:
            return json.load(f)
    except (OSError, ValueError):
        return None


def admin_save(cfg):
    tmp = ADMIN_FILE + ".tmp"
    with open(tmp, "w") as f:
        json.dump(cfg, f)
    os.chmod(tmp, 0o600)
    os.replace(tmp, ADMIN_FILE)


def admin_hash(password, salt):
    return hashlib.pbkdf2_hmac("sha256", password.encode(), bytes.fromhex(salt), 200000).hex()


def admin_set_password(password):
    salt = secrets.token_hex(16)
    admin_save({"salt": salt, "hash": admin_hash(password, salt), "changed": time.time()})


def admin_init():
    """First start: a random admin password the owner reads in the terminal (cat admin_password.txt)."""
    if admin_load():
        return
    password = "-".join("".join(secrets.choice("abcdefghjkmnpqrstuvwxyz23456789") for _ in range(4)) for _ in range(3))
    admin_set_password(password)
    with open(ADMIN_FIRST_PASSWORD, "w") as f:
        f.write(password + "\n")
    os.chmod(ADMIN_FIRST_PASSWORD, 0o600)
    log("admin password created in", ADMIN_FIRST_PASSWORD)


def admin_check(password):
    cfg = admin_load()
    if not cfg or not password:
        return False
    return hmac.compare_digest(admin_hash(password, cfg["salt"]), cfg["hash"])


def system_stats():
    stats = {"uptime": int(time.time() - started_at)}
    try:
        with open("/proc/loadavg") as f:
            stats["load"] = f.read().split()[:3]
        mem = {}
        with open("/proc/meminfo") as f:
            for line in f:
                k, v = line.split(":", 1)
                mem[k] = int(v.split()[0])
        stats["mem_total_mb"] = mem.get("MemTotal", 0) // 1024
        stats["mem_used_mb"] = (mem.get("MemTotal", 0) - mem.get("MemAvailable", 0)) // 1024
        st = os.statvfs(BASE)
        stats["disk_free_gb"] = round(st.f_bavail * st.f_frsize / 1e9, 1)
        stats["disk_total_gb"] = round(st.f_blocks * st.f_frsize / 1e9, 1)
        stats["cpus"] = os.cpu_count()
    except Exception:
        pass
    return stats


def log_tail(path, lines=150):
    try:
        with open(path, "rb") as f:
            f.seek(0, os.SEEK_END)
            f.seek(max(0, f.tell() - 60000))
            return f.read().decode("utf-8", "replace").splitlines()[-lines:]
    except OSError:
        return []


# ----- HTTP API -----

class Handler(BaseHTTPRequestHandler):
    server_version = "Zootopia/2"

    def log_message(self, fmt, *args):
        pass

    def send_body(self, code, body, ctype, extra=None):
        self.send_response(code)
        self.send_header("Content-Type", ctype)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Cache-Control", "no-store")
        for k, v in (extra or {}).items():
            self.send_header(k, v)
        self.end_headers()
        self.wfile.write(body)

    def reply(self, code, obj, extra=None):
        self.send_body(code, json.dumps(obj, ensure_ascii=False).encode("utf-8"), "application/json; charset=utf-8", extra)

    def params(self):
        u = urlparse(self.path)
        q = {k: v[0] for k, v in parse_qs(u.query).items()}
        return u.path.rstrip("/") or "/", q

    def ip(self):
        return self.client_address[0]

    def check_version(self, q):
        want = q.get("version", "")
        have = current_build.get("version")
        if have and want and want != have and "dev" not in (want, have):
            self.reply(409, {"ok": False, "error": "Sürüm uyuşmuyor: oyunu güncelle (yeni sürüm çıktıysa sunucu birkaç dakika içinde güncellenir)", "server": have})
            return False
        return True

    # --- accounts ---

    def account(self, required=True):
        """The calling player's account (headers X-ZM-Id / X-ZM-Secret). Replies with an error and returns None when not allowed."""
        acc_id = (self.headers.get("X-ZM-Id") or "").upper()
        secret = self.headers.get("X-ZM-Secret") or ""
        acc = q_one("SELECT * FROM accounts WHERE id=?", (acc_id,)) if acc_id else None
        if acc and not hmac.compare_digest(acc["secret_hash"], hash_secret(secret)):
            acc = None
        if acc is None:
            if required:
                self.reply(401, {"ok": False, "error": "Hesap bulunamadı, oyunu yeniden başlat", "relogin": True})
            return None
        if acc["banned"]:
            self.reply(403, {"ok": False, "banned": True, "error": "Hesabın yasaklandı" + (": " + acc["ban_reason"] if acc["ban_reason"] else "")})
            return None
        q_run("UPDATE accounts SET last_seen=?, last_ip=? WHERE id=?", (time.time(), self.ip(), acc["id"]))
        return acc

    def body_json(self, body):
        try:
            data = json.loads(body or b"{}")
            return data if isinstance(data, dict) else {}
        except ValueError:
            return {}

    def friends_view(self, me):
        now = time.time()
        rows = q_all("""SELECT f.a, f.b, f.state, x.id, x.name, x.last_seen, x.status, x.room, x.room_mode, x.status_time
                        FROM friends f JOIN accounts x ON x.id = CASE WHEN f.a=? THEN f.b ELSE f.a END
                        WHERE f.a=? OR f.b=?""", (me["id"], me["id"], me["id"]))
        friends, incoming, outgoing = [], [], []
        for r in rows:
            online = now - (r["last_seen"] or 0) < ONLINE_SECONDS
            item = {"id": r["id"], "name": r["name"], "online": online}
            if r["state"] == "accepted":
                status = r["status"] if online and now - (r["status_time"] or 0) < ONLINE_SECONDS * 2 else ""
                item.update({"status": status, "room": r["room"] if status == "room" else "", "mode": r["room_mode"]})
                friends.append(item)
            elif r["a"] == me["id"]:
                outgoing.append(item)
            else:
                incoming.append(item)
        friends.sort(key=lambda f: (not f["online"], f["name"].lower()))
        invites = q_all("""SELECT i.from_id AS id, a.name, i.room, i.mode FROM invites i JOIN accounts a ON a.id=i.from_id
                           WHERE i.to_id=? AND i.created>?""", (me["id"], now - 300))
        blocked = q_all("SELECT b.target AS id, a.name FROM blocks b JOIN accounts a ON a.id=b.target WHERE b.owner=?", (me["id"],))
        return {"ok": True, "me": {"id": me["id"], "name": me["name"]}, "friends": friends, "incoming": incoming,
                "outgoing": outgoing, "invites": invites, "blocked": blocked}

    def handle_account_post(self, path, q, body):
        ip = self.ip()
        if path == "/account/register":
            if too_many(ip, "register", 5):
                self.reply(429, {"ok": False, "error": "Çok sık deneme, biraz bekle"})
                return
            data = self.body_json(body)
            acc_id = new_account_id()
            secret = secrets.token_urlsafe(24)
            q_run("INSERT INTO accounts (id, secret_hash, name, created, last_seen, last_ip, device, version) VALUES (?,?,?,?,?,?,?,?)",
                  (acc_id, hash_secret(secret), clean_name(data.get("name")), time.time(), time.time(), ip,
                   str(data.get("device", ""))[:80], str(data.get("version", ""))[:40]))
            log("new account", acc_id)
            self.reply(200, {"ok": True, "id": acc_id, "secret": secret})
            return
        me = self.account()
        if not me:
            return
        data = self.body_json(body)
        other = (q.get("id") or data.get("id") or "").upper().replace("-", "").strip()
        if path == "/account/hello":
            q_run("UPDATE accounts SET name=?, version=?, device=? WHERE id=?",
                  (clean_name(data.get("name")), str(data.get("version", ""))[:40], str(data.get("device", ""))[:80], me["id"]))
            self.reply(200, {"ok": True, "id": me["id"]})
        elif path == "/friends":   # poll: presence + everything the friends screen shows
            status = data.get("status", "")
            if status in ("lobby", "room"):
                q_run("UPDATE accounts SET status=?, room=?, room_mode=?, status_time=? WHERE id=?",
                      (status, str(data.get("room", ""))[:6], str(data.get("mode", ""))[:8], time.time(), me["id"]))
            self.reply(200, self.friends_view(me))
        elif path == "/friends/add":
            if too_many(ip, "friendadd", 20):
                self.reply(429, {"ok": False, "error": "Çok sık deneme, biraz bekle"})
                return
            target = q_one("SELECT id, name FROM accounts WHERE id=?", (other,))
            if not target or target["id"] == me["id"]:
                self.reply(404, {"ok": False, "error": "Bu kodda bir oyuncu yok"})
                return
            if q_one("SELECT 1 FROM blocks WHERE owner=? AND target=?", (me["id"], target["id"])):
                q_run("DELETE FROM blocks WHERE owner=? AND target=?", (me["id"], target["id"]))
            back = q_one("SELECT state FROM friends WHERE a=? AND b=?", (target["id"], me["id"]))
            if back:   # they asked first: accept
                q_run("UPDATE friends SET state='accepted' WHERE a=? AND b=?", (target["id"], me["id"]))
                self.reply(200, {"ok": True, "message": target["name"] + " artık arkadaşın"})
                return
            if not q_one("SELECT 1 FROM blocks WHERE owner=? AND target=?", (target["id"], me["id"])):
                q_run("INSERT OR IGNORE INTO friends VALUES (?,?,'pending',?)", (me["id"], target["id"], time.time()))
            self.reply(200, {"ok": True, "message": "İstek gönderildi: " + target["name"]})   # same answer when blocked
        elif path == "/friends/accept":
            q_run("UPDATE friends SET state='accepted' WHERE a=? AND b=? AND state='pending'", (other, me["id"]))
            self.reply(200, self.friends_view(me))
        elif path == "/friends/remove":
            q_run("DELETE FROM friends WHERE (a=? AND b=?) OR (a=? AND b=?)", (me["id"], other, other, me["id"]))
            self.reply(200, self.friends_view(me))
        elif path == "/friends/invite":
            room = str(data.get("room", q.get("room", "")))[:6]
            friend = q_one("SELECT 1 FROM friends WHERE state='accepted' AND ((a=? AND b=?) OR (a=? AND b=?))", (me["id"], other, other, me["id"]))
            if not friend or not re.fullmatch(r"\d{6}", room):
                self.reply(400, {"ok": False, "error": "Davet gönderilemedi"})
                return
            q_run("INSERT OR REPLACE INTO invites VALUES (?,?,?,?,?)", (me["id"], other, room, str(data.get("mode", ""))[:8], time.time()))
            self.reply(200, {"ok": True})
        elif path == "/friends/dismiss":
            q_run("DELETE FROM invites WHERE from_id=? AND to_id=?", (other, me["id"]))
            self.reply(200, {"ok": True})
        elif path == "/block":
            if other and other != me["id"] and q_one("SELECT 1 FROM accounts WHERE id=?", (other,)):
                q_run("INSERT OR IGNORE INTO blocks VALUES (?,?,?)", (me["id"], other, time.time()))
                q_run("DELETE FROM friends WHERE (a=? AND b=?) OR (a=? AND b=?)", (me["id"], other, other, me["id"]))
                q_run("DELETE FROM invites WHERE from_id=? AND to_id=?", (other, me["id"]))
            self.reply(200, self.friends_view(me))
        elif path == "/unblock":
            q_run("DELETE FROM blocks WHERE owner=? AND target=?", (me["id"], other))
            self.reply(200, self.friends_view(me))
        elif path == "/report/player":
            if too_many(ip, "report", 10, 600):
                self.reply(429, {"ok": False, "error": "Çok fazla şikayet gönderdin, biraz bekle"})
                return
            if not q_one("SELECT 1 FROM accounts WHERE id=?", (other,)) or other == me["id"]:
                self.reply(404, {"ok": False, "error": "Oyuncu bulunamadı"})
                return
            q_run("INSERT INTO reports (reporter, target, reason, text, match, created) VALUES (?,?,?,?,?,?)",
                  (me["id"], other, str(data.get("reason", ""))[:40], str(data.get("text", ""))[:500],
                   str(data.get("match", ""))[:6], time.time()))
            self.reply(200, {"ok": True, "message": "Şikayetin alındı, teşekkürler"})
        elif path == "/bug":
            if too_many(ip, "bug", 5, 600):
                self.reply(429, {"ok": False, "error": "Çok fazla bildirim gönderdin, biraz bekle"})
                return
            q_run("INSERT INTO bugs (account, text, log, device, version, created) VALUES (?,?,?,?,?,?)",
                  (me["id"], str(data.get("text", ""))[:2000], str(data.get("log", ""))[:40000],
                   str(data.get("device", ""))[:120], str(data.get("version", ""))[:40], time.time()))
            self.reply(200, {"ok": True, "message": "Hata bildirimin alındı, teşekkürler"})
        else:
            self.reply(404, {"ok": False, "error": "not found"})

    # --- admin ---

    def admin_ok(self):
        cookie = self.headers.get("Cookie") or ""
        m = re.search(r"zm_admin=([A-Za-z0-9_\-]+)", cookie)
        if not m:
            return False
        exp = admin_sessions.get(m.group(1))
        return exp is not None and exp > time.time()

    def handle_admin(self, method, path, q, body):
        if path == "/admin":
            self.send_body(200, ADMIN_HTML.encode("utf-8"), "text/html; charset=utf-8")
            return
        if path == "/admin/login" and method == "POST":
            ip = self.ip()
            now = time.time()
            tries = [t for t in login_tries.get(ip, []) if now - t < 300]
            login_tries[ip] = tries
            if len(tries) >= 8:
                self.reply(429, {"ok": False, "error": "Çok fazla deneme, 5 dakika bekle"})
                return
            tries.append(now)
            if not admin_check(self.body_json(body).get("password", "")):
                self.reply(403, {"ok": False, "error": "Şifre yanlış"})
                return
            token = secrets.token_urlsafe(32)
            admin_sessions[token] = now + 12 * 3600
            self.reply(200, {"ok": True}, {"Set-Cookie": "zm_admin=%s; HttpOnly; SameSite=Strict; Path=/admin; Max-Age=43200" % token})
            return
        if not self.admin_ok():
            self.reply(401, {"ok": False, "error": "Giriş yap"})
            return
        data = self.body_json(body)
        now = time.time()
        if path == "/admin/api/overview":
            with lock:
                live = [dict(public_view(m), age=int(now - m["created"])) for m in matches.values()]
            self.reply(200, {"ok": True, "version": current_build.get("version"), "system": system_stats(),
                             "matches": live, "maxMatches": MAX_MATCHES,
                             "online": q_all("SELECT id, name, status, room, last_seen FROM accounts WHERE last_seen>? ORDER BY name", (now - ONLINE_SECONDS,)),
                             "counts": {
                                 "accounts": q_one("SELECT COUNT(*) n FROM accounts")["n"],
                                 "new_today": q_one("SELECT COUNT(*) n FROM accounts WHERE created>?", (now - 86400,))["n"],
                                 "active_today": q_one("SELECT COUNT(*) n FROM accounts WHERE last_seen>?", (now - 86400,))["n"],
                                 "matches_today": q_one("SELECT COUNT(*) n FROM match_history WHERE ended>?", (now - 86400,))["n"],
                                 "open_reports": q_one("SELECT COUNT(*) n FROM reports WHERE status='open'")["n"],
                                 "open_bugs": q_one("SELECT COUNT(*) n FROM bugs WHERE status='open'")["n"],
                                 "banned": q_one("SELECT COUNT(*) n FROM accounts WHERE banned=1")["n"]}})
        elif path == "/admin/api/players":
            term = "%" + q.get("q", "") + "%"
            rows = q_all("""SELECT id, name, created, last_seen, device, version, matches, banned, ban_reason,
                            (SELECT COUNT(*) FROM reports r WHERE r.target=accounts.id) AS reports
                            FROM accounts WHERE id LIKE ? OR name LIKE ? ORDER BY last_seen DESC LIMIT 200""", (term.upper(), term))
            self.reply(200, {"ok": True, "players": rows})
        elif path == "/admin/api/ban":
            q_run("UPDATE accounts SET banned=1, ban_reason=?, banned_at=? WHERE id=?",
                  (str(data.get("reason", ""))[:120], now, str(data.get("id", "")).upper()))
            log("admin banned", data.get("id"))
            self.reply(200, {"ok": True})
        elif path == "/admin/api/unban":
            q_run("UPDATE accounts SET banned=0, ban_reason='' WHERE id=?", (str(data.get("id", "")).upper(),))
            log("admin unbanned", data.get("id"))
            self.reply(200, {"ok": True})
        elif path == "/admin/api/reports":
            rows = q_all("""SELECT r.*, a.name AS reporter_name, t.name AS target_name, t.banned AS target_banned,
                            (SELECT COUNT(*) FROM reports x WHERE x.target=r.target) AS target_reports
                            FROM reports r LEFT JOIN accounts a ON a.id=r.reporter LEFT JOIN accounts t ON t.id=r.target
                            WHERE r.status=? ORDER BY r.created DESC LIMIT 200""", (q.get("status", "open"),))
            self.reply(200, {"ok": True, "reports": rows})
        elif path == "/admin/api/bugs":
            rows = q_all("""SELECT b.id, b.account, a.name, b.text, b.device, b.version, b.created, b.status, length(b.log) AS log_size
                            FROM bugs b LEFT JOIN accounts a ON a.id=b.account WHERE b.status=? ORDER BY b.created DESC LIMIT 200""",
                         (q.get("status", "open"),))
            self.reply(200, {"ok": True, "bugs": rows})
        elif path == "/admin/api/bug":
            self.reply(200, {"ok": True, "bug": q_one("SELECT * FROM bugs WHERE id=?", (int(q.get("id", "0") or 0),))})
        elif path == "/admin/api/close":
            table = "reports" if data.get("kind") == "report" else "bugs"
            q_run("UPDATE %s SET status=? WHERE id=?" % table, ("done" if data.get("done", True) else "open", int(data.get("id", 0))))
            self.reply(200, {"ok": True})
        elif path == "/admin/api/server":
            self.reply(200, {"ok": True, "recent": list(recent),
                             "history": q_all("SELECT * FROM match_history ORDER BY ended DESC LIMIT 50"),
                             "log": log_tail(os.path.join(LOGS, "orchestrator.log"))})
        elif path == "/admin/api/password":
            new = str(data.get("password", ""))
            if len(new) < 8:
                self.reply(400, {"ok": False, "error": "En az 8 karakter"})
                return
            admin_set_password(new)
            try:
                os.remove(ADMIN_FIRST_PASSWORD)
            except OSError:
                pass
            admin_sessions.clear()
            self.reply(200, {"ok": True, "message": "Şifre değişti, yeniden giriş yap"})
        elif path == "/admin/logout":
            if data.get("all"):
                admin_sessions.clear()
            self.reply(200, {"ok": True}, {"Set-Cookie": "zm_admin=; Path=/admin; Max-Age=0"})
        else:
            self.reply(404, {"ok": False, "error": "not found"})

    # --- routing ---

    def do_GET(self):
        path, q = self.params()
        if path == "/status":
            with lock:
                self.reply(200, {"ok": True, "version": current_build.get("version"), "maxMatches": MAX_MATCHES,
                                 "matches": [public_view(m) for m in matches.values()],
                                 "recent": list(recent)})
            return
        if path == "/admin" or path.startswith("/admin/"):
            self.handle_admin("GET", path, q, b"")
            return
        mm = re.fullmatch(r"/room/(\d{6})", path)
        if mm:
            if not self.check_version(q):
                return
            if self.headers.get("X-ZM-Id") and not self.account():
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
        limit = 65536 if path in ("/bug",) else 8192
        length = min(int(self.headers.get("Content-Length") or 0), limit)
        body = self.rfile.read(length) if length else b""
        ip = self.ip()

        if path == "/admin" or path.startswith("/admin/"):
            self.handle_admin("POST", path, q, body)
            return

        if path in ("/report", "/verify") and ip in ("127.0.0.1", "::1"):
            data = self.body_json(body)
            if path == "/verify":   # game server: is this player who they say, and not banned?
                acc = q_one("SELECT id, name, secret_hash, banned, ban_reason FROM accounts WHERE id=?", (str(data.get("id", "")).upper(),))
                if not acc or not hmac.compare_digest(acc["secret_hash"], hash_secret(str(data.get("secret", "")))):
                    self.reply(200, {"ok": False, "error": "Hesap doğrulanamadı"})
                elif acc["banned"]:
                    self.reply(200, {"ok": False, "error": "Hesabın yasaklandı" + (": " + acc["ban_reason"] if acc["ban_reason"] else "")})
                else:
                    self.reply(200, {"ok": True, "name": acc["name"]})
                return
            count_now = False
            with lock:
                m = matches.get(str(data.get("code", "")))
                if m:
                    m["state"] = data.get("state", m["state"])
                    m["players"] = int(data.get("players", m["players"]))
                    m["max_players"] = max(m.get("max_players", 0), m["players"])
                    if m["state"] == "playing" and not m.get("counted"):
                        m["counted"] = count_now = True
            accounts = [str(a).upper() for a in data.get("accounts", []) if isinstance(a, str)][:16]
            if accounts and data.get("state") in ("playing", "starting", "waiting"):
                now = time.time()
                for a in accounts:
                    q_run("UPDATE accounts SET status='match', room=?, status_time=?, last_seen=? WHERE id=?", (str(data.get("code", "")), now, now, a))
                    if count_now:
                        q_run("UPDATE accounts SET matches=matches+1 WHERE id=?", (a,))
            self.reply(200, {"ok": True})
            return
        if path.startswith("/account/") or path.startswith("/friends") or path in ("/block", "/unblock", "/bug", "/report/player"):
            self.handle_account_post(path, q, body)
            return

        mode = q.get("mode", "solo")
        if mode not in MAX_HUMANS:
            mode = "solo"
        if path in ("/quick", "/room/create"):
            if not self.check_version(q):
                return
            if self.headers.get("X-ZM-Id") and not self.account():
                return
        if path == "/quick":
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
    db_init()
    admin_init()
    for target in (build_loop, reap_loop, self_update_loop):
        threading.Thread(target=target, daemon=True).start()
    log("orchestrator listening on", API_PORT, "max matches", MAX_MATCHES)
    ThreadingHTTPServer(("0.0.0.0", API_PORT), Handler).serve_forever()


ADMIN_HTML = r"""<!doctype html>
<html lang="tr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Zootopia Yönetim</title>
<style>
:root { --bg:#0e1116; --panel:#171b22; --line:#262c36; --text:#e8ebf0; --dim:#8b95a5; --accent:#ffd02e; --good:#4ad37a; --bad:#ff5a4f; --blue:#3d8bff; }
* { box-sizing:border-box; }
body { margin:0; background:var(--bg); color:var(--text); font:15px/1.45 system-ui,-apple-system,"Segoe UI",Roboto,sans-serif; }
header { position:sticky; top:0; z-index:5; background:#0b0d11ee; backdrop-filter:blur(8px); border-bottom:1px solid var(--line); }
.bar { display:flex; align-items:center; gap:10px; padding:12px 16px; }
.logo { font-weight:800; letter-spacing:.5px; } .logo b { color:var(--accent); }
.bar .sp { flex:1; }
nav { display:flex; gap:4px; overflow-x:auto; padding:0 12px 10px; scrollbar-width:none; }
nav button { flex:none; background:none; border:1px solid var(--line); color:var(--dim); border-radius:999px; padding:7px 14px; font:inherit; font-size:14px; }
nav button.on { background:var(--accent); color:#111; border-color:var(--accent); font-weight:700; }
.badge { display:inline-block; min-width:18px; padding:0 5px; margin-left:4px; border-radius:9px; background:var(--bad); color:#fff; font-size:11px; font-weight:700; text-align:center; }
main { max-width:1100px; margin:0 auto; padding:16px; }
.grid { display:grid; grid-template-columns:repeat(auto-fit,minmax(150px,1fr)); gap:10px; }
.card { background:var(--panel); border:1px solid var(--line); border-radius:12px; padding:14px; }
.stat .n { font-size:26px; font-weight:800; } .stat .l { color:var(--dim); font-size:13px; }
h2 { font-size:15px; text-transform:uppercase; letter-spacing:.6px; color:var(--dim); margin:22px 0 10px; }
.list .row { display:flex; flex-wrap:wrap; gap:6px 12px; align-items:center; padding:12px 14px; border-bottom:1px solid var(--line); }
.list .row:last-child { border-bottom:0; }
.list { background:var(--panel); border:1px solid var(--line); border-radius:12px; overflow:hidden; }
.name { font-weight:700; } .id { font-family:ui-monospace,monospace; color:var(--accent); font-size:13px; }
.dim { color:var(--dim); font-size:13px; } .grow { flex:1; min-width:120px; }
.pill { font-size:12px; padding:2px 8px; border-radius:999px; border:1px solid var(--line); color:var(--dim); }
.pill.good { color:var(--good); border-color:#2c5a3b; } .pill.bad { color:var(--bad); border-color:#6a2b27; } .pill.acc { color:var(--accent); border-color:#6b5a1a; }
button.b { background:#232a35; color:var(--text); border:1px solid var(--line); border-radius:8px; padding:7px 12px; font:inherit; font-size:14px; }
button.b.red { background:#3a1715; border-color:#6a2b27; color:#ffb3ad; } button.b.green { background:#15301f; border-color:#2c5a3b; color:#a9f0c3; }
button.b.primary { background:var(--accent); color:#111; border-color:var(--accent); font-weight:700; }
input, select, textarea { background:#0b0e13; color:var(--text); border:1px solid var(--line); border-radius:8px; padding:10px 12px; font:inherit; width:100%; }
.text { white-space:pre-wrap; word-break:break-word; background:#0b0e13; border:1px solid var(--line); border-radius:8px; padding:10px; font-size:13px; width:100%; }
pre.log { white-space:pre-wrap; word-break:break-word; font-size:12px; color:#c8d0dc; background:#0b0e13; border:1px solid var(--line); border-radius:10px; padding:12px; max-height:420px; overflow:auto; }
.login { max-width:360px; margin:12vh auto; } .login h1 { font-size:22px; } .login .card > * + * { margin-top:12px; }
.err { color:var(--bad); min-height:20px; } .empty { padding:18px; color:var(--dim); text-align:center; }
.meter { height:6px; background:#0b0e13; border-radius:3px; overflow:hidden; margin-top:6px; } .meter i { display:block; height:100%; background:var(--blue); }
.toast { position:fixed; left:50%; bottom:18px; transform:translateX(-50%); background:#222a36; border:1px solid var(--line); padding:10px 16px; border-radius:10px; display:none; z-index:9; }
.tabs2 { display:flex; gap:6px; margin-bottom:10px; }
</style>
</head>
<body>
<div id="login" class="login" style="display:none">
  <div class="card">
    <h1>Zootopia <span style="color:var(--accent)">Yönetim</span></h1>
    <div class="dim">İlk şifre sunucuda: terminalde <code>cat /opt/zootopia/admin_password.txt</code></div>
    <input id="pw" type="password" placeholder="Yönetici şifresi" autocomplete="current-password">
    <button class="b primary" style="width:100%" onclick="login()">Giriş</button>
    <div class="err" id="loginErr"></div>
  </div>
</div>
<div id="app" style="display:none">
<header>
  <div class="bar"><div class="logo">ZOOTOPIA <b>YÖNETİM</b></div><div class="sp"></div><span class="dim" id="ver"></span></div>
  <nav id="nav">
    <button data-t="genel" class="on">Genel</button>
    <button data-t="oyuncular">Oyuncular</button>
    <button data-t="sikayetler">Şikayetler<span class="badge" id="bRep" hidden></span></button>
    <button data-t="hatalar">Hata bildirimleri<span class="badge" id="bBug" hidden></span></button>
    <button data-t="sunucu">Sunucu</button>
    <button data-t="ayarlar">Ayarlar</button>
  </nav>
</header>
<main id="main"></main>
</div>
<div class="toast" id="toast"></div>
<script>
const $ = s => document.querySelector(s);
const esc = s => String(s ?? "").replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c]));
const ago = t => { if (!t) return "–"; const s = Date.now()/1000 - t; if (s < 60) return "az önce"; if (s < 3600) return Math.floor(s/60)+" dk önce"; if (s < 86400) return Math.floor(s/3600)+" sa önce"; return Math.floor(s/86400)+" gün önce"; };
const dur = s => s < 3600 ? Math.floor(s/60)+" dk" : (s < 86400 ? Math.floor(s/3600)+" sa "+Math.floor(s%3600/60)+" dk" : Math.floor(s/86400)+" gün");
let tab = "genel", timer = null;
function toast(m) { const t = $("#toast"); t.textContent = m; t.style.display = "block"; clearTimeout(t._h); t._h = setTimeout(() => t.style.display = "none", 2500); }
async function api(path, body) {
  const r = await fetch("/admin/" + path, body === undefined ? {} : {method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify(body)});
  if (r.status === 401) { showLogin(); throw new Error("login"); }
  return r.json();
}
function showLogin() { $("#app").style.display = "none"; $("#login").style.display = "block"; clearInterval(timer); }
async function login() {
  const r = await fetch("/admin/login", {method:"POST", headers:{"Content-Type":"application/json"}, body:JSON.stringify({password:$("#pw").value})});
  const j = await r.json();
  if (!j.ok) { $("#loginErr").textContent = j.error || "Olmadı"; return; }
  $("#login").style.display = "none"; $("#app").style.display = "block"; show("genel");
}
$("#pw").addEventListener("keydown", e => { if (e.key === "Enter") login(); });
$("#nav").addEventListener("click", e => { const b = e.target.closest("button"); if (b) show(b.dataset.t); });
function show(t) { tab = t; document.querySelectorAll("#nav button").forEach(b => b.classList.toggle("on", b.dataset.t === t)); render(); clearInterval(timer); if (t === "genel" || t === "sunucu") timer = setInterval(render, 10000); }
async function render() { try { await ({genel, oyuncular, sikayetler, hatalar, sunucu, ayarlar})[tab](); } catch (e) { if (e.message !== "login") $("#main").innerHTML = '<div class="empty">Yüklenemedi: '+esc(e.message)+'</div>'; } }

async function genel() {
  const j = await api("api/overview"), c = j.counts, s = j.system || {};
  $("#ver").textContent = "sürüm " + (j.version || "yok");
  badge("#bRep", c.open_reports); badge("#bBug", c.open_bugs);
  const mem = s.mem_total_mb ? Math.round(100*s.mem_used_mb/s.mem_total_mb) : 0;
  const disk = s.disk_total_gb ? Math.round(100*(1 - s.disk_free_gb/s.disk_total_gb)) : 0;
  $("#main").innerHTML = `
  <div class="grid">
    ${stat(j.online.length, "Şu an çevrimiçi")}${stat(j.matches.length + " / " + j.maxMatches, "Süren maç")}
    ${stat(c.active_today, "Bugün oynayan")}${stat(c.matches_today, "Bugünkü maç")}
    ${stat(c.accounts, "Toplam oyuncu (+" + c.new_today + " bugün)")}${stat(c.open_reports, "Açık şikayet")}
    ${stat(c.open_bugs, "Açık hata bildirimi")}${stat(c.banned, "Yasaklı")}
  </div>
  <h2>Sunucu</h2>
  <div class="grid">
    <div class="card stat"><div class="n">${(s.load||["–"])[0]}</div><div class="l">İşlemci yükü (${s.cpus||"?"} çekirdek)</div><div class="meter"><i style="width:${Math.min(100, 100*(+(s.load||[0])[0])/(s.cpus||1))}%"></i></div></div>
    <div class="card stat"><div class="n">%${mem}</div><div class="l">Bellek ${s.mem_used_mb||0} / ${s.mem_total_mb||0} MB</div><div class="meter"><i style="width:${mem}%"></i></div></div>
    <div class="card stat"><div class="n">%${disk}</div><div class="l">Disk (${s.disk_free_gb||0} GB boş)</div><div class="meter"><i style="width:${disk}%"></i></div></div>
    <div class="card stat"><div class="n">${dur(s.uptime||0)}</div><div class="l">Çalışma süresi</div></div>
  </div>
  <h2>Süren maçlar</h2>
  <div class="list">${j.matches.length ? j.matches.map(m => `<div class="row"><span class="id">${m.code}</span><span class="name">${m.kind === "private" ? "Özel oda" : "Hızlı maç"} · ${m.mode.toUpperCase()}</span><span class="pill ${m.state === "playing" ? "good" : "acc"}">${({waiting:"bekleme odası",starting:"başlıyor",playing:"oynanıyor",ended:"bitti"})[m.state]||m.state}</span><span class="grow dim">${m.players} oyuncu · ${dur(m.age)}</span></div>`).join("") : '<div class="empty">Şu an maç yok</div>'}</div>
  <h2>Çevrimiçi oyuncular</h2>
  <div class="list">${j.online.length ? j.online.map(p => `<div class="row"><span class="name">${esc(p.name)}</span><span class="id">${p.id}</span><span class="grow dim">${p.status === "match" ? "maçta (" + esc(p.room) + ")" : p.status === "room" ? "odada " + esc(p.room) : "lobide"}</span></div>`).join("") : '<div class="empty">Kimse çevrimiçi değil</div>'}</div>`;
}
const stat = (n, l) => `<div class="card stat"><div class="n">${n}</div><div class="l">${l}</div></div>`;
function badge(sel, n) { const b = $(sel); b.hidden = !n; b.textContent = n; }

async function oyuncular(term) {
  const j = await api("api/players?q=" + encodeURIComponent(term || ""));
  $("#main").innerHTML = `<input id="q" placeholder="İsim ya da kod ara" value="${esc(term||"")}"><div style="height:10px"></div>
  <div class="list">${j.players.length ? j.players.map(p => `<div class="row">
    <span class="name">${esc(p.name)}</span><span class="id">${p.id}</span>
    ${p.banned ? '<span class="pill bad">yasaklı</span>' : ""}${p.reports ? `<span class="pill acc">${p.reports} şikayet</span>` : ""}
    <span class="grow dim">son görülme ${ago(p.last_seen)} · ${p.matches} maç · ${esc(p.device||"")}${p.banned && p.ban_reason ? " · sebep: " + esc(p.ban_reason) : ""}</span>
    ${p.banned ? `<button class="b green" onclick="unban('${p.id}')">Yasağı kaldır</button>` : `<button class="b red" onclick="ban('${p.id}','${esc(p.name)}')">Yasakla</button>`}
  </div>`).join("") : '<div class="empty">Oyuncu yok</div>'}</div>`;
  const q = $("#q"); q.focus(); q.setSelectionRange(q.value.length, q.value.length);
  q.oninput = () => { clearTimeout(q._t); q._t = setTimeout(() => oyuncular(q.value), 350); };
}
async function ban(id, name) {
  const reason = prompt(name + " (" + id + ") yasaklansın mı? Sebep yaz (oyuncuya gösterilir):", "Kurallara aykırı davranış");
  if (reason === null) return;
  await api("api/ban", {id, reason}); toast(name + " yasaklandı"); render();
}
async function unban(id) { await api("api/unban", {id}); toast("Yasak kaldırıldı"); render(); }

let repStatus = "open";
async function sikayetler() {
  const j = await api("api/reports?status=" + repStatus);
  const reasons = {cheat:"Hile", abuse:"Küfür / hakaret", voice:"Seste rahatsız etme", name:"Uygunsuz isim", afk:"Oynamıyor / AFK", other:"Diğer"};
  $("#main").innerHTML = `<div class="tabs2"><button class="b ${repStatus==="open"?"primary":""}" onclick="repStatus='open';render()">Açık</button><button class="b ${repStatus==="done"?"primary":""}" onclick="repStatus='done';render()">Kapatılan</button></div>
  <div class="list">${j.reports.length ? j.reports.map(r => `<div class="row">
    <span class="pill acc">${esc(reasons[r.reason] || r.reason || "–")}</span>
    <span class="name">${esc(r.target_name || "?")}</span><span class="id">${esc(r.target)}</span>
    ${r.target_banned ? '<span class="pill bad">yasaklı</span>' : ""}<span class="pill">${r.target_reports} şikayet</span>
    <span class="grow dim">${esc(r.reporter_name || "?")} şikayet etti · ${ago(r.created)}${r.match ? " · maç " + esc(r.match) : ""}</span>
    ${r.text ? `<div class="text">${esc(r.text)}</div>` : ""}
    <div style="display:flex;gap:6px;flex-wrap:wrap">${r.target_banned ? "" : `<button class="b red" onclick="ban('${esc(r.target)}','${esc(r.target_name||"")}')">Yasakla</button>`}
    <button class="b" onclick="closeItem('report',${r.id},${repStatus==="open"})">${repStatus==="open" ? "Kapat" : "Yeniden aç"}</button></div>
  </div>`).join("") : '<div class="empty">Şikayet yok</div>'}</div>`;
}
let bugStatus = "open";
async function hatalar() {
  const j = await api("api/bugs?status=" + bugStatus);
  $("#main").innerHTML = `<div class="tabs2"><button class="b ${bugStatus==="open"?"primary":""}" onclick="bugStatus='open';render()">Açık</button><button class="b ${bugStatus==="done"?"primary":""}" onclick="bugStatus='done';render()">Kapatılan</button></div>
  <div class="list">${j.bugs.length ? j.bugs.map(b => `<div class="row">
    <span class="name">${esc(b.name || "?")}</span><span class="id">${esc(b.account)}</span>
    <span class="grow dim">${ago(b.created)} · ${esc(b.device || "")} · sürüm ${esc(b.version || "?")}</span>
    <div class="text">${esc(b.text || "(açıklama yok)")}</div>
    <div style="display:flex;gap:6px;flex-wrap:wrap">${b.log_size ? `<button class="b" onclick="showLog(${b.id}, this)">Kayıtları göster (${Math.round(b.log_size/1024)} KB)</button>` : ""}
    <button class="b" onclick="closeItem('bug',${b.id},${bugStatus==="open"})">${bugStatus==="open" ? "Çözüldü" : "Yeniden aç"}</button></div>
    <pre class="log" id="log${b.id}" style="display:none;width:100%"></pre>
  </div>`).join("") : '<div class="empty">Hata bildirimi yok</div>'}</div>`;
}
async function showLog(id, btn) { const el = $("#log" + id); if (el.style.display === "block") { el.style.display = "none"; return; } const j = await api("api/bug?id=" + id); el.textContent = j.bug.log || ""; el.style.display = "block"; }
async function closeItem(kind, id, done) { await api("api/close", {kind, id, done}); render(); }

async function sunucu() {
  const j = await api("api/server");
  $("#main").innerHTML = `<h2>Son biten maçlar</h2>
  <div class="list">${j.history.length ? j.history.map(m => `<div class="row"><span class="id">${m.code}</span><span class="name">${m.kind === "private" ? "Özel" : "Hızlı"} · ${m.mode}</span>
    <span class="pill ${m.exit === 0 ? "good" : "bad"}">${m.exit === 0 ? "normal bitti" : "çıkış kodu " + m.exit}</span>
    <span class="grow dim">${m.players} oyuncu · ${dur(Math.max(0, m.ended - m.started))} · ${ago(m.ended)}</span>
    ${m.exit !== 0 && m.errors ? `<pre class="log" style="width:100%">${esc(m.errors)}</pre>` : ""}</div>`).join("") : '<div class="empty">Henüz maç bitmedi</div>'}</div>
  <h2>Maç yöneticisi kayıtları</h2><pre class="log">${esc(j.log.join("\n"))}</pre>`;
  const l = document.querySelectorAll("pre.log"); l[l.length-1].scrollTop = 1e9;
}
async function ayarlar() {
  $("#main").innerHTML = `<div class="card" style="max-width:420px"><h2 style="margin-top:0">Şifre değiştir</h2>
  <input id="np" type="password" placeholder="Yeni şifre (en az 8 karakter)" autocomplete="new-password"><div style="height:10px"></div>
  <button class="b primary" onclick="changePw()">Değiştir</button><div class="err" id="pwErr"></div>
  <p class="dim">Şifreyi değiştirince sunucudaki ilk şifre dosyası silinir ve herkesin oturumu kapanır.</p></div>
  <div style="height:12px"></div><button class="b" onclick="logout()">Çıkış yap</button>`;
}
async function changePw() { const j = await api("api/password", {password:$("#np").value}); if (!j.ok) { $("#pwErr").textContent = j.error; return; } toast(j.message); showLogin(); }
async function logout() { await api("logout", {}); showLogin(); }

(async () => { const r = await fetch("/admin/api/overview"); if (r.status === 401) showLogin(); else { $("#app").style.display = "block"; show("genel"); } })();
</script>
</body>
</html>
"""


if __name__ == "__main__":
    main()
