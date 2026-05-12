# CLAUDE.md — Exit-Game Repository Configuration

> Mandatory operating rules for all Claude instances working in this repository.
> Read this file completely before touching any code, asset, or configuration.

---

## 1. Role System (Virtual Agent Team)

Every session operates as a coordinated team. Assign roles internally based on the task:

| Role | Responsibility |
|---|---|
| **Architect** | Scene hierarchy, data flow, system design decisions |
| **Unity-Expert** | MonoBehaviour lifecycle, physics, UI, animation, Input System |
| **Arduino-Bridge** | Serial communication layer, sensor protocol, async data handling |
| **QA-Engineer** | Unity Edit/Play Mode tests, regression checks, performance profiling |
| **Head of Development** | Coordinates all roles; owns Git commits and final review |

---

## 2. Git Workflow (Non-Negotiable)

```
git pull                        # ALWAYS before starting work
# ... implement ...
git add <specific-files>        # Never `git add -A` — avoid accidental .env / binary commits
git commit -m "type(scope): description\n\nCo-Authored-By: Claude Sonnet 4.6 <noreply@anthropic.com>"
git push
```

**Commit message format:** `type(scope): short description`
Types: `feat`, `fix`, `refactor`, `test`, `docs`, `chore`
Scopes: `Level1`–`Level6`, `Arduino`, `GameManager`, `UI`, `NPC`, `Editor`, `Build`

Every completed task gets its own commit. No squash-and-forget.

---

## 3. Project Architecture Overview

### Tech Stack
- **Engine:** Unity (Top-Down 3D, Input System package)
- **UI:** TextMeshPro + Unity UI (Canvas-based level panels)
- **Hardware:** Arduino via `System.IO.Ports.SerialPort` (async thread)
- **Scripting:** C# (.NET, Unity 2022+)

### Scene Structure
```
Assets/Scenes/
  Level1.unity      — Prison Cell (3D world, PlayerController, ToiletInteraction)
  Level2.unity      — Storage Room (3D world, Joshi NPC, DustWall)
  GameLevel.unity   — Levels 3-6 as UI panels (GameManager panel-switching)
  SampleScene.unity — Sandbox / testing only
```

### Core Singletons
| Class | Purpose |
|---|---|
| `GameManager` | Level progression, panel activation, `CompleteCurrentLevel()` |
| `BigYahuDialogSystem` | Typewriter dialog queue, portrait, audio, callback chain |

### Level → Script Mapping
| Level | Scene / Panel | Script | Arduino Hook |
|---|---|---|---|
| 1 – Zelle | Level1.unity | `Level1_Cell.cs` + `ToiletInteraction.cs` + `NumpadController.cs` | — |
| 2 – Abstellraum | Level2.unity | `Level2_DustWall.cs` | Humidity sensor (blow detection) |
| 3 – Bibliothek | GameLevel panel | `Level3_ColorCode.cs` | Color sensor (sequence scan) |
| 4 – Computer | GameLevel panel | `Level4_StealthMinigame.cs` | — |
| 5 – Werkstatt | GameLevel panel | `Level5_Breadboard.cs` | Physical breadboard I/O |
| 6 – Tor | GameLevel panel | `Level6_FinalGate.cs` | Heat / temperature sensor |

---

## 4. Coding Standards

### DRY & Modularity
- One responsibility per MonoBehaviour. No God-objects.
- Shared logic → extract to a static utility or base class.
- Event-driven communication preferred over direct references (`Action`, `UnityEvent`).

### Performance
- **No polling in `Update()`** unless absolutely necessary. Use events, coroutines, or `OnTrigger*`.
- Arduino data must be read on a **dedicated background thread**; dispatch to main thread via a thread-safe queue or `MainThreadDispatcher`.
- Avoid `GetComponent<>()` every frame — cache in `Awake()` / `Start()`.
- O(n) maximum for puzzle validation loops; n is always small (≤ 10).

### Arduino Bridge Rules
```csharp
// DO: background thread reads, main-thread queue dispatches
// DO: timeout + reconnect logic
// DO: prefix all serial messages with a 1-byte command ID
// DON'T: SerialPort.Read() on the Unity main thread
// DON'T: block Update() waiting for hardware response
```

### Naming Conventions
```
Scripts/              PascalCase, suffix by type: Controller, Manager, Handler, System
Level scripts:        Level{N}_{Description}.cs
Editor scripts:       Assets/Scripts/Editor/
Arduino bridge:       Assets/Scripts/Arduino/ArduinoBridge.cs (singleton)
```

---

## 5. Test-Driven Development

Every logic component needs a corresponding test. No exceptions.

```
Assets/Tests/EditMode/   — Pure logic (puzzle validation, code parsing)
Assets/Tests/PlayMode/   — Coroutine flows, scene transitions, UI interaction
```

Minimum test coverage per level:
- Correct solution → `CompleteCurrentLevel()` is called
- Wrong solution → state resets, no level advance
- Arduino disconnect → graceful degradation (soft fallback to keyboard/mouse)

---

## 6. Arduino Integration Protocol

### Serial Message Format
```
Direction  Format           Example
PC → Ard   <CMD_ID><data>\n  0x01 "TRIGGER\n"
Ard → PC   <CMD_ID><data>\n  0x10 "HUMIDITY:72\n"
```

### Command IDs (reserved)
| ID | Meaning |
|---|---|
| `0x01` | Ping / keep-alive |
| `0x10` | Humidity sensor reading (L2) |
| `0x11` | Joystick direction event (L2) |
| `0x20` | Color sensor reading (L3) |
| `0x50` | Breadboard circuit state (L5) |
| `0x60` | Temperature sensor reading (L6) |

### Fallback Policy
Every Arduino-gated puzzle **must** have a keyboard/mouse fallback enabled via `[SerializeField] private bool arduinoFallback = true;` in the Inspector. This ensures the game is always playable without hardware.

---

## 7. NPC System

| NPC | Asset | Levels | Speaker Tag |
|---|---|---|---|
| Big Yahu | `Assets/Big Yahu/Big Yahu standing.fbx` | All | "Big Yahu" |
| Joshi | `Assets/Big Yahu/Joshi NPC.fbx` | Level 2 | "Joshi" |
| Helios | TBD | Level 3 | "Helios" |

Dialog is routed through `BigYahuDialogSystem`. For non-Big-Yahu NPCs, set `speakerLabel` and `portraitImage` dynamically before calling `ShowDialog()`.

---

## 8. Known GDD ↔ Implementation Gaps (Must Fix)

| # | GDD Requirement | Current State | Priority |
|---|---|---|---|
| G-01 | L1: Hex code `662` visible on blanket → decode to `1634` | Code correct (1634), but no in-world blanket hint object | HIGH |
| G-02 | L2: Joshi NPC audio narration, not Big Yahu | `Level2_DustWall.cs` uses `BigYahuDialogSystem` | HIGH |
| G-03 | L2: Arrow combo `↑↑↓↓` via joystick opens door | Current code reveals a hidden number; no directional puzzle | HIGH |
| G-04 | L3: Helios NPC + book selection (must pick Bible) | ✅ `HeliosInteraction` + `BookSelectionUI` (Level3.unity) | DONE |
| G-05 | L3: Color sequence via Arduino (hold sensor to code) | ✅ `Level3_ColorCodeUI` futuristisches Login-Terminal (3-stelliger Code GRÜN→BLAU→GRÜN, Farben ROT/BLAU/GRÜN), Live-Scannerwerte (`COLOR:RGB:...`), mouse fallback. Arduino: TCS3200 (S0=D4,S1=D5,S2=D6,S3=D7,OUT=D12,OE=D13) als `LV_COLOR` (Cmd `20:START/STOP`), sendet `COLOR:RED/GREEN/BLUE` nur bei echter Farbänderung + `COLOR:RESET` (Taster D8) | DONE |
| G-06 | L6: Bunsenbrenner / Föhn metaphor | Generic "heat button" UI | LOW |

---

## 9. File Hygiene

Periodically scan for dead files:
- `Assets/Scenes/SampleScene.unity` — delete when no longer needed for testing
- `Assets/Scripts/Editor/BuildLevel*.cs` — keep if used in CI; remove otherwise
- `Assets/Big Yahu/BigYahu_Run.controller` vs `BigYahuAnimator.controller` — consolidate to one

---

## 10. Definition of Done

A task is **done** when:
1. Feature works in Play Mode (manual test)
2. Unit/Play tests pass
3. No new compiler warnings
4. `git commit` pushed with correct message format
5. GDD gap closed (if applicable — update table in Section 8)
