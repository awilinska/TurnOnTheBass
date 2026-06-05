# Fishing Prototype

## What this includes
- WASD top-down movement.
- Water zones (`Ocean`, `Lake`, `River`) with unique fish pools.
- Press `E` or `F` near water to start fishing.
- A short bite delay followed by a Canvas rhythm minigame.
- Catch result quality and size are based on minigame accuracy.

## Runtime bootstrap
- `FishingPrototypeBootstrap` auto-builds a test scene at runtime if no `FishingGameController` exists:
  - camera
  - player
  - demo water zones
  - fishing controller + HUD

This means `SampleScene` can stay empty and still be playable in Play Mode.

## Manual scene setup
If you place `FishingGameController` in the scene, runtime bootstrap will not auto-create gameplay objects.

Required objects:
1. Player object with:
   - `TopDownPlayerController`
   - `Rigidbody2D` (`Gravity Scale = 0`, freeze Z rotation)
   - non-trigger `Collider2D`
   - `PlayerWaterDetector`
2. Water objects, each with:
   - trigger `Collider2D`
   - `WaterZone` (set `WaterType` and `ZoneName`)
3. Camera:
   - `MainCamera` tag
   - orthographic camera

In `FishingGameController` inspector:
- Assign `Player`.
- Assign `Water Detector`, or leave empty and enable `Auto Find Player If Missing`.
- Assign `Rhythm Minigame` to the `CanvasRhythmGame` component under the Canvas.
- Keep `Use Default Fish Catalog` enabled unless you inject your own catalog in code.
- Tune `Fishing Timing` to control bite delay.
- Tune `Minigame Result` to control the required accuracy for catching the fish.

In `CanvasRhythmGame` inspector:
- Assign `Play Area`, `Note Parent`, and `Target Note`.
- Assign note prefabs to the Green, Blue, Black, and White lanes.
- The script calculates lane bottom spawn points automatically and moves notes directly toward the target note on angled paths.
- The rhythm panel can stay inactive by default; fishing activates it when a fish is hooked.
- Temporary keyboard controls are `A/S/D/F` for Green/Blue/Black/White.
- During the note sequence, spin prompts pause new note spawning, show `SpinText`, pulse the target image, and require spamming `R`.
- Each `R` tap currently counts as one spin within the active prompt and updates the label as `SPIN x1`, `SPIN x2`, and so on. The text hides when the prompt ends and resets on the next prompt.
- `AccuracyText` and `NoteHitText` can be assigned as TextMeshProUGUI fields.
- Hit labels are inspector strings: `PERFECT`, `GOOD`, `MEH`, and `MISSED` by default.
- Tune lane spacing, note speed, spawn interval, hit windows, note count, spin presses, pulse speed, pulse scale, and score values in the inspector.

## Custom controller
- `PicoSerialRhythmInput` reads the Raspberry Pi Pico serial output at `9600` baud.
- Set `Port Name` to the Pico COM port, for example `COM3`.
- `BUTTON_1..4 pressed` trigger the Green, Blue, Black, and White lanes.
- `OBROT:` messages trigger the spin/reel input while a spin prompt is active.
- Enable `Log Serial Lines` temporarily to verify incoming messages in the Unity Console.

## Core scripts
- `Assets/Scripts/Bootstrap/FishingPrototypeBootstrap.cs`
- `Assets/Scripts/Player/TopDownPlayerController.cs`
- `Assets/Scripts/Fishing/FishingGameController.cs`
- `Assets/Scripts/Fishing/FishCatalog.cs`
- `Assets/Scripts/Rhythm/CanvasRhythmGame.cs`
