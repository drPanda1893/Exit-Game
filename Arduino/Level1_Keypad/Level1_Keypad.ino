/*
 * Big Yahu – Level 1 Keypad (Freenove Projects Kit FNK0059 / Control Board V4)
 *
 * Pin-Belegung laut Freenove FNK0059-Dokumentation:
 *   D2=Row1  D3=Row2  D4=Row3  D5=Row4
 *   D6=Col1  D7=Col2  D8=Col3  D9=Col4
 *
 * Board-Konflikte (Onboard-Komponenten):
 *   D4 = Relay  (aktiv HIGH) liegt auf Row3
 *   D8 = Buzzer (aktiv HIGH) liegt auf Col3
 *
 * Lösung – Custom-Scanner ohne Keypad-Library:
 *   Alle Spalten werden als OUTPUT LOW gehalten.
 *   Zum Scannen einer Spalte: kurz OUTPUT HIGH (~5 µs), dann sofort zurück LOW.
 *   D8 ist also nur während des Col3-Scans für ~5 µs HIGH → kein hörbares Piepen.
 *   Zeilen werden vor jedem Lesen auf 0 V entladen → Relay bleibt inaktiv.
 *   Einheitlicher 80 ms-Piepton nur bei erkannter Taste.
 *
 * Protokoll eingehend  (Unity → Arduino):
 *   "FF:START\n"  → Scan starten (Numpad geöffnet)
 *   "FF:STOP\n"   → Scan stoppen (Numpad geschlossen)
 *
 * Protokoll ausgehend  (Arduino → Unity):
 *   "05:0"…"05:9"  Ziffer
 *   "05:DEL"        * gedrückt
 *   "05:ENT"        # gedrückt
 *   "RDY"           Board bereit
 */

#define BUZZER_PIN  8    // D8 – aktiver Buzzer (= Col3, wird per Custom-Scan kurz gehalten)
#define RELAY_PIN   4    // D4 – Relay          (= Row3, wird entladen vor jedem Lesen)
#define BEEP_MS     80   // Einheitliche Piepton-Länge in ms
#define DEBOUNCE_MS 30   // Entprellzeit in ms

const byte ROWS = 4;
const byte COLS = 4;

char keys[ROWS][COLS] = {
  { '1', '2', '3', 'A' },
  { '4', '5', '6', 'B' },
  { '7', '8', '9', 'C' },
  { '*', '0', '#', 'D' }
};

byte rowPins[ROWS] = { 2, 3, RELAY_PIN,  5 };
byte colPins[COLS] = { 6, 7, BUZZER_PIN, 9 };

bool   scanning  = false;
String serialBuf = "";

// Debounce-Zustand
static char          _lastRaw   = 0;
static char          _stableKey = 0;
static unsigned long _firstSeen = 0;

// ── D8 + D4 sicher auf OUTPUT LOW ────────────────────────────────────────────
void silenceBoard()
{
  pinMode(BUZZER_PIN, OUTPUT); digitalWrite(BUZZER_PIN, LOW);
  pinMode(RELAY_PIN,  OUTPUT); digitalWrite(RELAY_PIN,  LOW);
}

// ── Einheitlicher Piepton ────────────────────────────────────────────────────
void beep()
{
  digitalWrite(BUZZER_PIN, HIGH);
  delay(BEEP_MS);
  digitalWrite(BUZZER_PIN, LOW);
}

// ── Low-level Scan ───────────────────────────────────────────────────────────
// Keine INPUT_PULLUP-Restore → D8 bleibt fast immer LOW.
// Zeilen werden vor dem Lesen auf 0 V entladen (OUTPUT LOW → INPUT) damit
// D4 (Relay) nicht durch schwebende Spannung anzieht.
char rawScan()
{
  // Alle Spalten auf OUTPUT LOW (D8 = LOW = Buzzer aus)
  for (byte c = 0; c < COLS; c++) {
    pinMode(colPins[c], OUTPUT);
    digitalWrite(colPins[c], LOW);
  }

  for (byte c = 0; c < COLS; c++) {
    digitalWrite(colPins[c], HIGH);  // Spalte kurz anwählen
    delayMicroseconds(5);            // Einschwingen

    for (byte r = 0; r < ROWS; r++) {
      // Zeile auf 0 V entladen → definierter Startzustand
      pinMode(rowPins[r], OUTPUT);
      digitalWrite(rowPins[r], LOW);
      delayMicroseconds(2);
      // Jetzt als Eingang: Taste gedrückt → Spalte (HIGH) zieht Zeile hoch
      pinMode(rowPins[r], INPUT);
      delayMicroseconds(5);

      if (digitalRead(rowPins[r]) == HIGH) {
        digitalWrite(colPins[c], LOW);  // Spalte sofort wieder LOW
        silenceBoard();
        return keys[r][c];
      }
    }

    digitalWrite(colPins[c], LOW);  // Spalte abwählen
  }

  silenceBoard();
  return 0;
}

// ── Entprellter Tastendruck ──────────────────────────────────────────────────
// Meldet jede Taste genau einmal beim ersten stabilen Erkennen nach DEBOUNCE_MS.
char getKey()
{
  char raw = rawScan();
  unsigned long now = millis();

  if (raw != _lastRaw) {
    _lastRaw   = raw;
    _firstSeen = now;
    _stableKey = 0;
    return 0;
  }

  if (raw && !_stableKey && (now - _firstSeen >= DEBOUNCE_MS)) {
    _stableKey = raw;
    return raw;
  }

  if (!raw) _stableKey = 0;

  return 0;
}

// ── Setup ────────────────────────────────────────────────────────────────────
void setup()
{
  silenceBoard();
  Serial.begin(115200);
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);
  delay(1500);
  silenceBoard();
  Serial.println("RDY");
}

// ── Unity-Befehle lesen ──────────────────────────────────────────────────────
void handleSerial()
{
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n') {
      serialBuf.trim();
      if (serialBuf == "FF:START") { scanning = true;  }
      if (serialBuf == "FF:STOP")  { scanning = false; silenceBoard(); }
      serialBuf = "";
    } else if (serialBuf.length() < 32) {
      serialBuf += c;
    }
  }
}

// ── Loop ─────────────────────────────────────────────────────────────────────
void loop()
{
  handleSerial();

  if (!scanning) {
    silenceBoard();
    return;
  }

  char key = getKey();
  if (!key) return;

  // Einheitlicher Piepton + LED-Feedback bei jeder Taste
  beep();
  digitalWrite(LED_BUILTIN, HIGH);
  delay(30);
  digitalWrite(LED_BUILTIN, LOW);

  // Senden
  if (key >= '0' && key <= '9') {
    Serial.print("05:"); Serial.println(key);
  } else if (key == '*') { Serial.println("05:DEL"); }
  else if (key == '#')   { Serial.println("05:ENT"); }
}
