/*
 * Big Yahu - Combined Sketch (alle Level in einer .ino-Datei)
 *
 * Ein einziger Sketch fuer das gesamte Spiel. Welches Level aktiv ist,
 * sagt Unity dem Arduino ueber bereits existierende START/STOP-Kommandos.
 * Pro aktiver Stufe wird NUR die jeweils benoetigte Hardware abgefragt –
 * so landen z.B. keine Keypad-Tasten mehr in Level 2.
 *
 * Hardware:
 *   Keypad 4x4 (Freenove Project Board) -> Pins 2,3,4,5 (Reihen) / 6,7,8,9 (Spalten)
 *   Thermistor                          -> A0
 *   Buzzer                              -> Pin 8 (geteilt mit Keypad-Spalte)
 *   Relay                               -> Pin 4 (geteilt mit Keypad-Reihe)
 *
 * Unity -> Arduino:
 *   LV:0 / LV:1 / LV:2     explizit Level setzen (optional)
 *   FF:START / FF:STOP     Level1-Keypad aktivieren/deaktivieren (impliziert LV:1 / LV:0)
 *   10:START / 10:STOP     Level2-Thermistor aktivieren/deaktivieren (impliziert LV:2 / LV:0)
 *   FF:ping                Health-Check, Antwort: FF:pong
 *
 * Arduino -> Unity:
 *   05:<key>               Level1 Keypad-Druck ('0'..'9' | DEL | ENT)
 *   10:TEMP:<celsius>      Level2 laufender Temperaturwert
 *   10:BLOW:<celsius>      Level2 Schwellwert ueberschritten
 *   success                einmaliger Marker bei Level2-Erfolg
 *   FF:ready               einmal nach Setup
 *   FF:pong                Antwort auf FF:ping
 */

// ── Level-State ────────────────────────────────────────────────────────────
enum Level : uint8_t { LV_NONE = 0, LV_KEYPAD = 1, LV_TEMP = 2 };
Level currentLevel = LV_NONE;

String serialBuf = "";

// ── Level 1 – Keypad ───────────────────────────────────────────────────────
#define BUZZER_PIN     8
#define RELAY_PIN      4
#define BEEP_MS        30      // kurzer Klick
#define BEEP_FREQ_HZ   2000    // passiver Buzzer braucht AC-Signal
#define DEBOUNCE_MS    40

const byte ROWS = 4;
const byte COLS = 4;
const char keys[ROWS][COLS] = {
  { '1', '2', '3', 'A' },
  { '4', '5', '6', 'B' },
  { '7', '8', '9', 'C' },
  { '*', '0', '#', 'D' }
};
const byte rowPins[ROWS] = { 2, 3, RELAY_PIN, 5 };
const byte colPins[COLS] = { 6, 7, BUZZER_PIN, 9 };

char keypadLastRaw = 0;
char keypadStable  = 0;
unsigned long keypadFirstSeen = 0;

// ── Level 2 – Thermistor ───────────────────────────────────────────────────
#define THERMISTOR_PIN     A0
#define SERIES_RESISTOR    10000.0
#define NOMINAL_RESISTANCE 10000.0
#define NOMINAL_TEMP       25.0
#define B_COEFFICIENT      3950.0

const unsigned long SAMPLE_INTERVAL_MS = 250;
const unsigned long BLOW_COOLDOWN_MS   = 400;
const float         TEMP_THRESHOLD     = 28.0;

unsigned long tempLastSampleAt = 0;
unsigned long tempLastBlowAt   = 0;
bool          tempSuccessSent  = false;

// ═══════════════════════════════════════════════════════════════════════════
// Setup / Loop
// ═══════════════════════════════════════════════════════════════════════════

void setup() {
  Serial.begin(115200);
  pinMode(LED_BUILTIN, OUTPUT);
  silenceKeypad();
  Serial.println("FF:ready");
}

void loop() {
  handleSerial();

  switch (currentLevel) {
    case LV_KEYPAD: tickKeypad(); break;
    case LV_TEMP:   tickTemp();   break;
    case LV_NONE:   default:      break;  // bewusst still
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Level-Umschaltung
// ═══════════════════════════════════════════════════════════════════════════

void setLevel(Level newLevel) {
  if (newLevel == currentLevel) return;

  // Aufraeumen des alten Levels
  if (currentLevel == LV_KEYPAD) silenceKeypad();
  if (currentLevel == LV_TEMP)   resetTempState();

  currentLevel = newLevel;

  // Initialisierung des neuen Levels
  if (currentLevel == LV_KEYPAD) silenceKeypad();   // sicherer Ausgangszustand fuer Scan
  if (currentLevel == LV_TEMP)   resetTempState();
}

// ═══════════════════════════════════════════════════════════════════════════
// Serial-Kommandos
// ═══════════════════════════════════════════════════════════════════════════

void handleSerial() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();
    if (c == '\n' || c == '\r') {
      if (serialBuf.length() > 0) {
        serialBuf.trim();
        handleCommand(serialBuf);
        serialBuf = "";
      }
    } else if (serialBuf.length() < 32) {
      serialBuf += c;
    }
  }
}

void handleCommand(const String& raw) {
  String cmd = raw;
  cmd.toUpperCase();

  // Explizite Level-Auswahl
  if      (cmd == "LV:0") { setLevel(LV_NONE);   return; }
  else if (cmd == "LV:1") { setLevel(LV_KEYPAD); return; }
  else if (cmd == "LV:2") { setLevel(LV_TEMP);   return; }

  // Implizite Aktivierung ueber bestehende START/STOP-Kommandos
  if (cmd == "FF:START") { setLevel(LV_KEYPAD); return; }
  if (cmd == "FF:STOP")  { if (currentLevel == LV_KEYPAD) setLevel(LV_NONE); return; }
  if (cmd == "10:START") { setLevel(LV_TEMP); Serial.println("10:ready"); return; }
  if (cmd == "10:STOP")  { if (currentLevel == LV_TEMP)   setLevel(LV_NONE); Serial.println("10:stopped"); return; }

  if (cmd == "FF:PING")  { Serial.println("FF:pong"); return; }
}

// ═══════════════════════════════════════════════════════════════════════════
// Level 1 – Keypad-Logik
// ═══════════════════════════════════════════════════════════════════════════

void silenceKeypad() {
  for (int i = 0; i < COLS; i++) pinMode(colPins[i], INPUT);
  for (int i = 0; i < ROWS; i++) pinMode(rowPins[i], INPUT_PULLUP);
}

void beep() {
  // Passiver Buzzer braucht ein Rechteck-Signal (tone), kein DC HIGH.
  // tone() erzeugt es unabhaengig vom vorherigen Pin-Zustand – wichtig,
  // weil Pin 8 = BUZZER_PIN auch Keypad-Spalte 2 (Tasten 3, 6, 9) ist
  // und durch eine gehaltene Taste vorgeladen sein kann.
  digitalWrite(BUZZER_PIN, LOW);
  pinMode(BUZZER_PIN, OUTPUT);
  tone(BUZZER_PIN, BEEP_FREQ_HZ, BEEP_MS);
  delay(BEEP_MS);
  noTone(BUZZER_PIN);
  digitalWrite(BUZZER_PIN, LOW);
  pinMode(BUZZER_PIN, INPUT);
}

char rawScanKeypad() {
  for (byte c = 0; c < COLS; c++) {
    pinMode(colPins[c], OUTPUT);
    digitalWrite(colPins[c], LOW);
    delayMicroseconds(20);

    for (byte r = 0; r < ROWS; r++) {
      if (digitalRead(rowPins[r]) == LOW) {
        char key = keys[r][c];
        // INPUT ohne Pull-Up zurueckgeben (PORT-Bit bleibt LOW von oben).
        // Wichtig fuer Spalte 2 = BUZZER_PIN: sonst summt der Buzzer leise
        // zwischen Erkennung und beep() durch den aktivierten Pull-Up.
        pinMode(colPins[c], INPUT);
        return key;
      }
    }
    pinMode(colPins[c], INPUT);
  }
  return 0;
}

char getDebouncedKey() {
  char raw = rawScanKeypad();
  unsigned long now = millis();

  if (raw != keypadLastRaw) {
    keypadLastRaw   = raw;
    keypadFirstSeen = now;
    return 0;
  }
  if (raw && (raw != keypadStable) && (now - keypadFirstSeen >= DEBOUNCE_MS)) {
    keypadStable = raw;
    return raw;
  }
  if (!raw) keypadStable = 0;
  return 0;
}

void tickKeypad() {
  char key = getDebouncedKey();
  if (!key) return;

  beep();
  digitalWrite(LED_BUILTIN, HIGH);

  if (key >= '0' && key <= '9') {
    Serial.print("05:"); Serial.println(key);
  } else if (key == '*') {
    Serial.println("05:DEL");
  } else if (key == '#') {
    Serial.println("05:ENT");
  }

  delay(50);
  digitalWrite(LED_BUILTIN, LOW);
}

// ═══════════════════════════════════════════════════════════════════════════
// Level 2 – Thermistor-Logik
// ═══════════════════════════════════════════════════════════════════════════

void resetTempState() {
  tempLastSampleAt = 0;
  tempLastBlowAt   = 0;
  tempSuccessSent  = false;
}

float readTemperatureC() {
  int raw = analogRead(THERMISTOR_PIN);
  if (raw <= 0) return -273.15;

  float resistance = SERIES_RESISTOR / (1023.0 / (float)raw - 1.0);
  float tempK = 1.0 / (1.0 / (NOMINAL_TEMP + 273.15) +
                       log(resistance / NOMINAL_RESISTANCE) / B_COEFFICIENT);
  return tempK - 273.15;
}

void tickTemp() {
  unsigned long now = millis();
  if (now - tempLastSampleAt < SAMPLE_INTERVAL_MS) return;
  tempLastSampleAt = now;

  float tempC = readTemperatureC();
  Serial.print("10:TEMP:");
  Serial.println(tempC, 1);

  if (tempC <= TEMP_THRESHOLD) return;
  if (now - tempLastBlowAt < BLOW_COOLDOWN_MS) return;

  if (!tempSuccessSent) {
    Serial.println("success");
    tempSuccessSent = true;
  }

  Serial.print("10:BLOW:");
  Serial.println((int)tempC);
  tempLastBlowAt = now;
}
