/*
 * Big Yahu – Level 1 Keypad (Freenove Projects Kit FNK0059 / Control Board V4)
 *
 * VERKABELUNG (Keypad-Pins 1–8 → Arduino):
 *   Pin 1 (Row1) → D2     Pin 5 (Col1) → D6
 *   Pin 2 (Row2) → D3     Pin 6 (Col2) → D7
 *   Pin 3 (Row3) → A0  ← GEÄNDERT (war D4 = Relay-Konflikt!)
 *   Pin 4 (Row4) → D5     Pin 7 (Col3) → A1  ← GEÄNDERT (war D8 = Buzzer-Konflikt!)
 *                          Pin 8 (Col4) → D9
 *
 * Onboard-Komponenten bleiben frei:
 *   D4 = Relay  (aktiv HIGH) – jetzt ausschließlich für kontrollierten Feedback
 *   D8 = Buzzer (aktiv HIGH) – jetzt ausschließlich für einheitlichen Piepton
 *
 * Protokoll eingehend (Unity → Arduino):
 *   "FF:START\n"  → Scan starten (Numpad geöffnet)
 *   "FF:STOP\n"   → Scan stoppen (Numpad geschlossen)
 *
 * Protokoll ausgehend (Arduino → Unity):
 *   "05:0"…"05:9"  Ziffer
 *   "05:DEL"        * gedrückt
 *   "05:ENT"        # gedrückt
 *   "RDY"           Board bereit
 *
 * Library: "Keypad" by Mark Stanley & Alexander Brevig
 */

#include <Keypad.h>

#define BUZZER_PIN  8    // D8 – aktiver Buzzer (NICHT im Keypad-Matrix)
#define RELAY_PIN   4    // D4 – Relay          (NICHT im Keypad-Matrix)
#define BEEP_MS     80   // Einheitliche Piepton-Länge in ms

const byte ROWS = 4;
const byte COLS = 4;

char keys[ROWS][COLS] = {
  { '1', '2', '3', 'A' },
  { '4', '5', '6', 'B' },
  { '7', '8', '9', 'C' },
  { '*', '0', '#', 'D' }
};

// Row3 auf A0, Col3 auf A1 – D4 und D8 bleiben außerhalb der Matrix
byte rowPins[ROWS] = { 2,  3,  A0, 5  };
byte colPins[COLS] = { 6,  7,  A1, 9  };

Keypad keypad = Keypad(makeKeymap(keys), rowPins, colPins, ROWS, COLS);

bool   scanning  = false;
String serialBuf = "";

// ── D8 (Buzzer) + D4 (Relay) LOW halten ─────────────────────────────────────
void silenceBoard()
{
  pinMode(BUZZER_PIN, OUTPUT); digitalWrite(BUZZER_PIN, LOW);
  pinMode(RELAY_PIN,  OUTPUT); digitalWrite(RELAY_PIN,  LOW);
}

// ── Einheitlicher Piepton ────────────────────────────────────────────────────
void beep()
{
  digitalWrite(BUZZER_PIN, HIGH);
  delay(BEEP_MS);
  digitalWrite(BUZZER_PIN, LOW);
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
  delay(1500);
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
    silenceBoard();
    return;
  }

  char key = keypad.getKey();
  if (!key) return;

  // Einheitlicher Piepton + LED-Feedback bei jeder Taste
  beep();
  digitalWrite(LED_BUILTIN, HIGH);
  delay(30);
  digitalWrite(LED_BUILTIN, LOW);

  // Senden
  if (key >= '0' && key <= '9')
  {
    Serial.print("05:"); Serial.println(key);
  }
  else if (key == '*') { Serial.println("05:DEL"); }
  else if (key == '#') { Serial.println("05:ENT"); }
}
