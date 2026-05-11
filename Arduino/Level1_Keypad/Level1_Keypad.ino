/*
 * ============================================================
 *  Big Yahu – Level 1 Keypad Interface
 *  Freenove Control Board V4 (Arduino Uno kompatibel)
 * ============================================================
 *
 *  Keypad-Layout (4×4):
 *    [ 1 ][ 2 ][ 3 ][ A ]
 *    [ 4 ][ 5 ][ 6 ][ B ]
 *    [ 7 ][ 8 ][ 9 ][ C ]
 *    [ * ][ 0 ][ # ][ D ]
 *
 *  Wiring – Anschlüsse von RECHTS nach LINKS: D2 → D9
 *
 *  !! FALLS FALSCHE ZAHLEN: Option B einkommentieren !!
 *
 *  Option A (Standard – Ribbon: Row1..Row4 | Col1..Col4)
 *    D9=Row1  D8=Row2  D7=Row3  D6=Row4
 *    D5=Col1  D4=Col2  D3=Col3  D2=Col4
 *
 *  Option B (Ribbon: Col1..Col4 | Row1..Row4)
 *    D9=Col1  D8=Col2  D7=Col3  D6=Col4
 *    D5=Row1  D4=Row2  D3=Row3  D2=Row4
 *
 *  Protokoll (Arduino → PC, 115200 Baud):
 *    "05:0" … "05:9"   Ziffer          (Cmd-ID 0x05)
 *    "05:DEL"           * gedrückt
 *    "05:ENT"           # gedrückt
 *
 *  Library: "Keypad" by Mark Stanley & Alexander Brevig
 * ============================================================
 */

#include <Keypad.h>

const byte ROWS = 4;
const byte COLS = 4;

char keys[ROWS][COLS] = {
  { '1', '2', '3', 'A' },
  { '4', '5', '6', 'B' },
  { '7', '8', '9', 'C' },
  { '*', '0', '#', 'D' }
};

// ── Option A ──────────────────────────────────────────────────────────────
byte rowPins[ROWS] = { 9, 8, 7, 6 };
byte colPins[COLS] = { 5, 4, 3, 2 };

// ── Option B – einkommentieren falls Tasten falsch ────────────────────────
// byte rowPins[ROWS] = { 5, 4, 3, 2 };
// byte colPins[COLS] = { 9, 8, 7, 6 };
// ─────────────────────────────────────────────────────────────────────────

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, ROWS, COLS);

// Mindestzeit zwischen zwei Tastendrücken (verhindert Phantom-Eingaben)
const unsigned long COOLDOWN_MS = 300;
unsigned long lastKeyTime = 0;

// ── Setup ─────────────────────────────────────────────────────────────────

void setup()
{
  // Spalten-Pins explizit auf INPUT_PULLUP – verhindert Float beim Start
  for (byte c : colPins)
  {
    pinMode(c, INPUT_PULLUP);
  }

  Serial.begin(115200);

  keypad.setDebounceTime(50);
  keypad.setHoldTime(1000);

  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);

  // Warten bis Pins nach DTR-Reset stabil sind (Freenove-Board setzt Arduino
  // beim Verbinden per Unity kurz zurück → ohne Delay kommen Phantomtasten)
  delay(1500);

  Serial.println("RDY");  // Unity weiß: Board ist bereit
}

// ── Loop ──────────────────────────────────────────────────────────────────

void loop()
{
  // getKey() liefert nur bei PRESSED (nicht bei HOLD / RELEASED)
  char key = keypad.getKey();
  if (!key) return;

  // Cooldown: schnelle Doppeltreffer ignorieren
  unsigned long now = millis();
  if (now - lastKeyTime < COOLDOWN_MS) return;
  lastKeyTime = now;

  // Kurzes LED-Blinken
  digitalWrite(LED_BUILTIN, HIGH);
  delay(40);
  digitalWrite(LED_BUILTIN, LOW);

  // Protokoll senden
  if (key >= '0' && key <= '9')
  {
    Serial.print("05:");
    Serial.println(key);
  }
  else if (key == '*')
  {
    Serial.println("05:DEL");
  }
  else if (key == '#')
  {
    Serial.println("05:ENT");
  }
  // A, B, C, D → keine Funktion in Level 1
}
