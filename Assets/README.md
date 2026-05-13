# SEKTOR_03 // BIBLIOTHEK Terminal — Unity Setup

CRT-Terminal-Interface für Level 3 (Bibliothek-Puzzle) im "Escape the Matrix"-Projekt.
Implementiert mit Unity UI Toolkit (UXML + USS + C#).

---

## Voraussetzungen

- **Unity 2022.3 LTS** oder **Unity 6** (UI Toolkit Runtime muss enthalten sein)
- Optional: Monospace-Font (z.B. **VT323** oder **Share Tech Mono** von Google Fonts)
  als TTF im Projekt für den authentischen Terminal-Look

---

## Setup in 5 Schritten

### 1. Dateien einfügen

Empfohlene Struktur im Projekt:

```
Assets/
└── EscapeTheMatrix/
    └── UI/
        └── Sektor03/
            ├── Sektor03Terminal.uxml
            ├── Sektor03Terminal.uss
            └── Sektor03TerminalController.cs
```

### 2. PanelSettings erstellen

1. Rechtsklick im Project-Fenster → **Create → UI Toolkit → Panel Settings Asset**
2. Name: `Sektor03_PanelSettings`
3. Im Inspector:
   - **Theme Style Sheet**: Unity Default Runtime Theme (kommt automatisch)
   - **Scale Mode**: `Scale With Screen Size`
   - **Reference Resolution**: `1920 × 1080`
   - **Match**: `0.5`

### 3. UI-GameObject erstellen

1. In der Szene: Rechtsklick → **UI Toolkit → UI Document**
2. Auf dem entstandenen GameObject im Inspector:
   - **Source Asset**: `Sektor03Terminal.uxml` zuweisen
   - **Panel Settings**: `Sektor03_PanelSettings` zuweisen
3. **Add Component** → `Sektor03TerminalController`

### 4. Konfiguration

Im Inspector am `Sektor03TerminalController`:

- **Correct Sequence**: Die korrekte Farbsequenz (Default: RED → BLUE → YELLOW).
  Diese Lösung sollte zu den Hinweisen aus den Büchern in der Bibliothek passen.
- **Max Attempts**: `0` = unbegrenzt, sonst Anzahl der Versuche
- **Events**:
  - `OnAuthenticationSuccess` → hier z.B. die nächste Tür öffnen, Sound abspielen,
    Level-State aktualisieren
  - `OnAuthenticationFailure` → Fehler-Sound, Alarm-Animation, etc.
  - `OnAttemptsExhausted` → Game-Over oder Reset bei zu vielen Fehlversuchen

### 5. Font einbinden (optional, aber empfohlen)

Für den authentischen CRT-Look:

1. **VT323** von Google Fonts herunterladen (`VT323-Regular.ttf`)
2. Datei in `Assets/EscapeTheMatrix/UI/Sektor03/Fonts/` ablegen
3. In der `Sektor03Terminal.uss` Datei oben ergänzen:

```css
:root {
    -unity-font-definition: url('project://database/Assets/EscapeTheMatrix/UI/Sektor03/Fonts/VT323-Regular.ttf');
}
```

Ohne Font läuft alles trotzdem — es wird die System-Default-Schrift verwendet.

---

## Arduino-Integration

Statt die Farben über UI-Buttons hinzuzufügen, kann der TCS230-Color-Sensor das
direkt machen. Im Code, der den Arduino-Input liest:

```csharp
public Sektor03TerminalController terminal;

void OnColorDetected(string colorName)
{
    // colorName kommt vom Arduino (z.B. via Serial)
    if (System.Enum.TryParse<Sektor03TerminalController.ColorCode>(
        colorName.ToUpper(), out var color))
    {
        terminal.AddColor(color);
    }
}
```

Die UI-Buttons können dann optional ausgeblendet werden, indem in der UXML
die `.palette` mit `class="palette hidden"` versehen wird.

---

## Anpassen

- **Mehr/weniger Slots**: In UXML `slot-X` und `fill-X` hinzufügen/entfernen,
  im Controller das `sequence`-Array entsprechend vergrößern
- **Andere Farben**: In UXML neue `color-btn` Buttons hinzufügen, in USS Klassen
  ergänzen, in der `ColorMap` im Controller die Hex-Werte eintragen
- **Andere Story-Texte**: Direkt in der UXML im jeweiligen `text="..."` Attribut

---

## Was nicht aus dem HTML-Mockup übernommen wurde

USS kann nicht alles was CSS kann. Folgende Effekte fehlen — können bei Bedarf
nachgerüstet werden:

- **Scanlines**: Als zusätzliches `VisualElement` mit gekachelter PNG-Textur
  über dem Screen legen (`position: absolute; pointer-events: none;`)
- **Vignette / CRT-Curvature**: PNG-Overlay mit radialem Schwarz-Verlauf
- **Glow / Box-Shadow**: USS unterstützt das nicht direkt. Alternative:
  Post-Processing Bloom auf der UI-Kamera, oder mehrere überlagerte Borders
- **CRT-Flicker**: Im Controller eine Coroutine, die `screen.style.opacity`
  zufällig kurz reduziert

---

## Files

| Datei | Zweck |
|---|---|
| `Sektor03Terminal.uxml` | UI-Layout (Struktur) |
| `Sektor03Terminal.uss` | Styles (Farben, Größen, Layout) |
| `Sektor03TerminalController.cs` | Logik, Events, Animationen |
| `README.md` | Diese Datei |
