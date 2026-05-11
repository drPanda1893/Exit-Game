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
 *  !! FALLS FALSCHE ZAHLEN ODER SPAM: Option B auskommentieren !!
 *
 *  Option A (Standard – Ribbon: Row1..Row4, Col1..Col4)
 *    D9=Row1  D8=Row2  D7=Row3  D6=Row4
 *    D5=Col1  D4=Col2  D3=Col3  D2=Col4
 *
 *  Option B (getauscht – Ribbon: Col1..Col4, Row1..Row4)
 *    D9=Col1  D8=Col2  D7=Col3  D6=Col4
 *    D5=Row1  D4=Row2  D3=Row3  D2=Row4
 *
 *  Protokoll (Arduino → PC, 115200 Baud):
 *    "05:0" … "05:9"   Ziffer gedrückt      (Cmd-ID 0x05)
 *    "05:DEL"           * gedrückt           (löschen)
 *    "05:ENT"           # gedrückt           (bestätigen)
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

// ── Option A (Standard) ───────────────────────────────────────────────────
byte rowPins[ROWS] = { 9, 8, 7, 6 };   // Row1..Row4
byte colPins[COLS] = { 5, 4, 3, 2 };   // Col1..Col4

// ── Option B – auskommentieren falls falsche Tasten / Spam ────────────────
// byte rowPins[ROWS] = { 5, 4, 3, 2 };
// byte colPins[COLS] = { 9, 8, 7, 6 };
// ─────────────────────────────────────────────────────────────────────────

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, ROWS, COLS);

// ── Setup ─────────────────────────────────────────────────────────────────

void setup()
{
  Serial.begin(115200);

  keypad.setDebounceTime(50);   // 50 ms – verhindert Spam bei prellenden Kontakten
  keypad.setHoldTime(500);      // Taste muss 500 ms gehalten werden bis HOLD ausgelöst wird

  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);
}

// ── Loop ──────────────────────────────────────────────────────────────────

void loop()
{
  // Nur bei PRESSED reagieren (nicht bei HOLD oder RELEASED)
  KeyState state = keypad.getState();
  char key = keypad.getKey();
  if (!key) return;

  // Kurzes LED-Blinken als Bestätigung
  digitalWrite(LED_BUILTIN, HIGH);
  delay(40);
  digitalWrite(LED_BUILTIN, LOW);

  // Protokoll: "05:payload"
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
