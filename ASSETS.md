# Assets

All 3D models are CC0 (public domain). No attribution is required; credit is given here anyway.

| Pack | Author | License | Used for |
| --- | --- | --- | --- |
| [Ultimate Animated Character Pack](https://quaternius.com/packs/ultimatedanimatedcharacter.html) | Quaternius | CC0 1.0 | Player, teammates and opponents (`Assets/Resources/Models/Characters`) with Idle, Run, Shoot, Hit and Death animations |
| [Ultimate Guns](https://quaternius.com/packs/ultimategun.html) | Quaternius | CC0 1.0 | Rifle, SMG, shotgun, sniper and pistol (`Assets/Resources/Models/Guns`) |
| [Toon Shooter Game Kit](https://quaternius.com/packs/toonshootergamekit.html) | Quaternius | CC0 1.0 | Sandbags, barriers, containers, barrels, crates, grenade (`Assets/Resources/Models/Props`) |

The models were taken from the CC0 copies in [Karnak19/bagarre](https://github.com/Karnak19/bagarre) (see its ASSETS.md),
decompressed with gltf-transform, the shared animation clips merged into each character, and converted to FBX with Blender.

Everything else (terrain, sea, sky, trees, rocks, houses, jeep, plane, sounds, effects) is generated in code.

## Sketchfab models (CC-BY 4.0 – attribution required)

Converted with `Tools/blender/convert_character.py` / `convert_prop.py` (decimated, base-colour textures only,
re-oriented; the character got the game's animations retargeted onto its Mixamo skeleton).

| In game | Original | Author | License |
| --- | --- | --- | --- |
| Kasap Leydi (character, `Models/Characters/LadyButcher.fbx`) | [Lady Butcher (WIP)](https://sketchfab.com/3d-models/lady-butcher-wip-9def0fb13b2c4bd8aabf16728622b10a) | [Loves_Art](https://sketchfab.com/Loves_Art) | CC-BY 4.0 |
| Alev Kartalı pistol (`Models/Guns/Pistol_Flame.fbx`) | [Custom Desert Eagle – Flame Edition (PBR)](https://sketchfab.com/3d-models/custom-desert-eagle-flame-edition-pbr-27790d1905ce41938619) | [Fevzi_Beydili](https://sketchfab.com/Fevzi_Beydili) | CC-BY 4.0 |
| AK-19 Taktik rifle (`Models/Guns/Rifle_AK19.fbx`) | [low-poly AK-19](https://sketchfab.com/3d-models/low-poly-ak-19-ab301629b9c44c80a2f9bb2aecd858c7) | [D_U](https://sketchfab.com/DU1701) | CC-BY 4.0 |
| Gölge Avcı sniper (`Models/Guns/Sniper_Shadow.fbx`) | [Sniper](https://sketchfab.com/3d-models/sniper-b5c9cf7f76754805b3a756988da526e1) | [PSICOPATO](https://sketchfab.com/emily.archeo) | CC-BY 4.0 |
| Armour loot: helmet (`Models/Props/ArmorHelmet.fbx`) | [Tactical Helmet with Headset (Game-Ready)](https://sketchfab.com/3d-models/tactical-helmet-with-headset-game-ready-82adf376164f4d99a4) | [Exactly](https://sketchfab.com/txyrm70) | CC-BY 4.0 |
| Armour loot: vest (`Models/Props/ArmorVest.fbx`) | [Tactical Plate Carrier Vest – Game Ready](https://sketchfab.com/3d-models/tactical-plate-carrier-vest-game-ready-3b51e6329dbb4b0aa14) | [Exactly](https://sketchfab.com/txyrm70) | CC-BY 4.0 |

The same credits are shown in the game under Ayarlar → Künye.

Not used (license or IP reasons): Adam Smasher (Cyberpunk 2077), Warthog (Halo), Warden (Valorant), Vantage (Apex Legends),
Mercedes SLS (car brand), MD 500 (CC-BY-NC-SA), AK LR / XM25 (Sketchfab Standard – not allowed in a public repo),
two static figure scans, and an AK-47 pack without license information.

## Font

[Barlow Condensed](https://github.com/google/fonts/tree/main/ofl/barlowcondensed) SemiBold by Jeremy Tribby – SIL Open Font License 1.1
(`Assets/Resources/Fonts/`, license text alongside).

## Map data (Çekmeköy, Ekşioğlu)

The battle map is the real neighbourhood around Zootopia Veteriner Kliniği (Turgut Özal Cd., Ekşioğlu, Çekmeköy/İstanbul).

| Data | Source | License |
| --- | --- | --- |
| Buildings, streets, parks, woods | © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors | ODbL 1.0 (attribution shown in the game lobby) |
| Terrain heights | [AWS Terrain Tiles](https://registry.opendata.aws/terrain-tiles/) (Mapzen; SRTM and other public sources) | see the registry page |

`.github/workflows/map-data.yml` downloads the raw data into `MapData/`; `Tools/build_map.py` bakes it into
`Assets/Resources/Map/{height,ground,features}.bytes`, which `MapData.cs` / `CityBuilder.cs` turn into the 3D town at runtime.
