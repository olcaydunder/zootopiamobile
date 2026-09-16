# Battle Island Mobile

A complete original 3D mobile battle-royale prototype built in Unity using original, non-copyrighted gameplay systems and procedural placeholder art.

## Included features
- Third-person shooter gameplay
- Large island battleground with safe-zone shrink
- Solo, duo, and squad tournament flow
- Enemy bots with basic AI
- Multiple weapons with unique stats
- Health, armor, ammo, and inventory management
- Loot and supply crates
- Lobby, matchmaking, and round start
- Leveling, XP, coins, and cosmetic unlocks
- Mobile touch controls and settings
- Restart / respawn flow
- Optimized low-end Android-friendly gameplay

## Requirements
- Unity Hub
- Unity 2022.3 LTS or newer
- Android Build Support if exporting to APK/AAB

## Run in Unity
1. Open Unity Hub.
2. Add the repository folder as a project.
3. Use Unity 2022.3 LTS.
4. Create a new empty scene named `Main`.
5. In the scene, create an empty GameObject called `GameBootstrap`.
6. Attach `Assets/Scripts/Game/GameBootstrap.cs` to it.
7. Press Play.
8. The lobby menu should appear, and you can begin a match.

## Controls
Keyboard:
- W A S D = move
- Shift = sprint
- Space = jump
- Ctrl = crouch
- Left mouse = fire
- R = reload
- X = use medkit
- Tab = pause / open settings (if expanded)

Mobile:
- Left virtual thumbstick = movement
- Right virtual thumbstick = aim / fire
- Buttons = sprint, crouch, jump, reload

## Project structure
- `Assets/Scripts/Game/` — gameplay systems
- `Assets/Scenes/` — scene setup and notes
- `README.md` — this run guide

## Notes
This is a fully playable prototype with procedural world generation and original gameplay logic, but it uses generated geometry rather than copyrighted external assets. It is intended as a working base for expansion into a larger production game.
