# Kilnwood

An original land-based factory-automation game for Windows, built in Unity 6.

Build an automated factory in a wild river valley. Woodcutters feed belts, kilns bake wood into charcoal,
and fire runs everything: mines, brick kilns and workshops burn wood or charcoal, and smelters eat charcoal
to make iron. Each tier brings a new power source (Tier 2 water, Tier 3 steam).

Milestone 1, "First Fire", is Tier 1 as a gray box: plain shapes on purpose, so the factory loop gets fun
before art. Goal: deliver 100 gears and 120 bricks to the lodge for the first waterwheel.

## Layout
- `Assets/Kilnwood/Scripts/Sim` - the rules engine, pure C# (no Unity), unit tested.
- `Assets/Kilnwood/Scripts/Game` - the Unity side: valley, machines, belts, build tools, camera, HUD. Builds itself at runtime.
- `Assets/Kilnwood/Tests/EditMode` - NUnit tests for the rules.
- `Assets/Kilnwood/Editor/KilnwoodBuild.cs` - setup and Windows build entry points.
- `tools/simcheck` - the same tests plus a demo-factory timing check, without Unity (`dotnet test`, `dotnet run`).

## Build (Windows, from the repo folder)
```
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod Kilnwood.EditorTools.KilnwoodBuild.BuildWindows -logFile build.log
```
Autopilot screenshot run (builds the demo factory, plays to the Tier 2 goal, saves screenshots, quits):
```
Builds\Kilnwood-0.2.1\Kilnwood.exe -kwshots C:\path\to\shots -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```

## Controls
1-6 pick a building (belt, woodcutter, mine, kiln, smelter, workshop), R turns it (yellow side = output),
click to place. Belts: click and drag. X demolish. Click a building to inspect it and pick its recipe.
Space pause, F fast, WASD / screen edge pan, scroll zoom, Q/E rotate, middle-drag orbit, Esc menu.
