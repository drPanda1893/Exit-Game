// =============================================================================
// ArduinoTestSketch – Prison Break Exit-Game
// Board: Freenove Control Board V5 Wifi (FNK0096)
// =============================================================================
//
// Protokoll (text-basiert, zeilengetrennt):
//   Unity → Arduino : "XX:payload\n"    (XX = 2-stelliger Hex-Befehlscode)
//   Arduino → Unity : "XX:payload\n"
//
// Befehle von Unity:
//   FF:ping     → Antwortet mit "FF:pong"
//   60:start    → Startet Temperatur-Simulation (Level 6 Test)
//   60:stop     → Stoppt Temperatur-Simulation
//
// Arduino sendet selbstständig:
//   FF:ready    → direkt nach setup()
//   60:TEMP:N   → alle 200 ms während Temp-Simulation läuft
//
// =============================================================================

// Befehlscode-Definitionen (müssen mit ArduinoBridge.cs übereinstimmen)
#define CMD_PING     0xFF
#define CMD_HUMIDITY 0x10
#define CMD_JOYSTICK 0x11
#define CMD_COLOR    0x20
#define CMD_BREADBOARD 0x50
#define CMD_TEMP     0x60

// Onboard-LED (meist Pin 13 oder LED_BUILTIN)
// Freenove Control Board V5: falls LED_BUILTIN nicht definiert, 2 oder 13 probieren
#ifndef LED_BUILTIN
  #define LED_BUILTIN 2
#endif

// Temperatur-Simulation
bool  simTempActive  = false;
float simTempValue   = 0.0f;
unsigned long lastTempSend = 0;
const unsigned long TEMP_INTERVAL_MS = 200;

// ── Eingabe-Puffer ─────────────────────────────────────────────────────────
String inputBuffer = "";

// =============================================================================
// setup
// =============================================================================
void setup() {
  Serial.begin(115200);
  while (!Serial) { /* warte bis Serial bereit (wichtig bei USB-CDC-Boards) */ }

  pinMode(LED_BUILTIN, OUTPUT);
  digitalWrite(LED_BUILTIN, LOW);

  // Startsignal an Unity senden
  delay(500);  // kurze Pause damit Unity-Seite empfangsbereit ist
  sendMsg(CMD_PING, "ready");
}

// =============================================================================
// loop
// =============================================================================
void loop() {
  // Eingehende Zeichen lesen (nicht-blockierend)
  while (Serial.available() > 0) {
    char c = (char)Serial.read();
    if (c == '\n' || c == '\r') {
      if (inputBuffer.length() > 0) {
        handleLine(inputBuffer);
        inputBuffer = "";
      }
    } else {
      inputBuffer += c;
    }
  }

  // Temperatur-Simulation alle 200 ms
  if (simTempActive) {
    unsigned long now = millis();
    if (now - lastTempSend >= TEMP_INTERVAL_MS) {
      lastTempSend = now;
      simTempValue += 2.0f;
      if (simTempValue > 100.0f) simTempValue = 100.0f;
      // Format: "60:TEMP:72.5"
      Serial.print(CMD_TEMP, HEX);
      Serial.print(":TEMP:");
      Serial.println(simTempValue, 1);
    }
  }
}

// =============================================================================
// handleLine – parst "XX:payload" und ruft den passenden Handler auf
// =============================================================================
void handleLine(String line) {
  line.trim();
  if (line.length() < 2) return;

  // Befehlscode lesen (hexadezimal, erste 2 Zeichen)
  int colonIdx = line.indexOf(':');
  if (colonIdx < 1) return;

  String hexPart  = line.substring(0, colonIdx);
  String payload  = line.substring(colonIdx + 1);
  int    cmd      = (int) strtol(hexPart.c_str(), nullptr, 16);

  // LED-Blink als visuelles Feedback
  blinkLed(1, 30);

  switch (cmd) {

    // ── Ping ────────────────────────────────────────────────────────────────
    case CMD_PING:
      sendMsg(CMD_PING, "pong");
      break;

    // ── Temperatur (Level 6) ─────────────────────────────────────────────
    case CMD_TEMP:
      if (payload == "start") {
        simTempActive = true;
        simTempValue  = 0.0f;
        lastTempSend  = millis();
        sendMsg(CMD_TEMP, "ok");
      } else if (payload == "stop") {
        simTempActive = false;
        sendMsg(CMD_TEMP, "stopped");
      }
      break;

    // ── Platzhalter für spätere Sensoren ────────────────────────────────
    case CMD_HUMIDITY:
      // TODO: Humidity-Sensor einlesen (Level 2a)
      sendMsg(CMD_HUMIDITY, "TODO");
      break;

    case CMD_JOYSTICK:
      // TODO: Joystick-Eingabe (Level 2b)
      sendMsg(CMD_JOYSTICK, "TODO");
      break;

    case CMD_COLOR:
      // TODO: Farbsensor einlesen (Level 3)
      sendMsg(CMD_COLOR, "TODO");
      break;

    case CMD_BREADBOARD:
      // TODO: Breadboard-Schaltkreis prüfen (Level 5)
      sendMsg(CMD_BREADBOARD, "TODO");
      break;

    default:
      // Unbekannten Befehl zurückmelden
      Serial.print("00:unknown:");
      Serial.println(hexPart);
      break;
  }
}

// =============================================================================
// Hilfsfunktionen
// =============================================================================

// Sendet "XX:payload\n" mit führenden Nullen im Hex-Teil
void sendMsg(int cmd, String payload) {
  if (cmd < 0x10) Serial.print("0");
  Serial.print(cmd, HEX);
  Serial.print(":");
  Serial.println(payload);
}

// LED N-mal blinken lassen
void blinkLed(int times, int ms) {
  for (int i = 0; i < times; i++) {
    digitalWrite(LED_BUILTIN, HIGH);
    delay(ms);
    digitalWrite(LED_BUILTIN, LOW);
    if (i < times - 1) delay(ms);
  }
}
