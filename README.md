# Hadal Works

An original undersea factory-automation game for Windows, built in Unity 6.

Build an automated factory on the ocean floor. Tap thermal vents for power, pipe ferrite, silica and
glowkelp through pressure tubes into processors and fabricators, and make hull panels and viewports rated
for deeper water. Richer resources sit deeper, so the factory grows downward in tiers.

Milestone 1, "First Light", is Tier 1 (The Shelf) as a gray box: plain shapes on purpose, so the factory
loop gets fun before art.

## Layout
- `Assets/HadalWorks/Scripts/Sim` - the rules engine, pure C# (no Unity), unit tested.
- `Assets/HadalWorks/Scripts/Game` - the Unity side: seabed, machines, tubes, build tools, camera, HUD. Builds itself at runtime.
- `Assets/HadalWorks/Tests/EditMode` - NUnit tests for the rules.
- `Assets/HadalWorks/Editor/HadalWorksBuild.cs` - setup and Windows build entry points.
- `tools/simcheck` - the same tests plus a demo-factory timing check, without Unity (`dotnet test`, `dotnet run`).

## Build (Windows, from the repo folder)
```
"C:\Program Files\Unity\Hub\Editor\<version>\Editor\Unity.exe" -batchmode -quit -projectPath . -executeMethod HadalWorks.EditorTools.HadalWorksBuild.BuildWindows -logFile build.log
```
Autopilot screenshot run (builds the demo factory, plays to the Tier 2 goal, saves screenshots, quits):
```
Builds\HadalWorks-0.1.0\HadalWorks.exe -hwshots C:\path\to\shots -screen-width 1920 -screen-height 1080 -screen-fullscreen 0
```

## Controls
1-5 pick a building (tube, extractor, processor, fabricator, vent tap), R turns it (yellow side = output),
click to place. Tubes: click and drag. X demolish. Click a building to inspect it and pick its recipe.
Space pause, F fast, WASD / screen edge pan, scroll zoom, Q/E rotate, middle-drag orbit, Esc menu.
