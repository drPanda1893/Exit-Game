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
 *   Farbsensor TCS3200 (Level 3)        -> S0=4, S1=5, S2=6, S3=7, OUT=8
 *                                          (teilt sich Pins mit dem Keypad – es ist
 *                                           immer nur ein Level aktiv, daher ok)
 *   Reset-Taster (Level 3)              -> Pin 12 gegen GND (INPUT_PULLUP)
 *
 * Unity -> Arduino:
 *   LV:0 / LV:1 / LV:2 / LV:3   explizit Level setzen (optional)
 *   FF:START / FF:STOP     Level1-Keypad aktivieren/deaktivieren (impliziert LV:1 / LV:0)
 *   10:START / 10:STOP     Level2-Thermistor aktivieren/deaktivieren (impliziert LV:2 / LV:0)
 *   20:START / 20:STOP     Level3-Farbsensor aktivieren/deaktivieren (impliziert LV:3 / LV:0)
 *   FF:ping                Health-Check, Antwort: FF:pong
 *
 * Arduino -> Unity:
 *   05:<key>               Level1 Keypad-Druck ('0'..'9' | DEL | ENT)
 *   10:TEMP:<celsius>      Level2 laufender Temperaturwert
 *   10:BLOW:<celsius>      Level2 Schwellwert ueberschritten
 *   success                einmaliger Marker bei Level2-Erfolg
 *   COLOR:RGB:<r>,<g>,<b>,<name>   Level3 laufende Scanner-Werte (0..255 + erkannte Farbe), jeder Takt
 *   COLOR:RED|GREEN|BLUE   Level3 erkannte Farbe – NUR bei echter Aenderung (1 Farbe = 1 Eingabe)
 *   COLOR:RESET            Level3 physischer Reset-Taster gedrueckt
 *   FF:ready               einmal nach Setup
 *   FF:pong                Antwort auf FF:ping
 */

// ── Level-State ────────────────────────────────────────────────────────────
enum Level : uint8_t { LV_NONE = 0, LV_KEYPAD = 1, LV_TEMP = 2, LV_COLOR = 3 };
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
    case LV_COLOR:  tickColor();  break;
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
  if (currentLevel == LV_COLOR)  resetColorState();

  currentLevel = newLevel;

  // Initialisierung des neuen Levels
  if (currentLevel == LV_KEYPAD) silenceKeypad();   // sicherer Ausgangszustand fuer Scan
  if (currentLevel == LV_TEMP)   resetTempState();
  if (currentLevel == LV_COLOR)  initColorSensor();
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
  else if (cmd == "LV:3") { setLevel(LV_COLOR);  return; }

  // Implizite Aktivierung ueber bestehende START/STOP-Kommandos
  if (cmd == "FF:START") { setLevel(LV_KEYPAD); return; }
  if (cmd == "FF:STOP")  { if (currentLevel == LV_KEYPAD) setLevel(LV_NONE); return; }
  if (cmd == "10:START") { setLevel(LV_TEMP); Serial.println("10:ready"); return; }
  if (cmd == "10:STOP")  { if (currentLevel == LV_TEMP)   setLevel(LV_NONE); Serial.println("10:stopped"); return; }
  if (cmd == "20:START") { setLevel(LV_COLOR); Serial.println("20:ready"); return; }
  if (cmd == "20:STOP")  { if (currentLevel == LV_COLOR)  setLevel(LV_NONE); Serial.println("20:stopped"); return; }

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

// ═══════════════════════════════════════════════════════════════════════════
// Level 3 – Farbsensor TCS3200 (Login-Terminal in der Bibliothek)
//
// Aktiv erst ab "20:START" (Unity sendet das, sobald das Login-UI aufgeht).
// Logik nach dem Test-Sketch des Spielers: Rohwerte messen, kalibrieren,
// auf 0..255 mappen und die staerkste Komponente als Farbe werten.
//
// WICHTIG: Es wird genau EIN Input pro echter Farbaenderung gemeldet:
//   - eine Farbe muss COL_STABLE_NEED Messungen in Folge stabil sein,
//   - und sie muss sich vom zuletzt gemeldeten Wert unterscheiden,
//   - "NONE/Schwarz" loest nichts aus und ueberschreibt den letzten Wert nicht.
// Der physische Reset-Taster (Pin 12 -> GND) sendet "COLOR:RESET" und macht den
// zuletzt gemeldeten Wert wieder "frei", sodass dieselbe Farbe danach erneut zaehlt.
// ═══════════════════════════════════════════════════════════════════════════

#define TCS_S0              4
#define TCS_S1              5
#define TCS_S2              6
#define TCS_S3              7
#define TCS_OUT             8
#define COLOR_RESET_BTN_PIN 12

// Kalibrierung: Rohwerte -> 0..255  (Weiss = kleiner Rohwert, Schwarz = grosser).
// Bei Bedarf an die echte Umgebung anpassen (siehe Original-Test-Sketch).
const long COL_R_MIN = 25,  COL_R_MAX = 150;
const long COL_G_MIN = 30,  COL_G_MAX = 160;
const long COL_B_MIN = 25,  COL_B_MAX = 140;
const int  COL_THRESHOLD    = 100;   // ab hier gilt eine Komponente als "Farbe"
const byte COL_STABLE_NEED  = 4;     // so viele gleiche Messungen in Folge = bestaetigt
const unsigned long COL_SAMPLE_MS = 80;
const unsigned long COL_PULSE_TIMEOUT_US = 8000;

enum DetColor : uint8_t { COL_NONE = 0, COL_RED = 1, COL_GREEN = 2, COL_BLUE = 3 };

unsigned long colLastSampleAt = 0;
DetColor      colCandidate    = COL_NONE;   // aktuelle Mess-Serie
byte          colStableCnt    = 0;          // Laenge der aktuellen Serie
DetColor      colConfirmed    = COL_NONE;   // zuletzt an Unity gemeldete Farbe
bool          colBtnLast      = HIGH;       // Reset-Taster Entprellung
bool          colBtnHandled   = false;
unsigned long colBtnChangedAt = 0;
int           colLastR = 0, colLastG = 0, colLastB = 0;   // letzte gemappten 0..255-Werte

void initColorSensor() {
  pinMode(TCS_S0, OUTPUT);
  pinMode(TCS_S1, OUTPUT);
  pinMode(TCS_S2, OUTPUT);
  pinMode(TCS_S3, OUTPUT);
  pinMode(TCS_OUT, INPUT);
  pinMode(COLOR_RESET_BTN_PIN, INPUT_PULLUP);
  // Frequenz-Skalierung 20 % (Standard fuer den TCS3200)
  digitalWrite(TCS_S0, HIGH);
  digitalWrite(TCS_S1, LOW);

  colLastSampleAt = 0;
  colCandidate    = COL_NONE;
  colStableCnt    = 0;
  colConfirmed    = COL_NONE;
  colBtnLast      = digitalRead(COLOR_RESET_BTN_PIN);
  colBtnHandled   = (colBtnLast == LOW);   // beim Eintritt gedrueckt? -> nicht sofort feuern
  colBtnChangedAt = millis();
}

void resetColorState() {
  colLastSampleAt = 0;
  colCandidate    = COL_NONE;
  colStableCnt    = 0;
  colConfirmed    = COL_NONE;
  colBtnLast      = HIGH;
  colBtnHandled   = false;
  // Sensor-Pins freigeben, damit Keypad/None sie ungestoert nutzen koennen
  pinMode(TCS_S0, INPUT);
  pinMode(TCS_S1, INPUT);
  pinMode(TCS_S2, INPUT);
  pinMode(TCS_S3, INPUT);
  pinMode(TCS_OUT, INPUT);
}

long readTcsPulse(bool s2, bool s3) {
  digitalWrite(TCS_S2, s2 ? HIGH : LOW);
  digitalWrite(TCS_S3, s3 ? HIGH : LOW);
  delayMicroseconds(200);                       // Photodioden-Filter umschalten lassen
  return pulseIn(TCS_OUT, LOW, COL_PULSE_TIMEOUT_US);
}

DetColor measureColorOnce() {
  long redRaw   = readTcsPulse(false, false);   // S2=L S3=L -> Rot
  long greenRaw = readTcsPulse(true,  true);    // S2=H S3=H -> Gruen
  long blueRaw  = readTcsPulse(false, true);    // S2=L S3=H -> Blau

  long r = constrain(map(redRaw,   COL_R_MIN, COL_R_MAX, 255, 0), 0, 255);
  long g = constrain(map(greenRaw, COL_G_MIN, COL_G_MAX, 255, 0), 0, 255);
  long b = constrain(map(blueRaw,  COL_B_MIN, COL_B_MAX, 255, 0), 0, 255);
  colLastR = (int)r; colLastG = (int)g; colLastB = (int)b;

  if (r > g && r > b && r > COL_THRESHOLD) return COL_RED;
  if (g > r && g > b && g > COL_THRESHOLD) return COL_GREEN;
  if (b > r && b > g && b > COL_THRESHOLD) return COL_BLUE;
  return COL_NONE;
}

const char* colorName(DetColor c) {
  switch (c) {
    case COL_RED:   return "RED";
    case COL_GREEN: return "GREEN";
    case COL_BLUE:  return "BLUE";
    default:        return "NONE";
  }
}

void tickColor() {
  unsigned long now = millis();

  // ── Reset-Taster (entprellt, gegen GND) ──────────────────────────────────
  bool btn = digitalRead(COLOR_RESET_BTN_PIN);
  if (btn != colBtnLast) { colBtnLast = btn; colBtnChangedAt = now; }
  if (btn == LOW && !colBtnHandled && (now - colBtnChangedAt) >= 30) {
    colBtnHandled = true;
    colConfirmed  = COL_NONE;            // dieselbe Farbe danach wieder zaehlbar
    colCandidate  = COL_NONE;
    colStableCnt  = 0;
    Serial.println("COLOR:RESET");
    digitalWrite(LED_BUILTIN, HIGH); delay(40); digitalWrite(LED_BUILTIN, LOW);
  }
  if (btn == HIGH) colBtnHandled = false;

  // ── Farbmessung im festen Takt ───────────────────────────────────────────
  if (now - colLastSampleAt < COL_SAMPLE_MS) return;
  colLastSampleAt = now;

  DetColor c = measureColorOnce();

  // Live-Werte des Scanners ans Terminal melden (jeder Mess-Takt)
  Serial.print("COLOR:RGB:");
  Serial.print(colLastR); Serial.print(',');
  Serial.print(colLastG); Serial.print(',');
  Serial.print(colLastB); Serial.print(',');
  Serial.println(colorName(c));

  // Stabilitaets-Serie verlaengern oder neu starten
  if (c == colCandidate) {
    if (colStableCnt < 255) colStableCnt++;
  } else {
    colCandidate = c;
    colStableCnt = 1;
  }

  if (colStableCnt != COL_STABLE_NEED) return;   // genau einmal beim Bestaetigen
  if (c == COL_NONE)        return;              // "kein/schwarz" loest nichts aus
  if (c == colConfirmed)    return;              // unveraendert -> kein neuer Input

  colConfirmed = c;
  Serial.print("COLOR:");
  Serial.println(colorName(c));
  digitalWrite(LED_BUILTIN, HIGH); delay(20); digitalWrite(LED_BUILTIN, LOW);
}
