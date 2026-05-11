/*
 * Big Yahu – Level 1 Keypad (Freenove Control Board V4)
 *
 * Wiring (D2–D9, rechts nach links):
 *   D2=Col4  D3=Col3  D4=Col2  D5=Col1
 *   D6=Row4  D7=Row3  D8=Row2  D9=Row1
 *
 * Protokoll eingehend  (Unity → Arduino):
 *   "FF:START\n"  → Keypad-Scan starten (Numpad geöffnet)
 *   "FF:STOP\n"   → Keypad-Scan stoppen (Numpad geschlossen)
 *
 * Protokoll ausgehend  (Arduino → Unity):
 *   "05:0"…"05:9"  Ziffer
 *   "05:DEL"        * gedrückt
 *   "05:ENT"        # gedrückt
 *   "RDY"           Board bereit (nach Reset)
 *
 * D8 = Buzzer (aktiv HIGH), D4 = Relais (aktiv HIGH).
 * Im Idle-Modus werden beide LOW gehalten → kein Piepen.
 * Library: "Keypad" by Mark Stanley & Alexander Brevig
 */

#include <Keypad.h>

#define BUZZER_PIN 8
#define RELAY_PIN  4

const byte ROWS = 4;
const byte COLS = 4;

char keys[ROWS][COLS] = {
  { '1', '2', '3', 'A' },
  { '4', '5', '6', 'B' },
  { '7', '8', '9', 'C' },
  { '*', '0', '#', 'D' }
};

byte rowPins[ROWS] = { 9, BUZZER_PIN, 7, 6 };
byte colPins[COLS] = { 5, RELAY_PIN,  3, 2 };

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, ROWS, COLS);

bool scanning = false;
String serialBuf = "";

// ── Buzzer + Relais ausschalten ───────────────────────────────────────────
void silenceBoard()
{
  pinMode(BUZZER_PIN, OUTPUT); digitalWrite(BUZZER_PIN, LOW);
  pinMode(RELAY_PIN,  OUTPUT); digitalWrite(RELAY_PIN,  LOW);
}

// ── Setup ─────────────────────────────────────────────────────────────────
void setup()
{
  silenceBoard();
  Serial.begin(115200);
  keypad.setDebounceTime(30);
  keypad.setHoldTime(500);
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);
  delay(1500);
  silenceBoard();
  Serial.println("RDY");
}

// ── Eingehende Unity-Befehle lesen ────────────────────────────────────────
void handleSerial()
{
  while (Serial.available())
  {
    char c = Serial.read();
    if (c == '\n')
    {
      serialBuf.trim();
      if (serialBuf == "FF:START") { scanning = true;  }
      if (serialBuf == "FF:STOP")  { scanning = false; silenceBoard(); }
      serialBuf = "";
    }
    else if (serialBuf.length() < 32)
    {
      serialBuf += c;
    }
  }
}

// ── Loop ──────────────────────────────────────────────────────────────────
void loop()
{
  handleSerial();

  if (!scanning)
  {
    silenceBoard();   // Im Idle kein Piepen
    return;
  }

  // Keypad scannen
  char key = keypad.getKey();

  // Sofort nach Scan D8+D4 wieder LOW – verhindert Buzzer-Pulse
  silenceBoard();

  if (!key) return;

  // LED kurz an
  digitalWrite(LED_BUILTIN, HIGH);
  delay(30);
  digitalWrite(LED_BUILTIN, LOW);
  silenceBoard();   // auch nach LED-Delay

  // Senden
  if (key >= '0' && key <= '9')
  {
    Serial.print("05:"); Serial.println(key);
  }
  else if (key == '*')  { Serial.println("05:DEL"); }
  else if (key == '#')  { Serial.println("05:ENT"); }
}
