 /* 

 * Optimierter Scanner für Freenove V4 Board

 * Nutzt INPUT_PULLUP für Stabilität und minimiert Konflikte.

 */


#define BUZZER_PIN  8    

#define RELAY_PIN   4    

#define BEEP_MS     80   

#define DEBOUNCE_MS 40   // Etwas erhöht für mechanische Membrane


const byte ROWS = 4; 

const byte COLS = 4; 


char keys[ROWS][COLS] = { 

  { '1', '2', '3', 'A' }, 

  { '4', '5', '6', 'B' }, 

  { '7', '8', '9', 'C' }, 

  { '*', '0', '#', 'D' } 

}; 


byte rowPins[ROWS] = { 2, 3, RELAY_PIN, 5 }; 

byte colPins[COLS] = { 6, 7, BUZZER_PIN, 9 }; 


bool scanning = false; 

String serialBuf = ""; 


static char _lastRaw = 0;

static char _stableKey = 0;

static unsigned long _firstSeen = 0;


void silenceBoard() {

  // Alle Spalten auf INPUT (High Impedance), um Buzzer/Relay nicht zu treiben

  for(int i=0; i<COLS; i++) pinMode(colPins[i], INPUT);

  // Zeilen auf INPUT_PULLUP für definierten HIGH Zustand

  for(int i=0; i<ROWS; i++) pinMode(rowPins[i], INPUT_PULLUP);

}


void beep() {

  pinMode(BUZZER_PIN, OUTPUT);

  digitalWrite(BUZZER_PIN, HIGH); 

  delay(BEEP_MS); 

  digitalWrite(BUZZER_PIN, LOW);

  pinMode(BUZZER_PIN, INPUT); // Zurück in neutralen Zustand

}


char rawScan() {

  for (byte c = 0; c < COLS; c++) {

    // Aktiviere Spalte: Als OUTPUT auf LOW ziehen

    pinMode(colPins[c], OUTPUT);

    digitalWrite(colPins[c], LOW);

    

    delayMicroseconds(20); // Etwas mehr Zeit zum Stabilisieren


    for (byte r = 0; r < ROWS; r++) {

      // Wenn eine Taste gedrückt ist, wird die Zeile durch die Spalte auf LOW gezogen

      if (digitalRead(rowPins[r]) == LOW) {

        char key = keys[r][c];

        // Spalte sofort wieder neutralisieren

        digitalWrite(colPins[c], HIGH);

        pinMode(colPins[c], INPUT);

        return key;

      }

    }

    // Spalte zurück auf INPUT (Zustand: neutral/schwebend)

    pinMode(colPins[c], INPUT);

  }

  return 0; 

}


char getKey() {

  char raw = rawScan();

  unsigned long now = millis();


  if (raw != _lastRaw) {

    _lastRaw = raw;

    _firstSeen = now;

    return 0;

  }


  if (raw && (raw != _stableKey) && (now - _firstSeen >= DEBOUNCE_MS)) {

    _stableKey = raw;

    return raw;

  }


  if (!raw) _stableKey = 0;

  return 0;

}


void setup() {

  Serial.begin(115200);

  silenceBoard();

  pinMode(LED_BUILTIN, OUTPUT);

  Serial.println("RDY");

}


void handleSerial() {

  while (Serial.available()) {

    char c = Serial.read();

    if (c == '\n') {

      serialBuf.trim();

      if (serialBuf == "FF:START") scanning = true;

      if (serialBuf == "FF:STOP")  { scanning = false; silenceBoard(); }

      serialBuf = "";

    } else if (serialBuf.length() < 32) {

      serialBuf += c;

    }

  }

}


void loop() {

  handleSerial();

  if (!scanning) return;


  char key = getKey();

  if (key) {

    beep();

    digitalWrite(LED_BUILTIN, HIGH);

    

    if (key >= '0' && key <= '9') {

      Serial.print("05:"); Serial.println(key);

    } else if (key == '*') {

      Serial.println("05:DEL");

    } else if (key == '#') {

      Serial.println("05:ENT");

    }

    

    delay(50); 

    digitalWrite(LED_BUILTIN, LOW);

  }

} 