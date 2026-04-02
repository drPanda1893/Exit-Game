# ProjectPlan.md — Prison Break Exit-Game

**Genre:** Top-Down 3D Puzzle / Escape Room  
**Engine:** Unity (C#) + Arduino Hardware Integration  
**Team:** 5 Claude Instances coordinated via CLAUDE.md  
**Status:** Milestone 1 — Core Loop Implemented, Gaps Remaining

---

## Story Summary

Big Yahu sitzt im Gefängnis. Mit Hilfe des Spielers kämpft er sich durch 6 Räume —
von der Gefängniszelle bis zum letzten Tor. Jeder Raum ist ein eigenständiges Rätsel,
das entweder durch Logik, Beobachtung oder physische Arduino-Hardware gelöst wird.

---

## Level Overview

### Level 1 — Zelle (Prison Cell)
**Scene:** `Level1.unity`  
**Status:** Implementiert (Script vorhanden, Scene gebaut)

| Element | Detail |
|---|---|
| Einstieg | Spieler startet eingesperrt in der Zelle |
| Hinweis | Auf der **Bettdecke** steht `66A` (Hex-Code) |
| Dekodierung | `66A` hex = `1642` dezimal → Lösung des Numpad-Rätsels |
| Interaktion | Spieler nähert sich der **Toilette** → Hint-Text erscheint unten: `"Drücke [E] um das Numpad zu öffnen"` |
| Puzzle | Numpad öffnet sich (E-Taste), Eingabe `1642` → Tür öffnet sich |
| Ausgang | Rechte Wand öffnet sich → Übergang zu Level 2 |
| NPC | Big Yahu: Hinweis-Dialog beim Betreten |

**Offene Aufgaben:**
- [ ] G-01: In-World Bettdecke mit Hex-Text `66A` als 3D-Objekt / Decal platzieren
- [ ] `CellPuzzleHandler.cs` mit `NumpadController.cs` verbinden (Lösung validieren)

---

### Level 2 — Abstellraum (Storage Room)
**Scene:** `Level2.unity`  
**Status:** Scene vorhanden, Script teilweise implementiert — GDD-Lücken kritisch

| Element | Detail |
|---|---|
| NPC | **Joshi** sitzt im Raum und erzählt Audio-Geschichte seines Ausbruchs |
| Trigger | Joshi erwähnt „staubige Wand" → Spieler muss Staub von Wand entfernen |
| Arduino (L2a) | **Humidity-Sensor**: Spieler pustet physisch → Sensor registriert Luftfeuchtigkeit → Staub verschwindet in Unity |
| Fallback | Maus-Scratch-Mechanic (bereits implementiert in `Level2_DustWall.cs`) |
| Enthüllung | Pfeilkombination `↑ ↑ ↓ ↓` erscheint auf der Wand |
| Arduino (L2b) | **Joystick**: Spieler gibt `↑ ↑ ↓ ↓` über physischen Joystick ein |
| Fallback | Pfeiltasten auf der Tastatur |
| Ausgang | Korrekte Kombination → Tür zum nächsten Raum öffnet sich |

**Offene Aufgaben:**
- [ ] G-02: Dialog-System auf Joshi NPC umstellen (eigener Speaker-Name + Portrait)
- [ ] G-03: Neues Puzzle-Script `Level2_ArrowCombo.cs` — Pfeilsequenz `↑↑↓↓` mit Arduino-Joystick-Input
- [ ] Arduino-Befehl `0x11` (Joystick) und `0x10` (Humidity) in `ArduinoBridge.cs` implementieren
- [ ] Joshi Audio-Clips (MP3/WAV) aufnehmen / einfügen

---

### Level 3 — Knastbibliothek (Prison Library)
**Scene:** `GameLevel.unity` (Panel 0)  
**Status:** Script `Level3_ColorCode.cs` implementiert, GDD-Mechaniken fehlen

| Element | Detail |
|---|---|
| NPC | **Helios** steht hinter dem Bibliotheksregister |
| Interaktion | Helios bietet Bücherauswahl an → Spieler **muss die Bibel wählen** |
| Falsche Wahl | Helios: „Das ist nicht das richtige Buch." → kein Fortschritt |
| Enthüllung | Bibel aufschlagen → Farbsequenz sichtbar: `Rot → Blau → Gelb → Grün` |
| Arduino (L3) | **Farbsensor**: Spieler hält physische Farbkarten nacheinander vor den Sensor |
| Fallback | 4 farbige UI-Buttons (bereits implementiert) |
| Ergebnis | Korrekte Sequenz → Generator startet, Computer hochfahren |
| Ausgang | Aktivierter Computer → Level 4 |

**Offene Aufgaben:**
- [ ] G-04: Bücherauswahl-UI bauen (3+ Bücher, nur Bibel löst Puzzle aus)
- [ ] G-04: Helios NPC in Dialog-System integrieren (separater Speaker-Tag)
- [ ] G-05: Arduino-Befehl `0x20` (Farbsensor) in `ArduinoBridge.cs`
- [ ] Helios Asset beschaffen oder Big-Yahu-Rig wiederverwenden

---

### Level 4 — Computer-Minigame (Stealth Run)
**Scene:** `GameLevel.unity` (Panel 1)  
**Status:** Vollständig implementiert (`Level4_StealthMinigame.cs`)

| Element | Detail |
|---|---|
| Gameplay | Top-Down UI-Minigame: Spieler-Icon navigiert per WASD durch Spielfeld |
| Gegner | Wärter-Blöcke patrouillieren (alternierend horizontal/vertikal) |
| Ziel | „Schuppen"-Icon in der Ecke erreichen |
| Fail | Kollision mit Wärter → Reset + Big-Yahu-Dialog |
| Ausgang | Schuppen erreicht → Level 5 |

**Offene Aufgaben:**
- [ ] Wärter-Anzahl und Patrol-Geschwindigkeit balancen (Playtesting)
- [ ] Optional: Sichtlinien-Kegel statt Rechteck-Kollision für mehr Realismus

---

### Level 5 — Werkstatt (Workshop / Breadboard)
**Scene:** `GameLevel.unity` (Panel 2)  
**Status:** Script `Level5_Breadboard.cs` implementiert (Drag & Drop), Arduino-Seite fehlt

| Element | Detail |
|---|---|
| Kontext | Eingabefeld des Schuppens ist kaputt — Spieler muss Schaltung bauen |
| Puzzle | Kabel-Drag-&-Drop auf korrekten Breadboard-Pins |
| Arduino (L5) | **Physisches Breadboard**: Spieler steckt echte Kabel → Unity liest Schaltungsstatus |
| Fallback | Drag & Drop UI (bereits implementiert) |
| Ausgang | Alle Verbindungen korrekt → Tor-Mechanismus öffnet sich → Level 6 |

**Offene Aufgaben:**
- [ ] Arduino-Befehl `0x50` (Breadboard-State-Bitmap) implementieren
- [ ] UI-Breadboard-Layout mit korrekter Beschriftung (A/B/C Pin-IDs) erstellen
- [ ] `requiredPairs` in Inspector für die Ziel-Schaltung konfigurieren

---

### Level 6 — Gefängnistor (Final Gate)
**Scene:** `GameLevel.unity` (Panel 3)  
**Status:** Script `Level6_FinalGate.cs` implementiert, Arduino + Metapher-Update offen

| Element | Detail |
|---|---|
| Kontext | Spieler steht vor dem letzten Gefängnistor |
| Werkzeug | **Bunsenbrenner / Föhn** wird ans Tor gehalten |
| Arduino (L6) | **Wärmesensor (z.B. DHT22 / Thermistor)**: Föhn aufheizen → Sensor misst Temperatur → Unity-Progressbar steigt |
| Fallback | Maus-gedrückt-halten auf „Erhitzen"-Button (bereits implementiert) |
| Mechanic | Loslassen → Temperatur fällt ab. Bei 100% → Schloss bricht → Win |
| Ausgang | Spiel gewonnen — Big Yahu Outro-Dialog |

**Offene Aufgaben:**
- [ ] G-06: UI auf Bunsenbrenner-Metapher updaten (Icon, Beschriftung)
- [ ] Arduino-Befehl `0x60` (Temperaturdaten) in `ArduinoBridge.cs` verarbeiten
- [ ] Win-Screen / Credits Scene bauen

---

## Arduino Integration Roadmap

```
ArduinoBridge.cs (Singleton, Background Thread)
├── Connect(portName, baudRate)
├── Disconnect()
├── RegisterHandler(byte cmdId, Action<string> handler)
├── Send(byte cmdId, string data)
└── [Internal] ReadLoop() → ThreadSafeDispatch → main thread callbacks
```

**Priorität:**
1. `ArduinoBridge.cs` Grundgerüst (kein Level-spezifischer Code)
2. L2 Humidity + Joystick (Milestone 2)
3. L3 Farbsensor (Milestone 2)
4. L5 Breadboard (Milestone 3)
5. L6 Wärmesensor (Milestone 3)

---

## Milestone Plan

| Milestone | Ziel | Status |
|---|---|---|
| **M1** | Alle 6 Level-Scripts, Szenen-Struktur, GameManager, Dialog-System | ✅ Abgeschlossen |
| **M2** | GDD-Lücken G-01 bis G-04 schließen, Joshi/Helios NPCs, Arrow-Combo Puzzle | 🔄 In Arbeit |
| **M3** | `ArduinoBridge.cs` + L2/L3 Arduino-Integration + Fallback-Tests | ⏳ Offen |
| **M4** | L5/L6 Arduino-Integration + Balancing + Win-Screen | ⏳ Offen |
| **M5** | Vollständiger Playtestlauf, Performance-Profiling, Bug-Fixing, Abgabe | ⏳ Offen |

---

## Asset Inventory

| Asset | Pfad | Status |
|---|---|---|
| Big Yahu Model (Standing) | `Assets/Big Yahu/Big Yahu standing.fbx` | ✅ |
| Big Yahu Model (Jogging) | `Assets/Big Yahu/Big Yahu jogging.fbx` | ✅ |
| Joshi NPC Model | `Assets/Big Yahu/Joshi NPC.fbx` | ✅ |
| Joshi Animator | `Assets/Big Yahu/Joshi_Sitting.controller` | ✅ |
| Helios NPC Model | — | ❌ Fehlt |
| Joshi Audio-Clips | — | ❌ Fehlt |
| Bunsenbrenner Icon | — | ❌ Fehlt |
| Bettdecken-Hex-Decal | — | ❌ Fehlt |

---

## Critical Path (Next Steps)

1. **G-01** — Hex-Decal `66A` auf Bettdecke in Level1.unity platzieren
2. **G-02/G-03** — Joshi NPC Dialog + Arrow-Combo Puzzle (`Level2_ArrowCombo.cs`)
3. **ArduinoBridge.cs** — Grundgerüst mit thread-safe dispatch
4. **G-04** — Helios + Bücherauswahl in Level 3
5. **Playtesting** — M2 intern testen, Feedback einarbeiten
