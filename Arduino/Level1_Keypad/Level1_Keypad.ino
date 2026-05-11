/*
 * ============================================================
 *  Big Yahu – Level 1 Keypad Interface
 *  Arduino Uno / Nano
 * ============================================================
 *
 *  Protocol (Arduino → PC via USB Serial, 9600 baud):
 *    "KEY:0" … "KEY:9"   digit pressed
 *    "KEY:DEL"            delete last digit  (* or A key)
 *    "KEY:ENT"            confirm entry       (# or B key)
 *
 *  Keypad layout (standard 4×4 membrane):
 *    [ 1 ] [ 2 ] [ 3 ] [ A ]
 *    [ 4 ] [ 5 ] [ 6 ] [ B ]
 *    [ 7 ] [ 8 ] [ 9 ] [ C ]
 *    [ * ] [ 0 ] [ # ] [ D ]
 *
 *  Wiring (8-pin ribbon, pin 1 = leftmost):
 *    Keypad Pin 1  (Row 1)  →  Arduino D9
 *    Keypad Pin 2  (Row 2)  →  Arduino D8
 *    Keypad Pin 3  (Row 3)  →  Arduino D7
 *    Keypad Pin 4  (Row 4)  →  Arduino D6
 *    Keypad Pin 5  (Col 1)  →  Arduino D5
 *    Keypad Pin 6  (Col 2)  →  Arduino D4
 *    Keypad Pin 7  (Col 3)  →  Arduino D3
 *    Keypad Pin 8  (Col 4)  →  Arduino D2
 *
 *  Library required:
 *    "Keypad" by Mark Stanley & Alexander Brevig
 *    Install: Arduino IDE → Sketch → Include Library → Manage Libraries → search "Keypad"
 * ============================================================
 */

#include <Keypad.h>

// ── Keypad Matrix Definition ──────────────────────────────────────────────

const byte ROWS = 4;
const byte COLS = 4;

char keys[ROWS][COLS] = {
  { '1', '2', '3', 'A' },
  { '4', '5', '6', 'B' },
  { '7', '8', '9', 'C' },
  { '*', '0', '#', 'D' }
};

byte rowPins[ROWS] = { 9, 8, 7, 6 };  // connect to Row 1–4
byte colPins[COLS] = { 5, 4, 3, 2 };  // connect to Col 1–4

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, ROWS, COLS);

// ── Setup ─────────────────────────────────────────────────────────────────

void setup()
{
  Serial.begin(9600);
  pinMode(LED_BUILTIN, OUTPUT);  // visual key-press feedback
  digitalWrite(LED_BUILTIN, LOW);
}

// ── Main Loop ─────────────────────────────────────────────────────────────

void loop()
{
  char key = keypad.getKey();
  if (!key) return;

  // Short LED blink as tactile confirmation
  digitalWrite(LED_BUILTIN, HIGH);
  delay(60);
  digitalWrite(LED_BUILTIN, LOW);

  // Send protocol message
  if (key >= '0' && key <= '9')
  {
    Serial.print("KEY:");
    Serial.println(key);
  }
  else if (key == '*' || key == 'A')
  {
    Serial.println("KEY:DEL");
  }
  else if (key == '#' || key == 'B')
  {
    Serial.println("KEY:ENT");
  }
  // C, D → no function assigned in Level 1
}
