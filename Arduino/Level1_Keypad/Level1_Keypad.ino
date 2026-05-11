/*
 * ============================================================
 *  Big Yahu – Level 1 Keypad Interface
 *  Arduino Uno / Nano
 * ============================================================
 *
 *  Keypad-Layout (4×4):
 *    [ 1 ][ 2 ][ 3 ][ A ]
 *    [ 4 ][ 5 ][ 6 ][ B ]
 *    [ 7 ][ 8 ][ 9 ][ C ]
 *    [ * ][ 0 ][ # ][ D ]
 *
 *  Wiring – Anschlüsse von RECHTS nach LINKS: Arduino D2 → D9
 *
 *    Keypad-Pin (rechts→links)  Typ    Arduino
 *    1 (ganz rechts)            Col4   D2
 *    2                          Col3   D3
 *    3                          Col2   D4
 *    4                          Col1   D5
 *    5                          Row4   D6
 *    6                          Row3   D7
 *    7                          Row2   D8
 *    8 (ganz links)             Row1   D9
 *
 *  Protokoll (Arduino → PC, 9600 Baud):
 *    "KEY:0" … "KEY:9"   Ziffer gedrückt
 *    "KEY:DEL"            * oder A gedrückt  (löschen)
 *    "KEY:ENT"            # oder B gedrückt  (bestätigen)
 *
 *  Library: "Keypad" by Mark Stanley & Alexander Brevig
 *    Arduino IDE → Sketch → Include Library → Manage Libraries → "Keypad"
 * ============================================================
 */

#include <Keypad.h>

// ── Matrix ────────────────────────────────────────────────────────────────

const byte ROWS = 4;
const byte COLS = 4;

char keys[ROWS][COLS] = {
  { '1', '2', '3', 'A' },
  { '4', '5', '6', 'B' },
  { '7', '8', '9', 'C' },
  { '*', '0', '#', 'D' }
};

// Wiring: von rechts nach links D2–D9
//   Row1=D9  Row2=D8  Row3=D7  Row4=D6
//   Col1=D5  Col2=D4  Col3=D3  Col4=D2
byte rowPins[ROWS] = { 9, 8, 7, 6 };
byte colPins[COLS]  = { 5, 4, 3, 2 };

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, ROWS, COLS);

// ── Setup ─────────────────────────────────────────────────────────────────

void setup()
{
  Serial.begin(9600);
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);
}

// ── Loop ──────────────────────────────────────────────────────────────────

void loop()
{
  char key = keypad.getKey();
  if (!key) return;

  // Kurzes LED-Blinken als Bestätigung
  digitalWrite(LED_BUILTIN, HIGH);
  delay(60);
  digitalWrite(LED_BUILTIN, LOW);

  // Protokoll senden
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
  // C, D → keine Funktion in Level 1
}
