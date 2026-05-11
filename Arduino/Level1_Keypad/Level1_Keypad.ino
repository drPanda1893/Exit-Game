/*
 * Big Yahu - Level 2 Humidity Sensor (DHT22 Version)
 * Pin: Signal -> A5 (wird hier als digitaler Pin genutzt)
 */

#include "DHT.h"

#define DHTPIN A5     
#define DHTTYPE DHT22 

DHT dht(DHTPIN, DHTTYPE);

const unsigned long SAMPLE_INTERVAL_MS = 2000; // DHT22 braucht Zeit zwischen den Messungen
const float HUMIDITY_THRESHOLD = 5.0;          // Anstieg um 5% Feuchtigkeit löst "Blow" aus

bool sensing = false;
bool successPrinted = false;
String serialBuffer = "";

float baseline = 0;
unsigned long lastSampleAt = 0;

void setup() {
  Serial.begin(115200);
  dht.begin();
  
  // Erste Messung als Baseline
  delay(2000); 
  baseline = dht.readHumidity();
  
  Serial.println("FF:ready");
}

void loop() {
  handleSerial();

  unsigned long now = millis();
  if (now - lastSampleAt < SAMPLE_INTERVAL_MS) return;
  lastSampleAt = now;

  float h = dht.readHumidity();

  // Fehlerprüfung (falls der Sensor nicht richtig steckt)
  if (isnan(h)) {
    Serial.println("10:ERR:Sensor_Read_Failed");
    return;
  }

  Serial.print("10:HUM:");
  Serial.println(h);

  // Wenn wir nicht aktiv "suchen", aktualisieren wir die Baseline langsam
  if (!sensing) {
    baseline = (baseline * 0.9) + (h * 0.1);
  } else {
    // Blow-Erkennung: Aktuelle Feuchtigkeit > Baseline + Schwellenwert
    if (h > (baseline + HUMIDITY_THRESHOLD)) {
      if (!successPrinted) {
        Serial.println("success");
        successPrinted = true;
      }
      Serial.print("10:BLOW:");
      Serial.println(h);
    }
  }
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
    // Wir nehmen den letzten Wert vor dem Start als frische Baseline
    baseline = dht.readHumidity(); 
    Serial.println("10:ready");
  } else if (command == "10:STOP") {
    sensing = false;
    Serial.println("10:stopped");
  } else if (command == "FF:PING") {
    Serial.println("FF:pong");
  }
}