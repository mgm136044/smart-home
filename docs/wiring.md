# 배선 (참고안)

펌웨어 기본값 기준이에요. 실제 배선이 다르면 `firmware/arduino/SmartHomeUno/SmartHomeUno.ino`와
`firmware/esp32/SmartHomeEsp32/config.h`의 설정 부분만 바꾸면 돼요.

## ESP32 ↔ Arduino Uno (UART)

| ESP32 | 방향 | Arduino Uno | 주의 |
|---|---|---|---|
| GPIO17 (TX2) | → | D4 (SoftwareSerial RX) | 3.3V 신호, 그대로 연결 |
| GPIO16 (RX2) | ← | D5 (SoftwareSerial TX) | **5V 신호라 분압 필수** |
| GND | — | GND | 반드시 공통 |

분압 예시 (Uno TX 5V → ESP32 RX 3.3V):

```text
Uno D5 ──[1kΩ]──┬── ESP32 GPIO16
                [2kΩ]
                 │
                GND
```

- 속도 9600bps, 줄 프로토콜 그대로 (`docs/protocol.md`)
- Uno의 USB 시리얼(D0/D1)은 업로드와 디버그용으로 비워둬요.

## Arduino Uno 핀

| 부품 | 핀 | 비고 |
|---|---|---|
| 온습도 DHT11 (DATA) | D2 | DHT22면 `DHT_TYPE` 변경. 모듈형은 풀업 저항 내장 |
| 적외선 센서 (OUT) | D3 | 감지 시 LOW인 모듈 기준 (`IR_ACTIVE_LOW`) |
| 조도 센서 (LDR 분압) | A0 | 0~1023. 밝을수록 작아지면 `LDR_INVERT 1` |
| 팬 | D6 | **트랜지스터/MOSFET 또는 릴레이로 구동**, 핀 직결 금지. 모터면 역방향 다이오드 |
| 전등 릴레이 (IN) | D7 | LOW에서 켜지는 모듈 기준 (`LIGHT_ACTIVE_LOW`) |
| 문 서보 (신호) | D9 | 열림 90°, 닫힘 0° (`DOOR_*_ANGLE`) |
| 커튼 스텝모터 ULN2003 IN1~IN4 | D8, D10, D11, D12 | 28BYJ-48 기준. 끝까지 걸음 수 `CURTAIN_FULL_STEPS` 실측 |

## 전원

- 서보, 스텝모터, 팬은 **별도 5V 전원**에서 받고 GND만 Arduino와 공통으로 묶어요. Arduino 5V 핀으로 모터를 돌리면 리셋/오동작이 나요.
- ESP32는 5V 2A 어댑터(USB)로 따로. Wi-Fi 송신 순간 전류가 커서 약한 전원이면 재부팅(brownout)돼요.
- 커튼은 전원을 켤 때 **닫힘(0%) 상태**라고 가정해요. 리밋 스위치를 달면 부팅 시 원점 잡기를 추가하면 돼요.

## 시험 순서

1. **Arduino 단독**: Uno를 USB로 연결, 시리얼 모니터 115200에서 `@1,FAN,ON` 입력 → `#ACK,1,FAN,ON`과 1초마다 `#SENS,...` 확인
2. **ESP32 단독 모드**: `config.h`에서 `BRIDGE_TO_ARDUINO 0`으로 올리고 허브에 접속되는지 확인 (허브 화면에 "ESP32 단독 모드")
3. **연결**: `BRIDGE_TO_ARDUINO 1`로 바꾸고 UART 배선 → 허브 화면의 "Arduino 응답 없음"이 사라지는지 확인
