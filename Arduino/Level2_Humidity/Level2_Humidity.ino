/*
 * Big Yahu - Level 2 Thermistor (Freenove Project Board)
 *
 * Sensor:
 *   Thermistor -> A0 (auf Freenove-Board fest verdrahtet)
 *
 * Unity -> Arduino:
 *   10:START  Aktiviert die Temperatur-Auswertung, sobald der Spieler die Wand mit E ansieht.
 *   10:STOP   Deaktiviert die Auswertung.
 *   FF:ping   Arduino antwortet mit FF:pong.
 *
 * Arduino -> Unity:
 *   10:TEMP:<celsius>   Laufender Messwert (Debug/Konsole).
 *   10:BLOW:<celsius>   Ausgeloest, sobald Temperatur > TEMP_THRESHOLD und sensing aktiv ist.
 *                       (Name "BLOW" beibehalten, damit Level2_DustWall.cs unveraendert bleibt.)
 *   success             Einmaliger Marker beim ersten Ueberschreiten.
 *   FF:ready            Wird einmal nach Setup gesendet.
 */

#define THERMISTOR_PIN     A0
#define SERIES_RESISTOR    10000.0
#define NOMINAL_RESISTANCE 10000.0
#define NOMINAL_TEMP       25.0
#define B_COEFFICIENT      3950.0

const unsigned long SAMPLE_INTERVAL_MS = 250;
const unsigned long BLOW_COOLDOWN_MS   = 400;
const float         TEMP_THRESHOLD     = 28.0;  // Grad Celsius

// Standardmaessig aktiv: Unity gated die Reaktion ohnehin nur im State Scratching.
// So bleibt das BLOW-Event nicht aus, falls 10:START vom Bridge verschluckt wird.
bool sensing = true;
bool successPrinted = false;
String serialBuffer = "";

unsigned long lastSampleAt = 0;
unsigned long lastBlowAt   = 0;

void setup() {
  Serial.begin(115200);
  Serial.println("FF:ready");
}

void loop() {
  handleSerial();

  unsigned long now = millis();
  if (now - lastSampleAt < SAMPLE_INTERVAL_MS) return;
  lastSampleAt = now;

  float tempC = readTemperatureC();

  Serial.print("10:TEMP:");
  Serial.println(tempC, 1);

  if (!sensing) return;
  if (tempC <= TEMP_THRESHOLD) return;
  if (now - lastBlowAt < BLOW_COOLDOWN_MS) return;

  if (!successPrinted) {
    Serial.println("success");
    successPrinted = true;
  }

  Serial.print("10:BLOW:");
  Serial.println((int)tempC);
  lastBlowAt = now;
}

float readTemperatureC() {
  int raw = analogRead(THERMISTOR_PIN);
  if (raw <= 0) return -273.15;

  float resistance = SERIES_RESISTOR / (1023.0 / (float)raw - 1.0);
  float tempK = 1.0 / (1.0 / (NOMINAL_TEMP + 273.15) +
                       log(resistance / NOMINAL_RESISTANCE) / B_COEFFICIENT);
  return tempK - 273.15;
}

void handleSerial() {
  while (Serial.available() > 0) {
    char c = (char)Serial.read();

    if (c == '\n' || c == '\r') {
      if (serialBuffer.length() > 0) {
        handleCommand(serialBuffer);
        serialBuffer = "";
      }
    } else if (serialBuffer.length() < 32) {
      serialBuffer += c;
    }
  }
}

void handleCommand(String command) {
  command.trim();
  command.toUpperCase();

  if (command == "10:START") {
    sensing = true;
    successPrinted = false;
    lastBlowAt = 0;
    Serial.println("10:ready");
  } else if (command == "10:STOP") {
    sensing = false;
    Serial.println("10:stopped");
  } else if (command == "FF:PING") {
    Serial.println("FF:pong");
  }
}
