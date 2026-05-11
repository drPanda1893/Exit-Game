/*
 * ============================================================
 *  Big Yahu – Level 1 Keypad Interface
 *  Freenove Control Board V4 (Arduino Uno kompatibel)
 * ============================================================
 *
 *  Keypad-Layout (4x4):
 *    [ 1 ][ 2 ][ 3 ][ A ]
 *    [ 4 ][ 5 ][ 6 ][ B ]
 *    [ 7 ][ 8 ][ 9 ][ C ]
 *    [ * ][ 0 ][ # ][ D ]
 *
 *  Wiring – Anschlüsse von RECHTS nach LINKS: D2 bis D9
 *    D2=Col4  D3=Col3  D4=Col2  D5=Col1
 *    D6=Row4  D7=Row3  D8=Row2  D9=Row1
 *
 *  Warum D8/D4 problematisch sind (Freenove V4):
 *    D8 = Buzzer (aktiv HIGH)
 *    D4 = Relais (aktiv HIGH)
 *    Die Keypad-Library setzt Row-Pins nach dem Scan auf INPUT_PULLUP
 *    → D8 geht kurz HIGH → Buzzer piept.
 *    D4 als Col-Pin wird auf INPUT_PULLUP gezogen → Relais aktiviert.
 *    Fix: silenceBoard() nach jedem getKey()-Aufruf.
 *
 *  Protokoll (115200 Baud):
 *    "05:0"..."05:9"  Ziffer
 *    "05:DEL"         * gedrückt
 *    "05:ENT"         # gedrückt
 *
 *  Library: "Keypad" by Mark Stanley & Alexander Brevig
 * ============================================================
 */

#include <Keypad.h>

// ── Onboard-Konflikt-Pins (Freenove V4) ──────────────────────────────────
#define BUZZER_PIN 8   // aktiv HIGH → muss nach Scan auf LOW gehalten werden
#define RELAY_PIN  4   // aktiv HIGH → muss nach Scan auf LOW gehalten werden

// ── Matrix ────────────────────────────────────────────────────────────────
const byte ROWS = 4;
const byte COLS = 4;

char keys[ROWS][COLS] = {
  { '1', '2', '3', 'A' },
  { '4', '5', '6', 'B' },
  { '7', '8', '9', 'C' },
  { '*', '0', '#', 'D' }
};

//             Row1  Row2        Row3  Row4
byte rowPins[ROWS] = { 9,  BUZZER_PIN,  7,    6 };

//             Col1  Col2       Col3  Col4
byte colPins[COLS] = { 5,  RELAY_PIN,   3,    2 };

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, ROWS, COLS);

const unsigned long COOLDOWN_MS = 300;
unsigned long lastKeyTime       = 0;

// ── Onboard-Komponenten deaktivieren ─────────────────────────────────────
// Die Keypad-Library lässt D8 und D4 zwischen Scans auf INPUT_PULLUP (HIGH).
// Diese Funktion zwingt sie sofort wieder auf LOW.
void silenceBoard()
{
  pinMode(BUZZER_PIN, OUTPUT);
  digitalWrite(BUZZER_PIN, LOW);
  pinMode(RELAY_PIN, OUTPUT);
  digitalWrite(RELAY_PIN, LOW);
}

// ── Setup ─────────────────────────────────────────────────────────────────
void setup()
{
  silenceBoard();           // sofort deaktivieren, noch vor allem anderen
  Serial.begin(115200);
  keypad.setDebounceTime(50);
  keypad.setHoldTime(1000);
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);
  delay(1500);              // warten bis Pins nach DTR-Reset stabil sind
  silenceBoard();           // nochmal sicherstellen
  Serial.println("RDY");
}

// ── Loop ──────────────────────────────────────────────────────────────────
void loop()
{
  char key = keypad.getKey();
  silenceBoard();           // sofort nach Scan: Buzzer + Relais aus

  if (!key) return;

  unsigned long now = millis();
  if (now - lastKeyTime < COOLDOWN_MS) return;
  lastKeyTime = now;

  digitalWrite(LED_BUILTIN, HIGH);
  delay(40);
  digitalWrite(LED_BUILTIN, LOW);
  silenceBoard();           // auch nach LED-Delay sicherstellen

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
