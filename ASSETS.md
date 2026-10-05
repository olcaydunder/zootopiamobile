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
Footstep sounds (asphalt, grass, gravel, floors, water; several variations each) are synthesised in
`Footsteps.cs`; gun attachments are code-built parts placed on points measured from each gun model (`GunAnchors.cs`).

## Sketchfab models (CC-BY 4.0 – attribution required)

Converted with `Tools/blender/convert_character.py` / `convert_prop.py` (decimated, base-colour textures only,
re-oriented; the character got the game's animations retargeted onto its Mixamo skeleton).

| In game | Original | Author | License |
| --- | --- | --- | --- |
| Kasap Leydi (character, `Models/Characters/LadyButcher.fbx`) | [Lady Butcher (WIP)](https://sketchfab.com/3d-models/lady-butcher-wip-9def0fb13b2c4bd8aabf16728622b10a) | [Loves_Art](https://sketchfab.com/Loves_Art) | CC-BY 4.0 |
| Alev Kartalı pistol (`Models/Guns/Pistol_Flame.fbx`) | [Custom Desert Eagle – Flame Edition (PBR)](https://sketchfab.com/3d-models/custom-desert-eagle-flame-edition-pbr-27790d1905ce41938619) | [Fevzi_Beydili](https://sketchfab.com/Fevzi_Beydili) | CC-BY 4.0 |
| AK-19 Taktik rifle (`Models/Guns/Rifle_AK19.fbx`) | [low-poly AK-19](https://sketchfab.com/3d-models/low-poly-ak-19-ab301629b9c44c80a2f9bb2aecd858c7) | [D_U](https://sketchfab.com/DU1701) | CC-BY 4.0 |
| AR-15 Saha rifle (`Models/Guns/Rifle_AR15.fbx`) | [AR-15 style rifle](https://sketchfab.com/3d-models/ar-15-style-rifle-50d33435445e439c95e3b36e9d4bd798) (optic, GPS and sling left out) | [Mateusz Woliński](https://sketchfab.com/jeandiz) | CC-BY 4.0 |
| Gravürlü 1911 pistol (`Models/Guns/Pistol_Engraved.fbx`) | [Pistol with Engravings](https://sketchfab.com/3d-models/pistol-with-engravings-bccd75a0016d49448243135a56facb96) | [Mateusz Woliński](https://sketchfab.com/jeandiz) | CC-BY 4.0 |
| Gölge Avcı sniper (`Models/Guns/Sniper_Shadow.fbx`) | [Sniper](https://sketchfab.com/3d-models/sniper-b5c9cf7f76754805b3a756988da526e1) | [PSICOPATO](https://sketchfab.com/emily.archeo) | CC-BY 4.0 |
| Armour loot: helmet (`Models/Props/ArmorHelmet.fbx`) | [Tactical Helmet with Headset (Game-Ready)](https://sketchfab.com/3d-models/tactical-helmet-with-headset-game-ready-82adf376164f4d99a4) | [Exactly](https://sketchfab.com/txyrm70) | CC-BY 4.0 |
| Armour loot: vest (`Models/Props/ArmorVest.fbx`) | [Tactical Plate Carrier Vest – Game Ready](https://sketchfab.com/3d-models/tactical-plate-carrier-vest-game-ready-3b51e6329dbb4b0aa14) | [Exactly](https://sketchfab.com/txyrm70) | CC-BY 4.0 |

### Soldier characters (CC-BY 4.0)

Realistic, textured soldier characters (6–10k triangles, 1K base-colour texture each) in `Models/Characters`.
The Mixamo-rigged GLB copies were taken from [DevWolf11/Cam-Strike](https://github.com/DevWolf11/Cam-Strike)
(`assets/models/`, whose README credits them as CC BY 4.0), converted with `Tools/blender/convert_character.py`
(1.8 m tall, decimated, base colour only, the game's Idle/Run/Shoot/Hit/Death clips retargeted from SoldierMale).
The colour variants are the same models with recoloured clothing textures (`Tools/blender/recolor_variants.py`,
`Models/Characters/Variants/`, swapped in by `ModelLibrary.ApplyVariant`).

| In game | File | Original | Author | License |
| --- | --- | --- | --- | --- |
| Operatör, Çöl Operatörü, Gece Operatörü | `Operator.fbx` (+ `Variants/OperatorDesert`, `Variants/OperatorNight`) | [Soldier Full Tactical Gear (LowPolyGameReady)](https://skfb.ly/pMDAV) | DanlyVostok | CC-BY 4.0 |
| Piyade, Orman Piyadesi | `Infantry.fbx` (+ `Variants/InfantryWoodland`) | [Ukrainian Soldier](https://skfb.ly/ot9Ny) | [doctortex](https://sketchfab.com/doctortex) | CC-BY 4.0 |
| Paralı Asker, Kent Komandosu | `Mercenary.fbx` (+ `Variants/MercenaryUrban`) | [Terrorista](https://skfb.ly/6xsAy) | [jeferson](https://sketchfab.com/djotagame) | CC-BY 4.0 |
| Maskeli | `Masked.fbx` | [terrorist](https://skfb.ly/6AnKG) | [DJMaesen](https://sketchfab.com/bumstrum) | CC-BY 4.0 |
| SWAT | `SwatOperator.fbx` | [S.W.A.T. Operator](https://sketchfab.com/3d-models/swat-operator-9e82fabf26194896b5ad4a364d864eab) | [Mateusz Woliński](https://sketchfab.com/jeandiz) | CC-BY 4.0 |
| Özel Tim | `SwatElite.fbx` | [S.W.A.T. Operator- 4k Followers Special Remaster](https://sketchfab.com/3d-models/swat-operator-4k-followers-special-remaster-f6923917c8014578b1c1cb2b4c249268) | [Mateusz Woliński](https://sketchfab.com/jeandiz) | CC-BY 4.0 |
| Nova, Nova Kızıl, Nova Gece, Nova Orman | `Asuna.fbx` (+ `Variants/AsunaRed`, `AsunaBlack`, `AsunaGreen`) | [Free Test Character Asuna](https://sketchfab.com/3d-models/free-test-character-asuna-cc63f3c02d46486fb1243e3c06072a94) | [MSGDI (Markus Schüler)](https://sketchfab.com/MSGDI) | CC-BY 4.0 |

The SWAT models and the AR-15 / 1911 below were uploaded by the project owner (release `assets-v2`); Nova comes from the
author's own pack (release `assets-v1`, the same model the author publishes on Sketchfab under CC-BY 4.0). Converted with
`convert_character.py` (`prep_asuna.py` first for Nova: its own skeleton renamed to Mixamo names, base colours of the four
body and five hair colours put on): decimated, all parts joined, one 2048 px atlas per character (alpha-tested where the
hair cards are), the game's clips retargeted, unused bones (fingers, hair, face) folded into their parents.

Changes: decimated, textures reduced to 1K base colour, rescaled, re-rigged/retargeted animations, recoloured variants.
Not used from that repo: three Mixamo characters (Adobe licence, no redistribution) and `militia.glb` (no CC licence stated).

The same credits are shown in the game under Ayarlar → Künye.

Not used (license or IP reasons): Adam Smasher (Cyberpunk 2077), Warthog (Halo), Warden (Valorant), Vantage (Apex Legends),
Mercedes SLS (car brand), MD 500 (CC-BY-NC-SA), AK LR / XM25 (Sketchfab Standard – not allowed in a public repo),
two static figure scans, and an AK-47 pack without license information.
From the owner's uploads (`assets-v1`/`assets-v2`) also not used: "Amber" (Character Creator export of unknown origin;
Reallusion content may not be redistributed, 1.6 M triangles), "Stylized Female Character FREE" (RetroStyle Games – no
licence stated, so not allowed in a public repository) and "Survival Character" (Unreal Engine project files only, no FBX).

## Photo textures (Poly Haven, CC0)

Downloaded by `.github/workflows/textures.yml` (`Tools/fetch_textures.py`) into `Assets/Resources/Textures` (2K diffuse + normal; the workflow's `cozunurluk` input picks 1k/2k/4k).
Used by the terrain (5-layer splat), building façades (`Zootopia/Facade`), roads (`Zootopia/Road`), roofs, plinths and tree bark.

| Use | Texture | Authors |
| --- | --- | --- |
| grass | [Aerial Grass Rock](https://polyhaven.com/a/aerial_grass_rock) | Rob Tuytel |
| forest | [Forest Leaves 02](https://polyhaven.com/a/forest_leaves_02) | Rob Tuytel |
| dirt | [Brown Mud](https://polyhaven.com/a/brown_mud) | Rob Tuytel |
| asphalt | [Asphalt 02](https://polyhaven.com/a/asphalt_02) | Rob Tuytel |
| paving | [Pavement 01](https://polyhaven.com/a/pavement_01) | Rob Tuytel |
| concrete | [Concrete Wall 001](https://polyhaven.com/a/concrete_wall_001) | Dimitrios Savva, Rico Cilliers |
| plaster | [Painted Plaster Wall](https://polyhaven.com/a/painted_plaster_wall) | Amal Kumar |
| rooftiles | [Roof Tiles](https://polyhaven.com/a/roof_tiles) | Stephan Seeliger |
| bark | [Bark Brown 01](https://polyhaven.com/a/bark_brown_01) | Rob Tuytel |
| metal | [Corrugated Iron](https://polyhaven.com/a/corrugated_iron) | Jenelle van Heerden, Dimitrios Savva |

## Own art (made for this game, no third-party assets)

- Animal masks (`Assets/Resources/Models/Masks/k_*.fbx`) and their icons (`UI/Icons/gear_k_*.png`): low-poly heads built
  from primitives by `Tools/blender/build_masks.py` (Blender, run headless).
- Inventory, perk, game-mode, wheel, trophy and store icons (`UI/Icons/gear_*`, `inv_*`, `mode_*`, …): SVG drawings in
  `Tools/make_gear_icons.py`, rendered with cairosvg. Box art (`UI/Crates`, `crate_*`): `Tools/make_icons.py`.

`Assets/Resources/UI/Logo.png` (loading and title screens) is drawn by `Tools/make_logo.py` (own design: the first O of
ZOOTOPIA is a sight with a paw print) using [Russo One](https://fonts.google.com/specimen/Russo+One) by Jovanny Lemonad and
[Teko](https://fonts.google.com/specimen/Teko) by Indian Type Foundry – SIL Open Font License 1.1 (`Tools/fonts/`, license texts alongside).

## Font

[Barlow Condensed](https://github.com/google/fonts/tree/main/ofl/barlowcondensed) SemiBold by Jeremy Tribby – SIL Open Font License 1.1
(`Assets/Resources/Fonts/`, license text alongside).

## Map data

Three real places: the neighbourhood around Zootopia Veteriner Kliniği (Turgut Özal Cd., Ekşioğlu, Çekmeköy/İstanbul),
the town of Senir (Keçiborlu/Isparta) between Lake Burdur and the mountain behind it, and the rectorate campus of
Fırat Üniversitesi (Üniversite Mahallesi, Elazığ).

| Data | Source | License |
| --- | --- | --- |
| Buildings, streets, parks, woods | © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors | ODbL 1.0 (attribution shown in the game lobby and the map selection) |
| Terrain heights | [AWS Terrain Tiles](https://registry.opendata.aws/terrain-tiles/) (Mapzen; SRTM and other public sources) | see the registry page |

`.github/workflows/map-data.yml` downloads the raw data into `MapData/<map>/`; `Tools/build_map.py <map>` bakes it into
`Assets/Resources/Map/<map>/{height,ground,features}.bytes` (+ `preview.png`), which `MapData.cs` / `CityBuilder.cs` turn
into the 3D place at runtime. Where OpenStreetMap has streets but no buildings (most of Senir, parts of the campus), houses and
faculty blocks are placed along the streets by the script.
