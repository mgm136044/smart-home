// SmartHomeUno.ino
// Arduino 제어부 (참고 구현): ESP32에서 받은 명령으로 팬, 전등(릴레이), 커튼(스텝모터), 문(서보)을
// 움직이고, 1초마다 센서값(온습도, 조도, 적외선)을 ESP32로 보내요.
// 규격: docs/protocol.md   배선: docs/wiring.md
//
// ※ 핀 번호와 센서 종류는 아래 "설정" 부분을 실제 배선에 맞게 바꾸세요.
// 필요한 라이브러리: Servo, Stepper, DHT sensor library (Adafruit) + Adafruit Unified Sensor
//
// USB 시리얼 모니터(115200)에서도 같은 명령을 직접 쳐서 시험할 수 있어요. 예: @1,FAN,ON

#include <SoftwareSerial.h>
#include <Servo.h>
#include <Stepper.h>
#include <DHT.h>

// ═══════════════════════════ 설정 ═══════════════════════════
// ESP32 연결 (SoftwareSerial)
#define ESP_RX_PIN          4      // ← ESP32 GPIO17(TX2)
#define ESP_TX_PIN          5      // → ESP32 GPIO16(RX2)  ※ 5V→3.3V 분압 필수
#define ESP_BAUD            9600

// 센서
#define USE_DHT             1      // 온습도 센서가 없으면 0
#define DHT_PIN             2
#define DHT_TYPE            DHT11  // DHT22면 DHT22로
#define LDR_PIN             A0     // 조도 센서 (아날로그 0~1023)
#define LDR_INVERT          0      // 밝을수록 값이 작아지는 배선이면 1
#define IR_PIN              3      // 적외선 센서 (디지털)
#define IR_ACTIVE_LOW       1      // 감지 시 LOW를 내는 모듈이면 1 (장애물 감지 모듈은 대부분 LOW)

// 출력
#define FAN_PIN             6      // 트랜지스터/MOSFET/릴레이로 구동 (핀에 팬 직접 연결 금지)
#define FAN_ACTIVE_LOW      0
#define LIGHT_RELAY_PIN     7
#define LIGHT_ACTIVE_LOW    1      // 릴레이 모듈은 대부분 LOW에서 켜져요
#define DOOR_SERVO_PIN      9
#define DOOR_OPEN_ANGLE     90
#define DOOR_CLOSE_ANGLE    0

// 커튼: 28BYJ-48 + ULN2003 (IN1~IN4)
#define STEPPER_IN1         8
#define STEPPER_IN2         10
#define STEPPER_IN3         11
#define STEPPER_IN4         12
#define STEPPER_STEPS_REV   2048
#define STEPPER_RPM         12
#define CURTAIN_FULL_STEPS  4096L  // 완전히 닫힘(0%) → 열림(100%)까지 걸음 수. 실측해서 바꾸세요
#define CURTAIN_REVERSE     0      // 방향이 반대로 돌면 1
// 전원을 켤 때 커튼이 닫힌 상태(0%)라고 가정해요. 리밋 스위치를 달면 여기서 원점을 잡으면 돼요.

#define SENSOR_INTERVAL_MS  1000
#define DHT_INTERVAL_MS     2000
// ═══════════════════════════════════════════════════════════

SoftwareSerial esp(ESP_RX_PIN, ESP_TX_PIN);
Servo door;
// Stepper 라이브러리는 IN1, IN3, IN2, IN4 순서로 넣어야 28BYJ-48이 제대로 돌아요
Stepper curtainMotor(STEPPER_STEPS_REV, STEPPER_IN1, STEPPER_IN3, STEPPER_IN2, STEPPER_IN4);
#if USE_DHT
DHT dht(DHT_PIN, DHT_TYPE);
#endif

bool fanOn = false, lightOn = false, doorOpen = false;
int curtainPercent = 0;              // 목표 위치 (%)
long curtainPos = 0, curtainGoal = 0; // 걸음 수
bool curtainMoving = false;
float lastTemp = NAN, lastHum = NAN;
bool lastIr = false;
unsigned long lastSensor = 0, lastDht = 0;

char espLine[64];
uint8_t espLen = 0;
char usbLine[64];
uint8_t usbLen = 0;

// ─────────────────────── 출력 ───────────────────────

// ESP32로 보내고, 디버그용으로 USB에도 똑같이 찍어요
void sendLine(const char* s) {
  esp.print(s);
  esp.print('\n');
  Serial.println(s);
}

void setOutput(uint8_t pin, bool on, bool activeLow) {
  digitalWrite(pin, (on != activeLow) ? HIGH : LOW);
}

void releaseStepper() {  // 멈춰 있을 때 코일 전류를 끊어서 발열 방지
  digitalWrite(STEPPER_IN1, LOW);
  digitalWrite(STEPPER_IN2, LOW);
  digitalWrite(STEPPER_IN3, LOW);
  digitalWrite(STEPPER_IN4, LOW);
}

void sendState() {
  char out[64];
  snprintf(out, sizeof(out), "#STATE,FAN=%d,LIGHT=%d,CURTAIN=%d,DOOR=%d",
           fanOn ? 1 : 0, lightOn ? 1 : 0, curtainPercent, doorOpen ? 1 : 0);
  sendLine(out);
}

void upper(char* s) {
  for (; *s; s++) *s = toupper((unsigned char)*s);
}

// ─────────────────────── 명령 처리 ───────────────────────
// @id,DEVICE,ACTION[,VALUE]

void handleLine(char* line) {
  if (line[0] != '@') return;

  char* idStr = strtok(line + 1, ",");
  char* dev = strtok(NULL, ",");
  char* act = strtok(NULL, ",");
  char* valStr = strtok(NULL, ",");
  if (!idStr || !dev) return;

  int id = atoi(idStr);
  upper(dev);
  if (act) upper(act);

  if (!strcmp(dev, "PING")) {
    sendState();
    return;
  }

  char out[64];
  if (!act) {
    snprintf(out, sizeof(out), "#ERR,%d,%s,BAD_COMMAND", id, dev);
  } else if (!strcmp(dev, "FAN") && (!strcmp(act, "ON") || !strcmp(act, "OFF"))) {
    fanOn = !strcmp(act, "ON");
    setOutput(FAN_PIN, fanOn, FAN_ACTIVE_LOW);
    snprintf(out, sizeof(out), "#ACK,%d,FAN,%s", id, fanOn ? "ON" : "OFF");
  } else if (!strcmp(dev, "LIGHT") && (!strcmp(act, "ON") || !strcmp(act, "OFF"))) {
    lightOn = !strcmp(act, "ON");
    setOutput(LIGHT_RELAY_PIN, lightOn, LIGHT_ACTIVE_LOW);
    snprintf(out, sizeof(out), "#ACK,%d,LIGHT,%s", id, lightOn ? "ON" : "OFF");
  } else if (!strcmp(dev, "DOOR") && (!strcmp(act, "OPEN") || !strcmp(act, "CLOSE"))) {
    doorOpen = !strcmp(act, "OPEN");
    door.write(doorOpen ? DOOR_OPEN_ANGLE : DOOR_CLOSE_ANGLE);
    snprintf(out, sizeof(out), "#ACK,%d,DOOR,%s", id, doorOpen ? "OPEN" : "CLOSED");
  } else if (!strcmp(dev, "CURTAIN") && !strcmp(act, "SET") && valStr) {
    int v = atoi(valStr);
    if (v < 0 || v > 100) {
      snprintf(out, sizeof(out), "#ERR,%d,CURTAIN,BAD_VALUE", id);
    } else {
      // 커튼은 다 움직이는 데 몇 초 걸려서, 목표를 받으면 바로 ACK하고 다 움직이면 #EVT,CURTAIN,DONE
      curtainPercent = v;
      curtainGoal = CURTAIN_FULL_STEPS * v / 100;
      curtainMoving = (curtainGoal != curtainPos);
      snprintf(out, sizeof(out), "#ACK,%d,CURTAIN,%d", id, v);
    }
  } else {
    snprintf(out, sizeof(out), "#ERR,%d,%s,BAD_COMMAND", id, dev);
  }
  sendLine(out);
}

void readInto(Stream& s, char* buf, uint8_t& len, uint8_t cap) {
  while (s.available()) {
    char c = (char)s.read();
    if (c == '\r') continue;
    if (c == '\n') {
      buf[len] = '\0';
      if (len > 0) handleLine(buf);
      len = 0;
    } else if (len < cap - 1) {
      buf[len++] = c;
    } else {
      len = 0;  // 너무 긴 줄은 버려요
    }
  }
}

// ─────────────────────── 커튼 (멈추지 않게 한 걸음씩) ───────────────────────

void updateCurtain() {
  if (curtainPos == curtainGoal) {
    if (curtainMoving) {
      curtainMoving = false;
      releaseStepper();
      sendLine("#EVT,CURTAIN,DONE");
    }
    return;
  }
  int dir = curtainGoal > curtainPos ? 1 : -1;
  curtainMotor.step(CURTAIN_REVERSE ? -dir : dir);  // 한 걸음 (약 2.4ms)
  curtainPos += dir;
}

// ─────────────────────── 센서 ───────────────────────

void updateSensors() {
  unsigned long now = millis();

#if USE_DHT
  if (now - lastDht >= DHT_INTERVAL_MS) {
    lastDht = now;
    float t = dht.readTemperature();
    float h = dht.readHumidity();
    if (!isnan(t)) lastTemp = t;
    if (!isnan(h)) lastHum = h;
  }
#endif

  bool ir = digitalRead(IR_PIN) == (IR_ACTIVE_LOW ? LOW : HIGH);
  if (ir && !lastIr) sendLine("#EVT,IR,DETECTED");
  lastIr = ir;

  if (now - lastSensor >= SENSOR_INTERVAL_MS) {
    lastSensor = now;
    int light = analogRead(LDR_PIN);
    if (LDR_INVERT) light = 1023 - light;
    char out[64];
    if (isnan(lastTemp) || isnan(lastHum)) {
      snprintf(out, sizeof(out), "#SENS,L=%d,IR=%d", light, ir ? 1 : 0);
    } else {
      char t[8], h[8];
      dtostrf(lastTemp, 0, 1, t);  // Arduino의 snprintf는 %f를 못 써서 dtostrf 사용
      dtostrf(lastHum, 0, 1, h);
      snprintf(out, sizeof(out), "#SENS,T=%s,H=%s,L=%d,IR=%d", t, h, light, ir ? 1 : 0);
    }
    sendLine(out);
  }
}

// ─────────────────────── setup / loop ───────────────────────

void setup() {
  Serial.begin(115200);
  esp.begin(ESP_BAUD);

  pinMode(FAN_PIN, OUTPUT);
  pinMode(LIGHT_RELAY_PIN, OUTPUT);
  pinMode(IR_PIN, INPUT);
  pinMode(STEPPER_IN1, OUTPUT);
  pinMode(STEPPER_IN2, OUTPUT);
  pinMode(STEPPER_IN3, OUTPUT);
  pinMode(STEPPER_IN4, OUTPUT);
  setOutput(FAN_PIN, false, FAN_ACTIVE_LOW);
  setOutput(LIGHT_RELAY_PIN, false, LIGHT_ACTIVE_LOW);
  releaseStepper();

  door.attach(DOOR_SERVO_PIN);
  door.write(DOOR_CLOSE_ANGLE);
  curtainMotor.setSpeed(STEPPER_RPM);
#if USE_DHT
  dht.begin();
#endif

  Serial.println(F("// SmartHome Arduino 시작"));
  sendState();
}

void loop() {
  readInto(esp, espLine, espLen, sizeof(espLine));
  readInto(Serial, usbLine, usbLen, sizeof(usbLine));
  updateCurtain();
  updateSensors();
}
