/*
 * Big Yahu – Level 1 Keypad (Freenove Projects Kit FNK0059 / Control Board V4)
 *
 * Pin-Belegung laut Freenove FNK0059-Dokumentation (Kapitel Keypad 4x4):
 *   D2=Row1  D3=Row2  D4=Row3  D5=Row4
 *   D6=Col1  D7=Col2  D8=Col3  D9=Col4
 *
 * Board-Konflikte (Control Board V4 Onboard-Komponenten):
 *   D4 = Relay   (aktiv HIGH) → ist Row3 → Keypad-Lib setzt INPUT_PULLUP → Relay zieht an
 *   D8 = Buzzer  (aktiv HIGH) → ist Col3 → INPUT_PULLUP zwischen Scans → Piepen
 *   Lösung: Nur scannen wenn Unity "FF:START" sendet; silenceBoard() nach jedem Scan
 *
 * Protokoll eingehend (Unity → Arduino):
 *   "FF:START\n"  → Scan starten (Numpad geöffnet)
 *   "FF:STOP\n"   → Scan stoppen (Numpad geschlossen)
 *
 * Protokoll ausgehend (Arduino → Unity):
 *   "05:0"…"05:9"  Ziffer
 *   "05:DEL"        * gedrückt (löschen)
 *   "05:ENT"        # gedrückt (bestätigen)
 *   "RDY"           Board bereit (nach Reset)
 *
 * Library: "Keypad" by Mark Stanley & Alexander Brevig
 */

#include <Keypad.h>

// Onboard-Komponenten des Control Board V4
#define BUZZER_PIN 8   // D8 = aktiver Buzzer  (Col3 im Keypad-Mapping)
#define RELAY_PIN  4   // D4 = Relay           (Row3 im Keypad-Mapping)

const byte ROWS = 4;
const byte COLS = 4;

char keys[ROWS][COLS] = {
  { '1', '2', '3', 'A' },
  { '4', '5', '6', 'B' },
  { '7', '8', '9', 'C' },
  { '*', '0', '#', 'D' }
};

// Standard-Belegung laut Freenove FNK0059-Doku: Rows D2–D5, Cols D6–D9
byte rowPins[ROWS] = { 2, 3, RELAY_PIN, 5 };   // D4 = Relay steckt in Row3
byte colPins[COLS] = { 6, 7, BUZZER_PIN, 9 };  // D8 = Buzzer steckt in Col3

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, ROWS, COLS);

bool scanning = false;
String serialBuf = "";

// ── D8 + D4 explizit LOW halten → kein Piepen, kein Relay-Klicken ──────────
void silenceBoard()
{
  pinMode(BUZZER_PIN, OUTPUT); digitalWrite(BUZZER_PIN, LOW);
  pinMode(RELAY_PIN,  OUTPUT); digitalWrite(RELAY_PIN,  LOW);
}

// ── Setup ────────────────────────────────────────────────────────────────────
void setup()
{
  silenceBoard();
  Serial.begin(115200);
  keypad.setDebounceTime(30);
  keypad.setHoldTime(500);
  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);
  delay(1500);   // DTR-Reset abklingen lassen
  silenceBoard();
  Serial.println("RDY");
}

// ── Unity-Befehle lesen ──────────────────────────────────────────────────────
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

// ── Loop ─────────────────────────────────────────────────────────────────────
void loop()
{
  handleSerial();

  if (!scanning)
  {
    silenceBoard();   // Im Idle kein Piepen, kein Relay
    return;
  }

  char key = keypad.getKey();
  silenceBoard();   // D4 + D8 sofort nach Keypad-Scan zurück auf LOW

  if (!key) return;

  // LED-Feedback
  digitalWrite(LED_BUILTIN, HIGH);
  delay(30);
  digitalWrite(LED_BUILTIN, LOW);
  silenceBoard();   // auch nach LED-Delay

  // Senden
  if (key >= '0' && key <= '9')
  {
    Serial.print("05:"); Serial.println(key);
  }
  else if (key == '*') { Serial.println("05:DEL"); }
  else if (key == '#') { Serial.println("05:ENT"); }
}
