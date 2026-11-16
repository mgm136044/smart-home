// SmartHomeEsp32.ino
// ESP32 통신 게이트웨이
//  - PC 허브와 Wi-Fi TCP(기본) 또는 USB 시리얼(백업)로 줄 프로토콜을 주고받아요
//  - 명령(@...)은 Arduino로 중계하고, Arduino의 응답/센서값(#...)은 PC로 올려요
//  - 2초마다 #STATUS(Wi-Fi 신호, Arduino 응답 여부)를 보내요
// 규격: docs/protocol.md   배선: docs/wiring.md
//
// 필요한 라이브러리: WiFiManager (tzapu, 2.x)  ← 라이브러리 매니저에서 "WiFiManager" 검색
// 보드: ESP32 Dev Module (esp32 by Espressif 3.x)
//
// USB 시리얼 모니터(115200)에서 쓸 수 있는 설정 명령:
//   CFG,HUB,192.168.0.10,5001   허브 주소 바꾸기 (저장됨)
//   CFG,SHOW                    현재 설정 보기
//   CFG,WIFIRESET               저장된 Wi-Fi 지우고 재부팅 (설정 화면이 다시 열려요)

#include <WiFi.h>
#include <WiFiManager.h>
#include <Preferences.h>
#include <ArduinoOTA.h>
#include "config.h"
#include "line_buffer.h"

static Preferences prefs;
static WiFiClient hub;
static char hubIp[40] = DEFAULT_HUB_IP;
static uint16_t hubPort = DEFAULT_HUB_PORT;

static LineBuffer netBuf = {}, usbBuf = {}, ardBuf = {};

static unsigned long lastConnectTry = 0, lastHubRx = 0, lastStatus = 0;
static bool wasHubConnected = false;

#if BRIDGE_TO_ARDUINO
static unsigned long lastArduinoRx = 0;
static bool arduinoOnline = false;
#else
// ESP32 단독 테스트용 가상 장치
static bool simFan = false, simLight = false, simDoor = false;
static int simCurtain = 0;
static float simTemp = 24.0f, simHum = 45.0f, simLux = 420.0f;
static unsigned long lastSimSensor = 0;
#endif

// ───────────────────────── 출력 ─────────────────────────

// PC로 가는 줄은 연결된 통로(TCP, USB) 모두로 보내요. 허브가 지금 쓰는 통로만 골라서 읽어요.
static void sendToPc(const char* line) {
  if (hub.connected()) {
    hub.print(line);
    hub.print('\n');
  }
  Serial.print(line);
  Serial.print('\n');
}

// "//"로 시작하는 줄은 허브가 무시해요 (USB 시리얼 모니터용 디버그 출력)
static void debugLog(const char* msg) {
  Serial.print("// ");
  Serial.println(msg);
}

// ───────────────────────── 줄 단위 입력 ─────────────────────────

static void feed(LineBuffer& b, char c, LineHandler handler) {
  if (c == '\r') return;
  if (c == '\n') {
    b.data[b.len] = '\0';
    if (b.len > 0) handler(b.data);
    b.len = 0;
    return;
  }
  if (b.len < sizeof(b.data) - 1) b.data[b.len++] = c;
  else b.len = 0;  // 너무 긴 줄은 버려요
}

// "@17,FAN,ON" → id=17, dev="FAN"
static bool parseIdDevice(const char* line, int* id, char* dev, size_t devLen) {
  if (line[0] != '@') return false;
  *id = atoi(line + 1);
  const char* c1 = strchr(line, ',');
  if (!c1) return false;
  const char* c2 = strchr(c1 + 1, ',');
  size_t n = c2 ? (size_t)(c2 - c1 - 1) : strlen(c1 + 1);
  if (n >= devLen) n = devLen - 1;
  memcpy(dev, c1 + 1, n);
  dev[n] = '\0';
  for (size_t i = 0; i < n; i++) dev[i] = toupper((unsigned char)dev[i]);
  return true;
}

// ───────────────────────── 설정 저장 ─────────────────────────

static void loadConfig() {
  prefs.begin("smarthome", false);
  String ip = prefs.getString("hub_ip", DEFAULT_HUB_IP);
  strlcpy(hubIp, ip.c_str(), sizeof(hubIp));
  hubPort = prefs.getUShort("hub_port", DEFAULT_HUB_PORT);
}

static void saveHub(const char* ip, uint16_t port) {
  strlcpy(hubIp, ip, sizeof(hubIp));
  hubPort = port;
  prefs.putString("hub_ip", hubIp);
  prefs.putUShort("hub_port", hubPort);
  hub.stop();  // 새 주소로 다시 접속
  lastConnectTry = 0;
}

static void printConfig() {
  char msg[160];
  snprintf(msg, sizeof(msg), "id=%s hub=%s:%u wifi=%s ip=%s rssi=%d mode=%s",
           DEVICE_ID, hubIp, hubPort, WiFi.SSID().c_str(), WiFi.localIP().toString().c_str(),
           WiFi.RSSI(), BRIDGE_TO_ARDUINO ? "bridge" : "standalone");
  debugLog(msg);
}

// ───────────────────────── 명령 처리 ─────────────────────────

#if !BRIDGE_TO_ARDUINO
static void simulateCommand(const char* line) {
  int id;
  char dev[16];
  if (!parseIdDevice(line, &id, dev, sizeof(dev))) return;

  char reply[96];
  if (strcmp(dev, "PING") == 0) {
    snprintf(reply, sizeof(reply), "#STATE,FAN=%d,LIGHT=%d,CURTAIN=%d,DOOR=%d", simFan, simLight, simCurtain, simDoor);
    sendToPc(reply);
    return;
  }

  // 동작과 값 꺼내기: @id,DEV,ACT[,VAL]
  char act[12] = "";
  int val = -1;
  const char* c2 = strchr(strchr(line, ',') + 1, ',');
  if (c2) {
    const char* c3 = strchr(c2 + 1, ',');
    size_t n = c3 ? (size_t)(c3 - c2 - 1) : strlen(c2 + 1);
    if (n >= sizeof(act)) n = sizeof(act) - 1;
    memcpy(act, c2 + 1, n);
    act[n] = '\0';
    for (size_t i = 0; i < n; i++) act[i] = toupper((unsigned char)act[i]);
    if (c3) val = atoi(c3 + 1);
  }

  bool ok = true;
  char state[8];
  if (!strcmp(dev, "FAN") && (!strcmp(act, "ON") || !strcmp(act, "OFF"))) {
    simFan = !strcmp(act, "ON");
    strcpy(state, simFan ? "ON" : "OFF");
  } else if (!strcmp(dev, "LIGHT") && (!strcmp(act, "ON") || !strcmp(act, "OFF"))) {
    simLight = !strcmp(act, "ON");
    strcpy(state, simLight ? "ON" : "OFF");
  } else if (!strcmp(dev, "DOOR") && (!strcmp(act, "OPEN") || !strcmp(act, "CLOSE"))) {
    simDoor = !strcmp(act, "OPEN");
    strcpy(state, simDoor ? "OPEN" : "CLOSED");
  } else if (!strcmp(dev, "CURTAIN") && !strcmp(act, "SET") && val >= 0 && val <= 100) {
    simCurtain = val;
    snprintf(state, sizeof(state), "%d", val);
  } else {
    ok = false;
  }

  if (ok) snprintf(reply, sizeof(reply), "#ACK,%d,%s,%s", id, dev, state);
  else snprintf(reply, sizeof(reply), "#ERR,%d,%s,BAD_COMMAND", id, dev);
  sendToPc(reply);
}

static void simulateSensors() {
  if (millis() - lastSimSensor < 1000) return;
  lastSimSensor = millis();
  simTemp = constrain(simTemp + (random(-10, 11) / 100.0f) + (simFan ? -0.04f : 0.02f), 18.0f, 32.0f);
  simHum = constrain(simHum + (random(-30, 31) / 100.0f) + (simFan ? -0.08f : 0.03f), 30.0f, 80.0f);
  simLux = constrain(simLux + random(-12, 13), 50.0f, 1000.0f);
  char line[80];
  snprintf(line, sizeof(line), "#SENS,T=%.1f,H=%.1f,L=%.0f,IR=0", simTemp, simHum, simLux);
  sendToPc(line);
}
#endif

// PC(TCP 또는 USB)에서 온 줄
static void handlePcLine(const char* line) {
  if (line[0] != '@') return;  // PC는 @로 시작하는 명령만 보내요
#if BRIDGE_TO_ARDUINO
  Serial2.print(line);
  Serial2.print('\n');
  if (!arduinoOnline) {
    // Arduino가 응답이 없으면 PC가 2초 기다리지 않게 바로 알려줘요 (PING은 STATUS가 대신함)
    int id;
    char dev[16];
    if (parseIdDevice(line, &id, dev, sizeof(dev)) && id != 0) {
      char reply[64];
      snprintf(reply, sizeof(reply), "#ERR,%d,%s,NO_ARDUINO", id, dev);
      sendToPc(reply);
    }
  }
#else
  simulateCommand(line);
#endif
}

// USB 시리얼에서 온 줄: 설정 명령(CFG,...) 또는 PC 명령
static void handleUsbLine(const char* line) {
  if (strncmp(line, "CFG,", 4) != 0) {
    handlePcLine(line);
    return;
  }
  if (!strcmp(line, "CFG,SHOW")) {
    printConfig();
  } else if (!strcmp(line, "CFG,WIFIRESET")) {
    debugLog("Wi-Fi 설정을 지우고 재부팅해요");
    WiFiManager wm;
    wm.resetSettings();
    delay(300);
    ESP.restart();
  } else if (!strncmp(line, "CFG,HUB,", 8)) {
    char ip[40];
    strlcpy(ip, line + 8, sizeof(ip));
    uint16_t port = hubPort;
    char* comma = strchr(ip, ',');
    if (comma) {
      *comma = '\0';
      port = (uint16_t)atoi(comma + 1);
    }
    saveHub(ip, port);
    printConfig();
  } else {
    debugLog("모르는 설정 명령. CFG,SHOW / CFG,HUB,ip,port / CFG,WIFIRESET");
  }
}

#if BRIDGE_TO_ARDUINO
// Arduino에서 온 줄: #으로 시작하면 PC로 올리고, 나머지(디버그 출력)는 USB로만
static void handleArduinoLine(const char* line) {
  lastArduinoRx = millis();
  if (line[0] == '#') {
    sendToPc(line);
  } else {
    Serial.print("// arduino: ");
    Serial.println(line);
  }
}
#endif

// ───────────────────────── Wi-Fi / 허브 연결 ─────────────────────────

static bool configButtonPressed() {
  // BOOT 버튼은 전원 켤 때 누르고 있으면 다운로드 모드로 들어가서, 부팅 직후 잠깐 동안만 확인해요
  unsigned long start = millis();
  while (millis() - start < CONFIG_WINDOW_MS) {
    digitalWrite(STATUS_LED_PIN, (millis() / 150) % 2);
    if (digitalRead(CONFIG_BUTTON_PIN) == LOW) return true;
    delay(10);
  }
  return false;
}

static void setupWifi() {
  WiFi.mode(WIFI_STA);
  WiFiManager wm;

  if (configButtonPressed()) {
    debugLog("BOOT 버튼 눌림: 저장된 Wi-Fi를 지우고 설정 화면을 열어요");
    wm.resetSettings();
  }

  char portStr[8];
  snprintf(portStr, sizeof(portStr), "%u", hubPort);
  WiFiManagerParameter pHub("hub_ip", "PC hub IP", hubIp, sizeof(hubIp) - 1);
  WiFiManagerParameter pPort("hub_port", "PC hub port", portStr, 6);
  wm.addParameter(&pHub);
  wm.addParameter(&pPort);
  wm.setConfigPortalTimeout(PORTAL_TIMEOUT_S);
  wm.setConnectTimeout(20);

  debugLog("Wi-Fi 연결 중 (실패하면 '" SETUP_AP_NAME "' 설정 화면이 열려요)");
  if (!wm.autoConnect(SETUP_AP_NAME, SETUP_AP_PASSWORD)) {
    debugLog("Wi-Fi 설정 시간 초과, 재부팅");
    delay(500);
    ESP.restart();
  }

  // 설정 화면에서 허브 주소를 바꿨으면 저장
  uint16_t newPort = (uint16_t)atoi(pPort.getValue());
  if (strcmp(pHub.getValue(), hubIp) != 0 || (newPort != 0 && newPort != hubPort)) {
    saveHub(pHub.getValue(), newPort != 0 ? newPort : hubPort);
  }

  WiFi.setSleep(false);  // 절전을 끄면 명령 응답 지연이 줄어요 (전류는 조금 늘어요)
  WiFi.setAutoReconnect(true);
}

static void setupOta() {
  ArduinoOTA.setHostname(OTA_HOSTNAME);
  ArduinoOTA.setPassword(OTA_PASSWORD);
  ArduinoOTA.onStart([]() { debugLog("OTA 업로드 시작"); });
  ArduinoOTA.onError([](ota_error_t e) {
    char msg[40];
    snprintf(msg, sizeof(msg), "OTA 오류 %u", (unsigned)e);
    debugLog(msg);
  });
  ArduinoOTA.begin();
}

static void maintainHub() {
  if (WiFi.status() != WL_CONNECTED) {
    if (hub.connected()) hub.stop();
    return;
  }

  if (!hub.connected()) {
    if (wasHubConnected) {
      wasHubConnected = false;
      debugLog("허브 연결 끊김");
    }
    if (millis() - lastConnectTry < RECONNECT_MS) return;
    lastConnectTry = millis();
    if (hub.connect(hubIp, hubPort, 1500)) {
      hub.setNoDelay(true);
      char hello[96];
      snprintf(hello, sizeof(hello), "#HELLO,%s,%s", DEVICE_ID, HUB_TOKEN);
      hub.print(hello);
      hub.print('\n');
      lastHubRx = millis();
      lastStatus = 0;  // 바로 STATUS도 보내요
      wasHubConnected = true;
      debugLog("허브 접속");
    }
    return;
  }

  while (hub.available()) {
    lastHubRx = millis();
    feed(netBuf, (char)hub.read(), handlePcLine);
  }
  if (millis() - lastHubRx > HUB_SILENCE_MS) {
    debugLog("허브 응답 없음, 다시 접속");
    hub.stop();
  }
}

static void sendStatus() {
  if (millis() - lastStatus < STATUS_INTERVAL_MS) return;
  lastStatus = millis();
  char line[96];
#if BRIDGE_TO_ARDUINO
  snprintf(line, sizeof(line), "#STATUS,ARDUINO=%d,RSSI=%d,UP=%lu", arduinoOnline ? 1 : 0, (int)WiFi.RSSI(), millis() / 1000);
#else
  snprintf(line, sizeof(line), "#STATUS,ARDUINO=1,SIM=1,RSSI=%d,UP=%lu", (int)WiFi.RSSI(), millis() / 1000);
#endif
  sendToPc(line);
}

// ───────────────────────── setup / loop ─────────────────────────

void setup() {
  Serial.begin(115200);
  pinMode(STATUS_LED_PIN, OUTPUT);
  pinMode(CONFIG_BUTTON_PIN, INPUT_PULLUP);
#if BRIDGE_TO_ARDUINO
  Serial2.begin(ARDUINO_BAUD, SERIAL_8N1, ARDUINO_RX_PIN, ARDUINO_TX_PIN);
#endif
  delay(200);
  debugLog("SmartHome ESP32 시작");

  loadConfig();
  setupWifi();
  setupOta();
  printConfig();
}

void loop() {
  ArduinoOTA.handle();
  maintainHub();

  while (Serial.available()) feed(usbBuf, (char)Serial.read(), handleUsbLine);

#if BRIDGE_TO_ARDUINO
  while (Serial2.available()) feed(ardBuf, (char)Serial2.read(), handleArduinoLine);
  bool online = lastArduinoRx != 0 && millis() - lastArduinoRx < ARDUINO_SILENCE_MS;
  if (online != arduinoOnline) {
    arduinoOnline = online;
    debugLog(online ? "Arduino 연결됨" : "Arduino 응답 없음");
    lastStatus = 0;  // 바뀌면 바로 알려요
  }
#else
  simulateSensors();
#endif

  sendStatus();
  digitalWrite(STATUS_LED_PIN, hub.connected() ? HIGH : LOW);
  delay(1);
}
