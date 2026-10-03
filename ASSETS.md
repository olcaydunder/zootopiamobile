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

## Map data (Çekmeköy, Ekşioğlu)

The battle map is the real neighbourhood around Zootopia Veteriner Kliniği (Turgut Özal Cd., Ekşioğlu, Çekmeköy/İstanbul).

| Data | Source | License |
| --- | --- | --- |
| Buildings, streets, parks, woods | © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors | ODbL 1.0 (attribution shown in the game lobby) |
| Terrain heights | [AWS Terrain Tiles](https://registry.opendata.aws/terrain-tiles/) (Mapzen; SRTM and other public sources) | see the registry page |

`.github/workflows/map-data.yml` downloads the raw data into `MapData/`; `Tools/build_map.py` bakes it into
`Assets/Resources/Map/{height,ground,features}.bytes`, which `MapData.cs` / `CityBuilder.cs` turn into the 3D town at runtime.
