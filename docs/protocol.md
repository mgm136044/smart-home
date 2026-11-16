# 통신 규격

PC 허브 ↔ ESP32 사이의 줄 단위 프로토콜과, 휴대폰/라즈베리파이가 쓰는 REST·WebSocket API예요.
코드 기준: `pc-hub/SmartHome.Hub/Core/Protocol.cs`, `Web/WebServer.cs`

---

## 1. PC ↔ ESP32 줄 프로토콜

- 한 줄에 메시지 하나, `\n`으로 끝나요 (`\r\n`도 허용).
- Wi-Fi TCP(PC:5001)와 USB 시리얼(115200bps)에서 똑같이 써요.
- PC → ESP32는 `@`로, ESP32 → PC는 `#`로 시작해요.
- 대소문자는 구분하지 않지만 대문자로 보내는 걸 권장해요.

### 역할 나눔

```text
PC 허브 ──(Wi-Fi TCP / USB)── ESP32 ──(UART 9600)── Arduino
```

- ESP32는 `@` 명령을 Arduino로 그대로 넘기고, Arduino의 `#` 줄을 PC로 그대로 올려요 (중계).
- ESP32가 직접 만드는 줄은 `#HELLO`, `#STATUS`, `#ERR,...,NO_ARDUINO`뿐이에요.
- ESP32 단독 모드(`BRIDGE_TO_ARDUINO 0`)에서는 ESP32가 가상 장치처럼 ACK/SENS/STATE를 만들어요.

### PC → ESP32 (명령)

```text
@<id>,<장치>,<동작>[,<값>]
```

| 장치 | 동작 | 예시 | 비고 |
|---|---|---|---|
| FAN | ON, OFF | `@17,FAN,ON` | |
| LIGHT | ON, OFF | `@18,LIGHT,OFF` | 릴레이 |
| CURTAIN | SET,0~100 | `@19,CURTAIN,SET,70` | 0 = 닫힘, 100 = 열림 |
| DOOR | OPEN, CLOSE | `@20,DOOR,OPEN` | 서보 |
| (PING) | - | `@0,PING` | id 0은 PING 전용 |

- id는 1~9999를 돌려 써요. 같은 명령을 다시 보낼 때(재시도)는 같은 id를 써요.
- 장치에는 항상 절대값만 보내요. 토글은 PC가 현재 상태를 보고 ON/OFF로 바꿔서 보내요.

### ESP32 → PC

| 메시지 | 예시 | 언제 |
|---|---|---|
| HELLO | `#HELLO,esp32-01,<토큰>` | TCP 접속 직후 첫 줄 (USB는 생략) |
| ACK | `#ACK,17,FAN,ON` | 명령 성공. 마지막 값은 **실제 상태** |
| ERR | `#ERR,19,CURTAIN,BAD_VALUE` | 명령 실패 + 이유 (아래 표) |
| SENS | `#SENS,T=24.5,H=41.0,L=530,IR=0` | 1초마다 (Arduino) |
| STATE | `#STATE,FAN=1,LIGHT=0,CURTAIN=70,DOOR=0` | PING 응답, 부팅 직후 (Arduino) |
| STATUS | `#STATUS,ARDUINO=1,RSSI=-55,UP=123` | 2초마다 (ESP32). Wi-Fi 신호, Arduino 응답 여부, 부팅 후 초. 단독 모드면 `SIM=1` |
| EVT | `#EVT,IR,DETECTED` / `#EVT,CURTAIN,DONE` | 비동기 이벤트 |
| (주석) | `// 허브 접속` | `//`로 시작하는 줄은 펌웨어 디버그 출력. 허브가 무시해요 |

ERR 이유:

| 이유 | 뜻 | 보내는 쪽 |
|---|---|---|
| NO_ARDUINO | Arduino가 3초 동안 아무 줄도 안 보냄 (배선/전원) | ESP32 |
| BAD_COMMAND | 모르는 장치나 동작 | Arduino |
| BAD_VALUE | 커튼 값이 0~100 밖 | Arduino |
| BUSY | 장치가 아직 동작 중 (예약) | Arduino |

커튼은 끝까지 움직이는 데 몇 초 걸려서, **목표를 받으면 바로 ACK**(목표 값)하고 다 움직이면 `#EVT,CURTAIN,DONE`을 보내요.

ACK의 상태 값:

| 장치 | 상태 값 |
|---|---|
| FAN, LIGHT | `ON` / `OFF` |
| CURTAIN | `0`~`100` |
| DOOR | `OPEN` / `CLOSED` |

SENS 키: `T` 온도(℃), `H` 습도(%), `L` 조도(원시값 또는 lux), `IR` 적외선 감지(0/1). 없는 키는 빼도 돼요.

### 연결 유지 규칙

- ESP32는 PC:5001로 접속하고 바로 `#HELLO`를 보내요. 6초 안에 HELLO가 없거나 토큰이 틀리면 PC가 끊어요.
- PC는 접속 직후와 5초마다 `@0,PING`을 보내고(TCP, USB 모두), Arduino가 `#STATE`로 답해요.
- PC는 6초 동안 아무 줄도 안 오면 끊김으로 처리해요. ESP32가 2초마다 보내는 `#STATUS`가 생존 신호라 Arduino가 빠져도 연결은 유지돼요.
- ESP32는 15초 동안 PC에서 아무것도 안 오면 연결을 끊고 3초 간격으로 다시 접속해요.
- PC는 ACK를 2초 기다리고, 없으면 같은 id로 한 번 더 보내요. 그래도 없으면 실패(timeout)예요.

### 통로 우선순위 (PC)

Wi-Fi TCP → USB 시리얼 → 가상 장치 순서로, 연결된 것 중 가장 앞의 것을 써요.
통로가 바뀌면 PC가 `@0,PING`을 보내서 전체 상태를 다시 받아요.

---

## 2. REST API (PC:5000)

| 메서드 | 경로 | 설명 |
|---|---|---|
| GET | `/api/state` | 장치 상태, 최신 센서값, ESP32 연결 상태 |
| POST | `/api/devices/{fan\|light\|curtain\|door}` | 명령. ACK를 받으면 응답 |
| GET | `/api/sensors?minutes=10` | 센서 이력 (1~60분, 1초 간격) |
| GET | `/api/logs` | 최근 로그 300개 |

명령 요청:

```http
POST /api/devices/curtain
Content-Type: application/json
X-Source: gesture

{"action":"set","value":70}
```

- `action`: `on`, `off`, `toggle`, `open`, `close`, `set`
- `value`: 커튼 `set`일 때 0~100
- `X-Source` 헤더: 명령 출처 (`phone`, `tablet`, `gesture`, `auto` …). 로그와 화면에 표시돼요.

응답:

```json
{"ok":true,"device":"CURTAIN","state":"70","error":null,"code":null,"ms":38,"source":"gesture"}
```

| HTTP | code | 의미 |
|---|---|---|
| 200 | null | 성공 |
| 400 | bad_request | 모르는 장치/동작, 커튼 값 범위 오류 |
| 502 | device_error | ESP32가 `#ERR`로 답함 |
| 503 | offline | ESP32 연결 없음 |
| 504 | timeout | 재시도까지 ACK 없음 |

---

## 3. WebSocket (`ws://PC:5000/ws`)

접속하면 바로 `state`를 한 번 보내고, 이후 변화가 있을 때마다 보내요. 메시지는 JSON이에요.

```json
{"type":"state","data":{"fan":true,"light":false,"curtain":70,"door":false,
  "sensors":{"time":"2026-11-16T15:25:22+09:00","temp":24.3,"hum":46.2,"lux":404,"ir":0},
  "connected":true,"link":"Wi-Fi TCP esp32-01 192.168.0.20",
  "esp":{"rssi":-55,"arduino":true,"uptime":123,"simulated":false}}}
{"type":"sensor","data":{"time":"...","temp":24.3,"hum":46.2,"lux":404,"ir":0}}
{"type":"log","data":{"time":"...","kind":"CMD","text":"phone: FAN → ON (42ms)"}}
```
