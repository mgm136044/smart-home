// ESP32 펌웨어 설정. 필요한 값만 바꾸면 돼요.
#pragma once

// ── 허브 접속 ─────────────────────────────────────────────
#define DEVICE_ID           "esp32-01"
#define HUB_TOKEN           "change-me"      // PC 허브 hubsettings.json의 Token과 같아야 해요
#define DEFAULT_HUB_IP      "192.168.0.10"   // 처음 값. 설정 화면이나 USB 명령으로 바꿀 수 있어요
#define DEFAULT_HUB_PORT    5001

// ── Wi-Fi 설정 화면 (WiFiManager) ─────────────────────────
// 저장된 Wi-Fi에 못 붙으면 ESP32가 이 이름의 Wi-Fi를 열어요. 폰으로 접속해서 SSID/비밀번호/허브 IP 입력
#define SETUP_AP_NAME       "SmartHome-Setup"
#define SETUP_AP_PASSWORD   "smarthome"      // 8자 이상
#define PORTAL_TIMEOUT_S    180              // 3분 동안 설정이 없으면 재부팅해서 다시 시도

// ── 무선 업로드 (OTA) ─────────────────────────────────────
#define OTA_HOSTNAME        "smarthome-esp32"
#define OTA_PASSWORD        "smarthome-ota"

// ── 동작 모드 ─────────────────────────────────────────────
// 1: 명령을 Arduino로 중계 (실제 구성)
// 0: ESP32 단독 테스트. Arduino 없이 가상 장치처럼 ACK와 센서값을 만들어요
#ifndef BRIDGE_TO_ARDUINO
#define BRIDGE_TO_ARDUINO   1
#endif

// ── Arduino 연결 (UART2) ──────────────────────────────────
// ESP32 GPIO17(TX2) → Arduino RX(D4)       : 3.3V 신호, 그대로 연결 가능
// ESP32 GPIO16(RX2) ← Arduino TX(D5)       : 5V 신호! 분압 저항(예: 1kΩ/2kΩ)이나 레벨 시프터 필수
// GND끼리 반드시 연결
#define ARDUINO_RX_PIN      16
#define ARDUINO_TX_PIN      17
#define ARDUINO_BAUD        9600
#define ARDUINO_SILENCE_MS  3000             // 이 시간 동안 Arduino에서 아무 줄도 없으면 '응답 없음'

// ── 기타 ──────────────────────────────────────────────────
#define STATUS_LED_PIN      2                // 보드 LED: 허브 연결 시 켜짐 (보드마다 다를 수 있어요)
#define CONFIG_BUTTON_PIN   0                // BOOT 버튼
#define CONFIG_WINDOW_MS    2000             // 부팅 직후 이 시간 안에 BOOT를 누르면 Wi-Fi 설정 초기화
#define RECONNECT_MS        3000             // 허브 재접속 간격
#define HUB_SILENCE_MS      15000            // 허브에서 이 시간 동안 아무것도 없으면 재접속
#define STATUS_INTERVAL_MS  2000             // #STATUS 보내는 간격 (허브의 생존 확인용)
