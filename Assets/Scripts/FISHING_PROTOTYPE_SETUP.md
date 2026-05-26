# Fishing Rhythm Prototype

## What this includes
- WASD top-down movement.
- Water zones (`Ocean`, `Lake`, `River`) with unique fish pools.
- Press `E` or `F` near water to start fishing.
- Hook phase, then a 4-lane rhythm minigame (`A/S/D/F`).
- Minigame score controls fish catch result:
  - success/fail (fish caught or escaped)
  - quality grade
  - final fish size

## Runtime bootstrap
- `FishingPrototypeBootstrap` auto-builds a test scene at runtime if no `FishingGameController` exists:
  - camera
  - player
  - demo water zones
  - fishing controller + HUD

This means `SampleScene` can stay empty and still be playable in Play Mode.

## Manual scene setup (when you add FishingGameController yourself)
If you place `FishingGameController` in the scene, runtime bootstrap will not auto-create gameplay objects.

Required objects:
1. Player object with:
   - `TopDownPlayerController`
   - `Rigidbody2D` (`Gravity Scale = 0`, freeze Z rotation)
   - non-trigger `Collider2D`
   - `PlayerWaterDetector`
2. Water objects (ocean/lake/river), each with:
   - trigger `Collider2D`
   - `WaterZone` (set `WaterType` and `ZoneName`)
3. Camera:
   - `MainCamera` tag
   - orthographic camera

In `FishingGameController` inspector:
- Assign `Player`
- Assign `Water Detector` (or leave empty and enable `Auto Find Player If Missing`)
- Keep `Use Default Fish Catalog` enabled unless you will inject your own catalog in code.
- Tune gameplay directly from inspector:
  - `Fishing Timing` (bite delay and `rhythmStartCountdownSeconds`)
  - `Rhythm Gameplay` (speed, density, hit windows, accuracy target)
  - `Rhythm UI` (fullscreen board size/layout and background tint)
  - `Diva Layout` (target spread, hit radius, center core size)
  - `Diva Motion` (preview lead and movement depth curve)
  - `Diva Notes` (`Rectangle` or `Circle`, note width/diameter)

## Song setup per fish
Each fish has a unique `songResourceName` in `FishCatalog`.

To add songs:
1. Create folder: `Assets/Resources/Songs`
2. Add audio clips with matching names, for example:
   - `ocean_tuna.wav`
   - `lake_bass.wav`
   - `river_salmon.wav`
3. Clips will be loaded automatically with `Resources.Load<AudioClip>("Songs/<name>")`.

If a clip is missing, the rhythm chart still works without audio.

## Core scripts
- `Assets/Scripts/Bootstrap/FishingPrototypeBootstrap.cs`
- `Assets/Scripts/Player/TopDownPlayerController.cs`
- `Assets/Scripts/Fishing/FishingGameController.cs`
- `Assets/Scripts/Fishing/FishCatalog.cs`
- `Assets/Scripts/Rhythm/RhythmMinigame.cs`
