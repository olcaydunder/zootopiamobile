"""Downloads CC0 PBR textures from Poly Haven (https://polyhaven.com, CC0) for the game.
Runs on GitHub Actions (internet). Writes Assets/Resources/Textures/<role>_diff.jpg and <role>_nor.jpg."""
import json, os, sys, time, urllib.request

UA = {"User-Agent": "ZootopiaMobile-textures/1.0 (github.com/olcaydunder/zootopiamobile)"}
OUT = "Assets/Resources/Textures"
os.makedirs(OUT, exist_ok=True)
RES = os.environ.get("TEX_RES", "1k")

ROLES = {
    "grass":    ["aerial_grass_rock", "grass_path", "grass", "meadow"],
    "forest":   ["forest_leaves", "forest_ground", "forest_floor", "leaves"],
    "dirt":     ["brown_mud", "dirt", "mud", "soil"],
    "asphalt":  ["asphalt_02", "asphalt"],
    "paving":   ["paving_stones", "pavement", "sidewalk", "cobblestone"],
    "concrete": ["concrete_wall", "concrete_floor", "concrete"],
    "plaster":  ["painted_plaster", "plastered_wall", "plaster", "white_plaster"],
    "rooftiles":["roof_tiles", "clay_roof", "roof"],
    "bark":     ["bark_brown", "bark"],
    "metal":    ["corrugated_iron", "metal_plate", "rusty_metal"],
}

def get(url):
    for i in range(4):
        try:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=120) as r:
                return r.read()
        except Exception as e:
            print("retry", url, e); time.sleep(3 * (i + 1))
    raise SystemExit("failed " + url)

assets = json.loads(get("https://api.polyhaven.com/assets?t=textures"))
ids = sorted(assets.keys())
manifest = {}
for role, keys in ROLES.items():
    pick = None
    for k in keys:
        cands = [i for i in ids if i == k] or [i for i in ids if i.startswith(k)] or [i for i in ids if k in i]
        if cands:
            pick = cands[0]; break
    if not pick:
        print("no texture for", role); continue
    files = json.loads(get("https://api.polyhaven.com/files/" + pick))
    ok = True
    for kind, suffix in (("Diffuse", "diff"), ("nor_gl", "nor")):
        entry = files.get(kind) or files.get(kind.lower())
        try:
            url = entry[RES]["jpg"]["url"]
        except Exception:
            print("missing", kind, "for", pick); ok = False; continue
        open(f"{OUT}/{role}_{suffix}.jpg", "wb").write(get(url))
    manifest[role] = {"id": pick, "name": assets[pick].get("name"), "authors": list(assets[pick].get("authors", {}).keys()),
                      "url": "https://polyhaven.com/a/" + pick}
    print(role, "->", pick, ok)
json.dump(manifest, open(f"{OUT}/polyhaven.json", "w"), indent=1, ensure_ascii=False)
