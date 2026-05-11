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
 *  WIRING (Freenove V4 – D4=Relais und D8=Buzzer werden VERMIEDEN):
 *
 *    Keypad-Kabel   → Arduino-Pin
 *    Col 4 (rechts) → D2
 *    Col 3          → D3
 *    Col 2          → D10   ← war D4 (Relais!) → umgesteckt
 *    Col 1          → D5
 *    Row 4          → D6
 *    Row 3          → D7
 *    Row 2          → D11   ← war D8 (Buzzer!) → umgesteckt
 *    Row 1 (links)  → D9
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

//        Row1  Row2  Row3  Row4
byte rowPins[ROWS] = {  9,   11,    7,    6 };

//        Col1  Col2  Col3  Col4
byte colPins[COLS] = {  5,   10,    3,    2 };

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, ROWS, COLS);

const unsigned long COOLDOWN_MS = 300;
unsigned long lastKeyTime = 0;

// ── Setup ─────────────────────────────────────────────────────────────────

void setup()
{
  Serial.begin(115200);

  keypad.setDebounceTime(50);
  keypad.setHoldTime(1000);

  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);

  // Warten bis Pins nach DTR-Reset stabil sind
  delay(1500);
  Serial.println("RDY");
}

// ── Loop ──────────────────────────────────────────────────────────────────

void loop()
{
  char key = keypad.getKey();
  if (!key) return;

  unsigned long now = millis();
  if (now - lastKeyTime < COOLDOWN_MS) return;
  lastKeyTime = now;

  digitalWrite(LED_BUILTIN, HIGH);
  delay(40);
  digitalWrite(LED_BUILTIN, LOW);

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
}
