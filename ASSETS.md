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

### Owner uploads, release `assets-v3` (CC-BY 4.0)

Chosen by the project owner and uploaded to the `assets-v3` release; each GLB carries its Sketchfab licence and author.
Guns: `convert_prop.py` (re-oriented, real length, decimated, one atlas texture; animated ones in their first frame),
attachment points in `GunAnchors.cs` measured from mesh cross-sections. Grenades, helmets and vests:
`build_gear_models.py` (also renders their inventory icons). Characters: `prep_specops.py` (Gölge: own skeleton renamed to
Mixamo names, holstered pistol, knife and goggles bound to their bones) or `autorig.py` (Polis Timi, Komando: unrigged
models, skeleton placed from the mesh, bone-heat weights from a voxel copy), then `convert_character.py`.

| In game | File (`Models/…`) | Original | Author | License |
| --- | --- | --- | --- | --- |
| Servis 9 (pistol) | `Guns/Pistol_Servis.fbx` | [9mm Pistol](https://sketchfab.com/3d-models/9mm-pistol-43bc09f5aace4346a8a2b6e1580fc03f) | [TORI106](https://sketchfab.com/TORI106) | CC-BY 4.0 |
| Vaşak 92 (pistol) | `Guns/Pistol_Vasak.fbx` | [animated pistol](https://sketchfab.com/3d-models/animated-pistol-bd896167e7ca44f19597d3afe6a8d83f) (first-person arms left out) | [DJMaesen](https://sketchfab.com/bumstrum) | CC-BY 4.0 |
| Klasik 45 (pistol) | `Guns/Pistol_Klasik.fbx` | [Colt M1911](https://sketchfab.com/3d-models/colt-m1911-26adfd631730494ab0fc80c806eada77) | [Ole Gunnar Isager](https://sketchfab.com/FrenchBaguette) | CC-BY 4.0 |
| Magnum 50 (pistol) | `Guns/Pistol_Magnum.fbx` | [Desert Eagle](https://sketchfab.com/3d-models/desert-eagle-334462df58b942759122567fa0b81113) | [Minte Elseviers](https://sketchfab.com/Minte_Elseviers) | CC-BY 4.0 |
| Altın Magnum (pistol) | `Guns/Pistol_Retro.fbx` | [Low-Poly Desert Eagle](https://sketchfab.com/3d-models/low-poly-desert-eagle-b81da261345f4462b2c4412352162287) (untextured: coloured gold) | [TastyTony](https://sketchfab.com/TastyTony) | CC-BY 4.0 |
| Kobra (pistol) | `Guns/Pistol_Kobra.fbx` | [Pistol](https://sketchfab.com/3d-models/pistol-5f6ec54257de449cacc8c872660b40d3) | [DJMaesen](https://sketchfab.com/bumstrum) | CC-BY 4.0 |
| AK Klasik (rifle) | `Guns/Rifle_AK47.fbx` | [ak47](https://sketchfab.com/3d-models/ak47-3df7a102290140058223f5bc186d92bd) | [Pieter Ferreira](https://sketchfab.com/Badboy17Aiden) | CC-BY 4.0 |
| Akrep 9 (SMG) | `Guns/SMG_Akrep.fbx` | [HK MP5 (9mm submachine gun)](https://sketchfab.com/3d-models/hk-mp5-9mm-submachine-gun-c503d96157614fb78b5953d49e643b78) | [quick_loop](https://sketchfab.com/so_O) | CC-BY 4.0 |
| U-45 Taktik (SMG) | `Guns/SMG_U45.fbx` | [Ump.45](https://sketchfab.com/3d-models/ump45-152fc06d0f484056a3748f3c10771a1b) | [Rahul Kumar Dey](https://sketchfab.com/Elementbreeder) | CC-BY 4.0 |
| Paralı Avcı (shotgun) | `Guns/Shotgun_Avci.fbx` | [Mercenary's Shotgun](https://sketchfab.com/3d-models/mercenarys-shotgun-8db1d882a9e94f4dab8d945d737e048e) | [Katharina Alexander](https://sketchfab.com/Mondpanther) | CC-BY 4.0 |
| Çiftlik 500 (shotgun) | `Guns/Shotgun_M500.fbx` | [Mossberg500 Shotgun low-poly](https://sketchfab.com/3d-models/mossberg500-shotgun-low-poly-5ae38a34bfed4c91a3dbe78aaea51790) | [AK](https://sketchfab.com/skaf13) | CC-BY 4.0 |
| Polis 870 (shotgun) | `Guns/Shotgun_P870.fbx` | [Remington 870 Shotgun](https://sketchfab.com/3d-models/remington-870-shotgun-6db0ad4764d14eee8f063eea3600071b) | [Milin Andrei](https://sketchfab.com/milinam2002) | CC-BY 4.0 |
| Hücum 12 (shotgun) | `Guns/Shotgun_S12.fbx` | [SPAS Shotgun](https://sketchfab.com/3d-models/spas-shotgun-fea7b12a18d24b8da18d8019f82496b9) | [TORI106](https://sketchfab.com/TORI106) | CC-BY 4.0 |
| Bob (sniper) | `Guns/Sniper_Bob.fbx` | [Bob's sniper-rifle](https://sketchfab.com/3d-models/bobs-sniper-rifle-b459c3df0c5d4f2ebbe9137e04d86e24) | [denlark](https://sketchfab.com/denlark) | CC-BY 4.0 |
| Taktik DMR (sniper) | `Guns/Sniper_G28.fbx` | [HK G28 Sniper Rifle](https://sketchfab.com/3d-models/hk-g28-sniper-rifle-997840668e1e4c1384c7220563323085) | [trolosqlfod](https://sketchfab.com/trolosqlfod) | CC-BY 4.0 |
| Kar 98 (sniper) | `Guns/Sniper_K98.fbx` | [Kar98k with ZF4](https://sketchfab.com/3d-models/kar98k-with-zf4-1ba37f4c9b104fa9ba981d22ab2d08aa) (sling and loose parts left out) | [FF_Morph](https://sketchfab.com/FF_Morph) | CC-BY 4.0 |
| Nemesis (sniper) | `Guns/Sniper_Nemesis.fbx` | [Nemesis Sniper Rifle](https://sketchfab.com/3d-models/nemesis-sniper-rifle-c68434f1190f48e1902be623971b4031) | [CaptainToggle](https://sketchfab.com/CaptainToggle) | CC-BY 4.0 |
| Keskin (sniper) | `Guns/Sniper_Keskin.fbx` | [Sniper](https://sketchfab.com/3d-models/sniper-ac84ffacbbb34504a528446e241465f6) | [DJMaesen](https://sketchfab.com/bumstrum) | CC-BY 4.0 |
| SVD Avcı (sniper) | `Guns/Sniper_SVD.fbx` | [SVD (Dragunov) *Updated*](https://sketchfab.com/3d-models/svd-dragunov-updated-d4a9412275aa4974b146ad8ce9dc5fc2) | [WillyG99](https://sketchfab.com/WillyG99) | CC-BY 4.0 |
| El bombası (thrown, icon) | `Props/Grenade_M67.fbx` | [M67 Fragmentation Grenade](https://sketchfab.com/3d-models/m67-fragmentation-grenade-05e2a29118c3408eaa574bcec6997aba) | [Vextin](https://sketchfab.com/vextin) | CC-BY 4.0 |
| Sis bombası (thrown, icon) | `Props/Grenade_Smoke.fbx` | [Smoke Grenade](https://sketchfab.com/3d-models/smoke-grenade-d61768fbc7494143bb87852c36d1beaa) | [Roman Berezyak](https://sketchfab.com/yamagsummi) | CC-BY 4.0 |
| Gaz bombası (thrown, icon) | `Props/Grenade_Gas.fbx` | [M18 Smoke Grenade Green](https://sketchfab.com/3d-models/m18-smoke-grenade-green-7e7bbd1eb478426dbd9b4f2f5820f7e8) | [Hitansh 3D](https://sketchfab.com/Hitansh_3DArtist) | CC-BY 4.0 |
| Flaş bombası (thrown, icon) | `Props/Grenade_Flash.fbx` | [Zarya 2 stun grenade](https://sketchfab.com/3d-models/zarya-2-stun-grenade-f19a970e9a9a4656a8c4303282e29667) | [alpenfant](https://sketchfab.com/alpenfant) | CC-BY 4.0 |
| Taktik Kask (loot, icon) | `Props/Helmet_MICH.fbx` | [MICH 2001 military helmet](https://sketchfab.com/3d-models/mich-2001-military-helmet-ad5907514a5343e3aca5ae0357b485a3) | [HaizorWill](https://sketchfab.com/HaizorWill) | CC-BY 4.0 |
| Ağır Muharebe Kaskı (loot, icon) | `Props/Helmet_K6.fbx` | [Combat helmet K6-3](https://sketchfab.com/3d-models/combat-helmet-k6-3-94701874d8b949718708b018c8d4f61d) | [shamanoff](https://sketchfab.com/shamanoff) | CC-BY 4.0 |
| Ağır Zırh (loot, icon) | `Props/Vest_Tactical.fbx` | [Tactical Armor Vest](https://sketchfab.com/3d-models/tactical-armor-vest-60f6e1cee19c4ea39d6594d07a7505e1) | [yronthal](https://sketchfab.com/yronthal) | CC-BY 4.0 |
| Hafif Yelek, Komando Yeleği, black vest (loot, icons) | `Props/Vest_Rig.fbx`, `Vest_Olive.fbx`, `Vest_Black.fbx` | [Vest Armor Holster LowPoly GameReady Pack](https://sketchfab.com/3d-models/vest-armor-holster-lowpoly-gameready-pack-7c41d35e505c4057abe60f89537408bd) (three of the seven items) | [00amza](https://sketchfab.com/00amza) | CC-BY 4.0 |
| Gölge (character) | `Characters/FemaleSpecops.fbx` | [female specops](https://sketchfab.com/3d-models/female-specops-367a37bf5c0b48e688195d915c5496a2) | [DJMaesen](https://sketchfab.com/bumstrum) | CC-BY 4.0 |
| Polis Timi (character) | `Characters/PoliceSwat.fbx` | [Rocketbox - German Swat 3D model](https://sketchfab.com/3d-models/rocketbox-german-swat-3d-model-8787fba0fd2e4b4e9e3eadb33a606548) (from the Microsoft Rocketbox avatar library, MIT) | [Chernov-Egor](https://sketchfab.com/Chernov-Egor) | CC-BY 4.0 |
| Komando (character) | `Characters/SpecialForces.fbx` | [Special Forces](https://sketchfab.com/3d-models/special-forces-e010444792384c8ab65ca446febcd240) | [George Zhuzha](https://sketchfab.com/Zhork9) | CC-BY 4.0 |

Not used from `assets-v3`: "Battle Vest" and "Stun Grenade" (Sketchfab Standard licence: no redistribution, so not in a
public repository), "Animated MP5" (its arms are a Devil May Cry 5 asset), "Free animated Pump Shotgun" (contains a
Call of Duty part), "Bullpup Assault Rifle" (re-uploaded from a site that spreads paid assets), the "FBI" vest of the
vest pack (agency marking), and an APK file. "Tactical Plate Carrier Vest" is the armour vest the game already had.

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
