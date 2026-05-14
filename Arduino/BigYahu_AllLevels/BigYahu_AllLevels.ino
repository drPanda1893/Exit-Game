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
 *   Farbsensor TCS3200 (Level 3)        -> S0=D4, S1=D5, S2=D6, S3=D7, OUT=D11
 *                                          (kein OE, Frequenz-Skalierung 20 %)
 *   Reset-Taster (Level 3, optional)    -> D12 gegen GND (INPUT_PULLUP)
 *   Joystick Freenove (Level 4)         -> VRX=A1, VRY=A2, SW=A3 (INPUT_PULLUP)
 *
 * Unity -> Arduino:
 *   LV:0 / LV:1 / LV:2 / LV:3 / LV:4   explizit Level setzen (optional)
 *   FF:START / FF:STOP     Level1-Keypad aktivieren/deaktivieren (impliziert LV:1 / LV:0)
 *   10:START / 10:STOP     Level2-Thermistor aktivieren/deaktivieren (impliziert LV:2 / LV:0)
 *   20:START / 20:STOP     Level3-Farbsensor aktivieren/deaktivieren (impliziert LV:3 / LV:0)
 *   30:START / 30:STOP     Level4-Joystick aktivieren/deaktivieren (impliziert LV:4 / LV:0)
 *   FF:ping                Health-Check, Antwort: FF:pong
 *
 * Arduino -> Unity:
 *   05:<key>               Level1 Keypad-Druck ('0'..'9' | DEL | ENT)
 *   10:TEMP:<celsius>      Level2 laufender Temperaturwert
 *   10:BLOW:<celsius>      Level2 Schwellwert ueberschritten
 *   success                einmaliger Marker bei Level2-Erfolg
 *   COLOR:RGB:<r>,<g>,<b>,<name>,<rawR>,<rawG>,<rawB>
 *                                      Level3 laufende Scanner-Werte zum Testen/Kalibrieren
 *   COLOR:RED|GREEN|BLUE   Level3 erkannte Farbe – NUR bei echter Aenderung (1 Farbe = 1 Eingabe)
 *   COLOR:RESET            Level3 physischer Reset-Taster gedrueckt
 *   30:JOY:<x>,<y>,<btn>   Level4 normierter Joystick-Stream (~20 Hz) –
 *                          x,y in [-1.00..+1.00] (Deadzone schon angewandt), btn = 0|1
 *   FF:ready               einmal nach Setup
 *   FF:pong                Antwort auf FF:ping
 */

// ── LCD (I2C: SDA=A4, SCL=A5) ──────────────────────────────────────────────
// Benoetigt Library "LiquidCrystal I2C" (Frank de Brabander) im Library-Manager.
#include <Wire.h>
#include <LiquidCrystal_I2C.h>
LiquidCrystal_I2C lcd(0x27, 16, 2);   // 16x2-Modul; Adresse 0x27 (manche 0x3F)
bool lcdReady = false;

// ── Level-State ────────────────────────────────────────────────────────────
enum Level : uint8_t { LV_NONE = 0, LV_KEYPAD = 1, LV_TEMP = 2, LV_COLOR = 3, LV_JOYSTICK = 4 };
Level currentLevel = LV_NONE;

// Vom Farbsensor (Level 3) erkannte Farbe. MUSS hier oben stehen: die Arduino-IDE
// generiert Prototypen fuer measureColorOnce()/colorName(DetColor) und fuegt sie vor
// setup() ein – DetColor muss dort schon bekannt sein, sonst "does not name a type".
enum DetColor : uint8_t { COL_NONE = 0, COL_RED = 1, COL_GREEN = 2, COL_BLUE = 3 };

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
  initLcd();
  Serial.println("FF:ready");
}

// ── LCD-Hilfsfunktionen ────────────────────────────────────────────────────
void initLcd() {
  Wire.begin();              // A4=SDA, A5=SCL (UNO/Nano-Standard)
  lcd.init();
  lcd.backlight();
  lcd.clear();
  lcd.setCursor(0, 0);
  lcd.print("   Spielzeit:");
  lcd.setCursor(0, 1);
  lcd.print("      00:00");
  lcdReady = true;
}

// Schreibt 'text' aufs 16x2-LCD. Erkennt '|' als Zeilen-Separator und schreibt
// den linken Teil auf Zeile 0, den rechten auf Zeile 1. Beide Zeilen werden
// zentriert. Ohne '|' geht der ganze String zentriert auf Zeile 0.
void lcdShow(const String& text) {
  if (!lcdReady) return;
  int sep = text.indexOf('|');
  String line0 = (sep >= 0) ? text.substring(0, sep) : text;
  String line1 = (sep >= 0) ? text.substring(sep + 1) : String("");
  lcdWriteLine(0, line0);
  lcdWriteLine(1, line1);
}

void lcdWriteLine(uint8_t row, const String& text) {
  int len = text.length();
  if (len > 16) len = 16;
  int col = (16 - len) / 2;

  // Erst die ganze Zeile mit Leerzeichen leeren, dann zentriert ueberschreiben.
  lcd.setCursor(0, row);
  for (int i = 0; i < 16; i++) lcd.print(' ');
  if (len <= 0) return;
  lcd.setCursor(col, row);
  for (int i = 0; i < len; i++) lcd.print(text[i]);
}

void loop() {
  handleSerial();

  switch (currentLevel) {
    case LV_KEYPAD:   tickKeypad();   break;
    case LV_TEMP:     tickTemp();     break;
    case LV_COLOR:    tickColor();    break;
    case LV_JOYSTICK: tickJoystick(); break;
    case LV_NONE:     default:        break;  // bewusst still
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Level-Umschaltung
// ═══════════════════════════════════════════════════════════════════════════

void setLevel(Level newLevel) {
  if (newLevel == currentLevel) return;

  // Aufraeumen des alten Levels
  if (currentLevel == LV_KEYPAD)   silenceKeypad();
  if (currentLevel == LV_TEMP)     resetTempState();
  if (currentLevel == LV_COLOR)    resetColorState();
  if (currentLevel == LV_JOYSTICK) resetJoystickState();

  currentLevel = newLevel;

  // Initialisierung des neuen Levels
  if (currentLevel == LV_KEYPAD)   silenceKeypad();   // sicherer Ausgangszustand fuer Scan
  if (currentLevel == LV_TEMP)     resetTempState();
  if (currentLevel == LV_COLOR)    initColorSensor();
  if (currentLevel == LV_JOYSTICK) initJoystick();
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

  // LCD-Update von Unity: "70:<text>" – kommt mehrmals pro Spiel,
  // wir halten den Pfad bewusst kurz und ohne weiteres Parsing.
  if (cmd.startsWith("70:")) { lcdShow(raw.substring(3)); return; }

  // Explizite Level-Auswahl
  if      (cmd == "LV:0") { setLevel(LV_NONE);     return; }
  else if (cmd == "LV:1") { setLevel(LV_KEYPAD);   return; }
  else if (cmd == "LV:2") { setLevel(LV_TEMP);     return; }
  else if (cmd == "LV:3") { setLevel(LV_COLOR);    return; }
  else if (cmd == "LV:4") { setLevel(LV_JOYSTICK); return; }

  // Implizite Aktivierung ueber bestehende START/STOP-Kommandos
  if (cmd == "FF:START") { setLevel(LV_KEYPAD); return; }
  if (cmd == "FF:STOP")  { if (currentLevel == LV_KEYPAD)   setLevel(LV_NONE); return; }
  if (cmd == "10:START") { setLevel(LV_TEMP);     Serial.println("10:ready"); return; }
  if (cmd == "10:STOP")  { if (currentLevel == LV_TEMP)     setLevel(LV_NONE); Serial.println("10:stopped"); return; }
  if (cmd == "20:START") { setLevel(LV_COLOR);    Serial.println("20:ready"); return; }
  if (cmd == "20:STOP")  { if (currentLevel == LV_COLOR)    setLevel(LV_NONE); Serial.println("20:stopped"); return; }
  if (cmd == "30:START") { setLevel(LV_JOYSTICK); Serial.println("30:ready"); return; }
  if (cmd == "30:STOP")  { if (currentLevel == LV_JOYSTICK) setLevel(LV_NONE); Serial.println("30:stopped"); return; }

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
// Aktiv erst ab "20:START" (Unity sendet das direkt nach der Bibel-Auswahl).
// Verkabelung: S0=D4, S1=D5, S2=D6, S3=D7, OUT=D11 (kein OE).
// Logik 1:1 nach dem Test-Sketch des Spielers:
//   1) Rohwerte fuer R/G/B per pulseIn() messen (jeweils delay(10) zur Stabilisierung)
//   2) per map() mit Weiss/Schwarz-Referenz auf 0..255 normieren + constrain()
//   3) staerkste Komponente > COL_DOMINANT_THRESHOLD wird zur Farbe
// Sendet jeden Takt "COLOR:RGB:r,g,b,NAME,rawR,rawG,rawB" (Live-Werte fuers Terminal).
//
// WICHTIG: Es wird genau EIN bestaetigter Input pro echter Farbaenderung gemeldet:
//   - eine Farbe muss COL_STABLE_NEED Messungen in Folge stabil sein,
//   - und sie muss sich vom zuletzt gemeldeten Wert unterscheiden,
//   - "NONE/Schwarz" loest nichts aus und ueberschreibt den letzten Wert nicht.
// Der physische Reset-Taster (D12 -> GND) sendet "COLOR:RESET" und macht den
// zuletzt gemeldeten Wert wieder "frei", sodass dieselbe Farbe danach erneut zaehlt.
// ═══════════════════════════════════════════════════════════════════════════

// --- Pin-Konfiguration TCS3200 (entspricht dem neuen Test-Sketch) ---
#define TCS_S0          4
#define TCS_S1          5
#define TCS_S2          6
#define TCS_S3          7
#define TCS_OUT         11
#define COL_RESET_PIN   12   // optionaler Reset-Taster gegen GND

// Kalibrierungswerte (aus dem Test-Sketch: map(Wert, Min_Weiss, Max_Schwarz, 255, 0))
const long COL_R_MIN = 25,  COL_R_MAX = 150;
const long COL_G_MIN = 30,  COL_G_MAX = 160;
const long COL_B_MIN = 25,  COL_B_MAX = 140;

// Erkennung: dominanter Kanal muss diese Schwelle ueberschreiten (Test-Sketch: > 100)
const int           COL_DOMINANT_THRESHOLD = 100;
const uint8_t       COL_STABLE_NEED        = 2;     // 2 gleiche Messungen in Folge
const unsigned long COL_SAMPLE_MS          = 200;   // Loop-Takt (= delay(200) im Test-Sketch)
const unsigned long COL_RESET_DEBOUNCE_MS  = 30;
const unsigned long COL_PULSE_TIMEOUT_US   = 50000; // pulseIn-Timeout pro Filter (50 ms),
                                                    // verhindert 1-Sekunden-Blockaden wenn der
                                                    // Sensor mal kein Signal liefert

// Live-Werte fuer COLOR:RGB:...
int  colLastR = 0, colLastG = 0, colLastB = 0;
long colLastRawR = 0, colLastRawG = 0, colLastRawB = 0;

// Wiederholfilter-State
DetColor      colCandidate    = COL_NONE;
uint8_t       colStableCnt    = 0;
DetColor      colConfirmed    = COL_NONE;
unsigned long colLastSampleAt = 0;

// Reset-Taster-State
int           colResetLastRaw    = HIGH;
unsigned long colResetLastEdgeAt = 0;

void resetColorState() {
  colCandidate       = COL_NONE;
  colStableCnt       = 0;
  colConfirmed       = COL_NONE;
  colLastSampleAt    = 0;
  colLastR = colLastG = colLastB = 0;
  colLastRawR = colLastRawG = colLastRawB = 0;
  colResetLastRaw    = HIGH;
  colResetLastEdgeAt = 0;
}

const char* colorName(DetColor c) {
  switch (c) {
    case COL_RED:   return "RED";
    case COL_GREEN: return "GREEN";
    case COL_BLUE:  return "BLUE";
    default:        return "NONE";
  }
}

void initColorSensor() {
  pinMode(TCS_S0,        OUTPUT);
  pinMode(TCS_S1,        OUTPUT);
  pinMode(TCS_S2,        OUTPUT);
  pinMode(TCS_S3,        OUTPUT);
  pinMode(TCS_OUT,       INPUT);
  pinMode(COL_RESET_PIN, INPUT_PULLUP);

  // Frequenz-Skalierung 20 % (S0=HIGH, S1=LOW)
  digitalWrite(TCS_S0, HIGH);
  digitalWrite(TCS_S1, LOW);

  resetColorState();
}

long readTcsPulse(bool s2, bool s3) {
  digitalWrite(TCS_S2, s2 ? HIGH : LOW);
  digitalWrite(TCS_S3, s3 ? HIGH : LOW);
  long v = pulseIn(TCS_OUT, LOW, COL_PULSE_TIMEOUT_US);  // begrenzter Timeout statt 1 s default
  if (v == 0) v = COL_PULSE_TIMEOUT_US;                  // kein Signal -> "schwarz" (grosser Wert)
  delay(10);                       // Pause nach Messung – wie im Test-Sketch
  return v;
}

DetColor measureColorOnce() {
  // Filter-Auswahl exakt wie im Test-Sketch
  long rawR = readTcsPulse(false, false); // ROT:   S2=LOW,  S3=LOW
  long rawG = readTcsPulse(true,  true);  // GRUEN: S2=HIGH, S3=HIGH
  long rawB = readTcsPulse(false, true);  // BLAU:  S2=LOW,  S3=HIGH

  long r = constrain(map(rawR, COL_R_MIN, COL_R_MAX, 255, 0), 0, 255);
  long g = constrain(map(rawG, COL_G_MIN, COL_G_MAX, 255, 0), 0, 255);
  long b = constrain(map(rawB, COL_B_MIN, COL_B_MAX, 255, 0), 0, 255);

  colLastR    = (int)r;
  colLastG    = (int)g;
  colLastB    = (int)b;
  colLastRawR = rawR;
  colLastRawG = rawG;
  colLastRawB = rawB;

  if (r > g && r > b && r > COL_DOMINANT_THRESHOLD) return COL_RED;
  if (g > r && g > b && g > COL_DOMINANT_THRESHOLD) return COL_GREEN;
  if (b > r && b > g && b > COL_DOMINANT_THRESHOLD) return COL_BLUE;
  return COL_NONE;
}

void pollColorResetButton() {
  int raw = digitalRead(COL_RESET_PIN);
  unsigned long now = millis();
  if (raw != colResetLastRaw && (now - colResetLastEdgeAt) >= COL_RESET_DEBOUNCE_MS) {
    colResetLastEdgeAt = now;
    if (colResetLastRaw == HIGH && raw == LOW) {
      // fallende Flanke = Taster gedrueckt -> letzte bestaetigte Farbe freigeben
      colConfirmed = COL_NONE;
      Serial.println("COLOR:RESET");
    }
    colResetLastRaw = raw;
  }
}

void tickColor() {
  pollColorResetButton();

  unsigned long now = millis();
  if (now - colLastSampleAt < COL_SAMPLE_MS) return;
  colLastSampleAt = now;

  DetColor c = measureColorOnce();

  // Live-Werte (Unity rendert sie im Login-Terminal)
  Serial.print("COLOR:RGB:");
  Serial.print(colLastR);    Serial.print(',');
  Serial.print(colLastG);    Serial.print(',');
  Serial.print(colLastB);    Serial.print(',');
  Serial.print(colorName(c));Serial.print(',');
  Serial.print(colLastRawR); Serial.print(',');
  Serial.print(colLastRawG); Serial.print(',');
  Serial.println(colLastRawB);

  // Wiederholfilter
  if (c == colCandidate) {
    if (colStableCnt < 255) colStableCnt++;
  } else {
    colCandidate = c;
    colStableCnt = 1;
  }

  if (colStableCnt == COL_STABLE_NEED) {
    if (c != COL_NONE && c != colConfirmed) {
      colConfirmed = c;
      Serial.print("COLOR:");
      Serial.println(colorName(c));
    }
  }
}

// ═══════════════════════════════════════════════════════════════════════════
// Level 4 – Joystick Freenove (Arcade-Steuerung fuers Waerter-Stealth-Spiel)
//
// Aktiv erst ab "30:START". Vorher wird A1/A2/A3 nicht angefasst, damit
// andere Level (z.B. A0-Thermistor in L2) ungestoert bleiben.
// VRX = A1, VRY = A2, SW = A3 (Taster gegen GND, INPUT_PULLUP).
// Sendet ~20 Hz "30:JOY:<x>,<y>,<btn>" mit normierten Achsen in [-1.00..+1.00]
// und Button = 0|1. Die Mittenposition (~512) wird zu 0, eine kleine Deadzone
// um die Mitte unterdrueckt Drift.
// ═══════════════════════════════════════════════════════════════════════════

#define JOY_X_PIN   A1
#define JOY_Y_PIN   A2
#define JOY_BTN_PIN A3

const unsigned long JOY_SAMPLE_MS  = 50;     // 20 Hz
const int           JOY_DEFAULT_C  = 512;
const float         JOY_DEADZONE   = 0.15f;  // ~15 % – Freenove-Joysticks haben Spiel um Mitte
const uint8_t       JOY_ZERO_SAMP  = 16;     // Mittelung fuer Auto-Zero
const uint8_t       JOY_ZERO_DELAY = 4;      // ms zwischen Auto-Zero-Samples

unsigned long joyLastSampleAt = 0;
int           joyCenterX      = JOY_DEFAULT_C;
int           joyCenterY      = JOY_DEFAULT_C;

void resetJoystickState() {
  joyLastSampleAt = 0;
  joyCenterX      = JOY_DEFAULT_C;
  joyCenterY      = JOY_DEFAULT_C;
}

void initJoystick() {
  pinMode(JOY_BTN_PIN, INPUT_PULLUP);
  // analogRead-Pins brauchen kein pinMode
  resetJoystickState();

  // Auto-Zero: aktuelle Ruheposition als Mittelpunkt verwenden.
  // Annahme: beim Wechsel in Level 4 wird der Joystick nicht beruehrt.
  // Loest das Problem, dass die Figur ohne Eingabe driftet.
  long sumX = 0, sumY = 0;
  // ersten Wert verwerfen (ADC Channel-Switch braucht eine Wandlung zum Setzen)
  (void)analogRead(JOY_X_PIN);
  (void)analogRead(JOY_Y_PIN);
  for (uint8_t i = 0; i < JOY_ZERO_SAMP; i++) {
    sumX += analogRead(JOY_X_PIN);
    sumY += analogRead(JOY_Y_PIN);
    delay(JOY_ZERO_DELAY);
  }
  joyCenterX = sumX / JOY_ZERO_SAMP;
  joyCenterY = sumY / JOY_ZERO_SAMP;
}

void tickJoystick() {
  unsigned long now = millis();
  if (now - joyLastSampleAt < JOY_SAMPLE_MS) return;
  joyLastSampleAt = now;

  int rawX = analogRead(JOY_X_PIN);
  int rawY = analogRead(JOY_Y_PIN);
  int btn  = (digitalRead(JOY_BTN_PIN) == LOW) ? 1 : 0;

  // Range zur kleineren Seite des gemessenen Centers normieren,
  // damit voller Ausschlag wirklich +/-1.0 erreicht.
  float spanXNeg = (float)joyCenterX;
  float spanXPos = (float)(1023 - joyCenterX);
  float spanYNeg = (float)joyCenterY;
  float spanYPos = (float)(1023 - joyCenterY);
  float dx = (float)(rawX - joyCenterX);
  float dy = (float)(rawY - joyCenterY);
  float nx = dx >= 0 ? (spanXPos > 0 ? dx / spanXPos : 0) : (spanXNeg > 0 ? dx / spanXNeg : 0);
  float ny = dy >= 0 ? (spanYPos > 0 ? dy / spanYPos : 0) : (spanYNeg > 0 ? dy / spanYNeg : 0);
  if (nx >  1.0f) nx =  1.0f;
  if (nx < -1.0f) nx = -1.0f;
  if (ny >  1.0f) ny =  1.0f;
  if (ny < -1.0f) ny = -1.0f;
  if (fabs(nx) < JOY_DEADZONE) nx = 0.0f;
  if (fabs(ny) < JOY_DEADZONE) ny = 0.0f;

  Serial.print("30:JOY:");
  Serial.print(nx, 2);
  Serial.print(',');
  Serial.print(ny, 2);
  Serial.print(',');
  Serial.println(btn);
}