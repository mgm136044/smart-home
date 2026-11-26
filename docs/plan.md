# 스마트홈 텀프로젝트 SW 구현 계획

- 작성: 2026-11-16 (초안, 팀 확정 전)
- SW: 민경민 (전담) / HW: 소석환, 박상원
- 구조도: `smarthome_architecture.html` (같은 폴더)
- 일정은 발표가 12월 중순이라는 가정으로 짰어요. 날짜가 다르면 주차만 옮기면 돼요.

---

## 0. 구현 현황 (2026-11-16)

코드: 이 저장소 (README.md 참고)

완료하고 테스트까지 한 것:
- [x] PC 허브: .NET 10 WinForms + 내장 웹 서버(Kestrel), 설정 파일, 로그 파일
- [x] 줄 프로토콜 파서, 상태 저장소, `CommandService`(명령 번호, ACK 매칭, 2초 타임아웃 + 1회 재시도, 토글 → 절대값)
- [x] 통로: Wi-Fi TCP(토큰, 5초 하트비트, 6초 무응답 감지), USB 시리얼 백업, 가상 장치, 우선순위 자동 전환
- [x] REST API + WebSocket 실시간 푸시 (SignalR 대신 기본 WebSocket: 대시보드와 Python에서 라이브러리 없이 쓰기 쉬움)
- [x] 휴대폰/아이패드 대시보드 (장치 4개, 센서 4개, 온습도 그래프, 최근 기록)
- [x] WinForms 화면 (연결 상태, 장치 제어, 센서, 온습도 그래프, 통신 로그)
- [x] ESP32 시뮬레이터 (`tools/Esp32Sim`): 하드웨어 없이 Wi-Fi TCP 경로 테스트

- [x] ESP32 펌웨어: WiFiManager(폰으로 Wi-Fi/허브 IP 설정), OTA, TCP 재접속, USB 백업, Arduino 중계, 2초마다 #STATUS, ESP32 단독 테스트 모드 (컴파일 확인, 플래시 81%)
- [x] Arduino Uno 참고 펌웨어: 팬/릴레이/서보/스텝(멈추지 않게 한 걸음씩), DHT/조도/적외선, 커튼 완료 이벤트 (컴파일 확인, 플래시 40%, RAM 41%)
- [x] 허브: ESP32 상태(신호, Arduino 응답) 표시, Arduino가 없으면 명령 즉시 실패, USB에도 하트비트
- [x] 제스처 앱(Python, MediaPipe Tasks 1.2): 상태머신 단위 테스트 14개 통과, 예제 사진 4장 인식 → 허브 → 장치까지 확인
- [x] 배선 참고안 `docs/wiring.md`, 라즈베리파이 설치/자동 실행 안내 `rpi/README.md`

남은 것:
- [ ] 실제 보드 업로드와 배선 시험 (HW팀과)
- [ ] 제스처 앱 실시간 확인: 웹캠 실행과 손 인식은 됨(11/16), 제스처 명령이 허브까지 가는 건 아직. 손바닥이 손가락 3개로 잘못 세어지는지 확인 필요
- [ ] 자동화 규칙, SQLite 저장, 브리핑

---
## 1. 핵심 결정

1. **PC 허브 중심**: 휴대폰, 라즈베리파이, 자동화 규칙, WinForms 버튼의 모든 명령이 PC의 `CommandService` 하나를 거쳐요. 장치 상태는 PC 한 곳에서 관리하고, 모든 화면에 같은 상태가 보여요.
2. **대시보드는 웹**: PC 허브가 웹페이지를 서빙해요. 아이폰, 안드로이드, 아이패드에서 앱 설치 없이 똑같이 써요.
3. **PC ↔ ESP32는 Wi-Fi TCP 기본, USB 시리얼은 백업**: 줄 단위 프로토콜은 같고 전달 통로만 바뀌어요(`ITransport`). 과제에서 SerialPort 사용이 필수라면 그 시연만 USB로 해요.
4. **1차 목표**: 2주차 안에 팬 시나리오 1~7을 끝까지 성공시키기.

---

## 2. 시스템 구조

```text
[휴대폰/아이패드] --Wi-Fi: REST + WebSocket--> [PC 허브 :5000]
[라즈베리파이 4]  --Wi-Fi: REST------------> [PC 허브 :5000]
[외부 API: 구글 캘린더, 기상청] --HTTPS--> [라즈베리파이 4]

[PC 허브 :5001] <--Wi-Fi TCP (USB 시리얼 = 백업)--> [ESP32]
[ESP32] --UART--> [Arduino] --GPIO--> [센서 / 액추에이터]

모든 무선 기기는 데모용 2.4GHz 공유기 하나에 연결
ESP32, Arduino, 센서/액추에이터는 하우스 모형 안 (5V 어댑터 전원)
```

| 구성요소 | 역할 | 기술 | 담당 |
|---|---|---|---|
| PC 허브 | 상태 저장소, 명령 처리, 자동화, 로그, 그래프, 웹 서버 | .NET 10 WinForms, ASP.NET Core(Kestrel) 내장, TcpListener, System.IO.Ports, SQLite, ScottPlot | 민경민 |
| 웹 대시보드 | 장치 제어, 실시간 센서값, 모드, 브리핑 화면 | HTML/JS, canvas 그래프, WebSocket (PC 허브의 wwwroot) | 민경민 |
| 라즈베리파이 | 제스처 인식, 명령 전송, 브리핑 음성 | Python venv, mediapipe Tasks API, OpenCV | 민경민 |
| ESP32 | PC와 통신, Arduino 중계, 센서값 회신 | Arduino-ESP32, WiFiManager, ArduinoOTA | HW (규격은 SW가 정의) |
| Arduino | 센서 읽기, 모터·릴레이 구동, 리밋/안전 | Arduino | HW |

---

## 3. 통신 규격

### 3.1 ESP32 줄 프로토콜 (TCP와 USB 공통)

한 줄에 메시지 하나, `\n`으로 끝나요. USB는 115200bps예요.

```text
[PC → ESP32]
@<id>,<장치>,<동작>[,<값>]
@17,FAN,ON
@18,LIGHT,OFF
@19,CURTAIN,SET,70        (커튼 0~100%)
@20,DOOR,OPEN
@0,PING                   (접속 직후 + 5초마다)

[ESP32 → PC]
#HELLO,esp32-01,<토큰>                  TCP 접속 직후 첫 줄
#ACK,17,FAN,ON                          성공 + 실제 상태
#ERR,19,CURTAIN,BUSY                    실패 + 이유
#SENS,T=24.5,H=41.0,L=530,IR=0          1초마다 센서값
#EVT,IR,DETECTED                        이벤트
#STATE,FAN=1,LIGHT=0,CURTAIN=70,DOOR=0  PING 응답, 부팅 시
```

연결 유지 규칙:
- ESP32가 PC:5001로 접속하고 바로 `#HELLO`를 보내요. 토큰이 틀리면 PC가 연결을 끊어요.
- PC는 접속 직후와 5초마다 `@0,PING`을 보내고, ESP32는 `#STATE`로 답해요.
- PC는 5초 동안 아무 줄도 안 오면 끊김으로 처리해요. 센서값이 1초마다 와서 이게 생존 신호 역할을 해요.
- ESP32는 15초 동안 PC에서 아무것도 안 오면 연결을 끊고 3초 간격으로 다시 접속해요.

### 3.2 REST API와 WebSocket

```text
GET  /api/state                             장치, 센서, 모드, ESP32 연결 상태
POST /api/devices/{fan|light|curtain|door}  {"action":"on"}  → ACK를 받으면 응답
POST /api/modes/{device}                    {"mode":"auto"|"manual"}
GET  /api/sensors?from=&to=                 그래프용 이력 (SQLite)
POST /api/briefing                          라즈베리파이 브리핑을 대시보드에 표시
WebSocket /ws → state, sensor, log (briefing은 추가 예정)
```

응답 예시: `{"ok":true,"device":"fan","state":"on","source":"phone","ms":84}`

### 3.3 명령 처리 규칙

- 명령마다 번호(id)를 붙이고, 같은 번호의 `#ACK`가 오면 완료로 처리해요. C#에서는 id별 `TaskCompletionSource`로 기다려요.
- 2초 안에 ACK가 없으면 한 번 다시 보내고, 그래도 없으면 실패로 응답해요.
- 장치에는 ON, OFF, SET 같은 절대값만 보내요. 토글 요청은 서버가 현재 상태를 보고 ON/OFF로 바꿔서 보내요. 그래야 다시 보내도 안전해요.
- 쓰기는 lock(SemaphoreSlim)으로 한 번에 하나씩만 해요.
- 상태가 바뀌면 WebSocket으로 모든 화면(폰, 아이패드, WinForms)에 보내요.

### 3.4 팬 시나리오 1~7 흐름

1. 폰에서 팬 ON 버튼 클릭
2. Wi-Fi로 `POST /api/devices/fan {"action":"on"}`
3. PC 허브(Kestrel)가 받아서 `CommandService` 호출
4. `transport.SendLineAsync("@17,FAN,ON")` (USB 모드면 `SerialPort.WriteLine`)
5. ESP32가 Arduino로 중계해서 팬 작동
6. ESP32가 `#ACK,17,FAN,ON` 회신
7. PC가 HTTP 응답을 보내고 WebSocket으로 모든 화면 갱신

---

## 4. 제스처 설계

MediaPipe 기본 제스처 7가지(Closed_Fist, Open_Palm, Pointing_Up, Thumb_Down, Thumb_Up, Victory, ILoveYou)를 쓰고, 손가락 개수만 랜드마크로 직접 계산해요.

| 단계 | 제스처 | 동작 |
|---|---|---|
| 깨우기 | Open_Palm 1초 유지 | 5초 동안 명령 대기 (화면/소리로 알림) |
| 전등 | Thumb_Up / Thumb_Down | ON / OFF |
| 팬 | Victory | 켜기/끄기 전환 |
| 커튼 | Pointing_Up / Closed_Fist | 열기 / 닫기 |
| 브리핑 | ILoveYou | 오늘 일정과 날씨 브리핑 |
| 문 | 손가락 3개 → Thumb_Up으로 확인 | 열기 (2단계 확인) |

오작동 방지:
- 인식 점수 0.6 이상
- 같은 제스처가 0.5초 이상 이어져야 실행
- 실행 후 1.5초 쉬기
- 한 손만 인식 (`num_hands=1`)

구현 메모:
- `mp.solutions`는 0.10.30~0.10.31부터 삭제됐어요. Tasks API의 `GestureRecognizer`만 써요.
- 노트북 웹캠으로 먼저 개발하고 라즈베리파이로 옮겨요.
- 라즈베리파이에서는 MJPEG, V4L2 백엔드, 버퍼 크기 1로 설정하고 방열판과 팬을 달아요.

---

## 5. 자동화 규칙 (숫자는 예시, 시연하면서 조정)

- **팬**: 28℃ 이상이거나 습도 70% 이상이면 켜고, 26℃ 이하이면서 65% 이하로 내려가면 꺼요. 켜는 기준과 끄는 기준을 다르게 둬서(히스테리시스) 기준값 근처에서 계속 켜졌다 꺼지는 걸 막아요.
- **커튼과 전등**: 조도 이동평균을 쓰고, 같은 상태가 10초 이상 이어질 때만 움직여요.
- **문**: 적외선 감지 시 열고, 5초 동안 감지가 없으면 닫아요. 센서 종류를 확인한 뒤 확정해요.
- **수동 우선**: 사람이 직접 조작한 장치는 자동 규칙을 10분 동안 멈춰요.
- **안전**: 리밋이나 안전 관련 처리는 Arduino에서 직접 해요.

---

## 6. 브리핑, 캘린더, 날씨

- **표시 위치 (기본안)**: 화면은 아이패드 대시보드, 음성은 라즈베리파이 스피커
- **캘린더**: 구글 캘린더 iCal 비밀 주소(로그인 없이 읽기 전용, 외부 공유 금지). 필요하면 OAuth Calendar API(데스크톱 앱)를 써요.
- **날씨**: 공공데이터포털 기상청 단기예보 조회서비스(무료, 자동승인, 개발계정 트래픽 10,000)
  - 현재 날씨는 `getUltraSrtNcst`, 예보는 `getVilageFcst`를 써요.
  - 격자 좌표 nx, ny는 활용가이드 엑셀에서 찾아요.
- **음성(TTS)**: edge-tts(`ko-KR-SunHiNeural`, 온라인)를 쓰고, 막히면 Piper(오프라인)로 바꿔요. 시연용 문장은 미리 mp3로 만들어 두는 것도 방법이에요.

---

## 7. 역할 분담

SW는 2026-11-16부터 민경민이 전부 맡아요.

| 담당 | 작업 |
|---|---|
| 민경민 (SW 전체) | PC 허브(C#), 웹 대시보드, ESP32 펌웨어 통신부, 라즈베리파이 제스처 앱(Python), 브리핑(캘린더, 날씨, TTS) |
| HW팀 (소석환, 박상원) | 배선, 센서·액추에이터 구동, Arduino 제어 로직, 하우스 모형 |
| 같이 | `docs/protocol.md` 확정, 핀맵, 통합 테스트, 데모 시나리오 |

---
## 8. 주차별 일정

### 1주차 (11/16~22): 기반 만들기
- PC 허브: .NET 10 WinForms + Kestrel 뼈대, `ITransport` 세 가지(가상 장치, TCP, 시리얼), `CommandService`(명령 번호, ACK, 타임아웃과 재시도)
- 라즈베리파이·대시보드: 라즈베리파이 64비트 OS + venv + mediapipe 설치, 노트북 웹캠으로 제스처 인식 시험, 버튼 4개짜리 대시보드 v0
- 같이: HW팀과 프로토콜/API 확정, 데모용 공유기 준비, ESP32 Wi-Fi 에코 테스트
- **완료 기준**: 폰 버튼 → PC → 가상 장치 ACK → 폰에 결과 표시

### 2주차 (11/23~29): 실제 하드웨어 연결
- PC 허브: 실제 ESP32와 TCP 연결, 센서값 → SQLite → WinForms 그래프, USB 백업 전환
- 라즈베리파이·대시보드: 제스처 상태머신(깨우기, 오인식 방지, 쉬는 시간) → 허브 REST로 명령, 라즈베리파이 FPS 확인, WebSocket 실시간 대시보드 v1
- 같이: 팬 시나리오 1~7 완주
- **완료 기준**: 폰과 제스처 둘 다 실제 팬을 켜고 끄고, 모든 화면에 같은 상태 표시

### 3주차 (11/30~12/6): 기능 완성
- PC 허브: 장치 4개 전부, 자동화 규칙과 모드, 이벤트 로그
- 라즈베리파이·대시보드: 브리핑(기상청, 캘린더, TTS), 대시보드 그래프와 브리핑 화면, 문 2단계 제스처
- 같이: 하우스 모형에 넣고 통합 (ESP32 전원 분리, OTA)
- **완료 기준**: 전체 시나리오가 모형에서 동작

### 4주차 (12/7~13): 안정화와 발표 준비
- 예외 처리 보강: 재접속, 타임아웃, 센서 이상값
- 공유기 재부팅 테스트, Wi-Fi 끊김 시 USB 전환 테스트
- 리허설 최소 3번, 발표 자료, 시연 영상 백업 촬영
- **완료 기준**: 리허설 연속 성공

### 12/14~: 버퍼

---

## 9. 일정이 밀릴 때 우선순위

1. **꼭 해야 하는 것**: 폰 → PC → ESP32 왕복, 센서 그래프, 제스처로 장치 4개 제어
2. **그다음**: 자동화 규칙, 날씨/일정 브리핑, 음성 출력
3. **여유 있으면**: 제스처 직접 학습(Model Maker), 대시보드 PIN, 사용 통계

---

## 10. 초기 설정 체크리스트

IP는 예시예요. 공유기 대역에 맞춰 바꾸면 돼요.

### 네트워크
- [ ] 데모용 공유기 하나 (2.4GHz 켜기, WPA2-PSK(AES), WPA3 전용은 피하기)
- [ ] 아이폰 핫스팟을 쓰면 설정 > 개인용 핫스팟 > '호환성 최대화' 켜기 (iPhone 12 이후)
- [ ] DHCP 예약: PC `192.168.0.10`, 라즈베리파이 `.11`, ESP32 `.20`
- [ ] 학교 Wi-Fi는 기기끼리 통신이 막혀 있을 수 있으니 쓰지 않기

### PC 허브
- [ ] 프로젝트 대상 버전 `net10.0-windows` (.NET 8은 2026-11-10에 지원 종료)
- [ ] csproj에 `<FrameworkReference Include="Microsoft.AspNetCore.App" />` 추가, Kestrel은 `http://0.0.0.0:5000`
- [ ] 관리자 PowerShell:
  ```powershell
  Set-NetConnectionProfile -InterfaceAlias "Wi-Fi" -NetworkCategory Private
  New-NetFirewallRule -DisplayName "SmartHome Hub" -Direction Inbound -Protocol TCP -LocalPort 5000,5001 -Action Allow -Profile Private
  ```
- [ ] 설정 파일: `Transport=Tcp`, 백업 COM 포트
- [ ] USB 시리얼을 쓸 때는 `DtrEnable=false`, `RtsEnable=false` (포트 열 때 ESP32 재부팅 방지)

### ESP32
- [ ] 첫 업로드만 USB (WiFiManager + ArduinoOTA 포함 펌웨어)
- [ ] 폰으로 `SmartHome-Setup` AP에 접속해서 SSID, 비밀번호, PC IP 입력
- [ ] 이후 펌웨어는 Arduino IDE 네트워크 포트(OTA)로 업로드
- [ ] `WiFi.setSleep(false)` (수신 지연 줄이기)
- [ ] 아날로그 입력은 ADC1 핀(GPIO32~39). Wi-Fi를 켜면 ADC2는 못 써요.
- [ ] 5V 2A 어댑터로 따로 전원, 모터 전원 분리

### 라즈베리파이
- [ ] Raspberry Pi OS 64비트
- [ ] `python -m venv`, `pip install mediapipe opencv-python`, `gesture_recognizer.task` 다운로드
- [ ] 설정 파일에 허브 주소 `http://192.168.0.10:5000`
- [ ] 방열판 + 팬

### 휴대폰 / 아이패드
- [ ] 같은 공유기에 접속, `http://192.168.0.10:5000` 열기, 홈 화면에 추가

### 연결 확인
- [ ] 허브 화면에 ESP32 '온라인', 센서값이 1초마다 갱신
- [ ] 폰에서 팬 ON → ACK와 지연(ms) 확인
- [ ] 공유기를 껐다 켜서 자동 재접속 확인
- [ ] Wi-Fi를 끊고 USB 백업으로 전환되는지 확인

### 외부 서비스
- [ ] 공공데이터포털에서 기상청 단기예보 조회서비스 활용신청
- [ ] 구글 캘린더 설정 > 캘린더 통합 > 'iCal 형식의 비밀 주소' 복사 (깃에 올리지 않기)

---

## 11. 리스크와 대응

| 리스크 | 대응 |
|---|---|
| 인터넷 예제와 AI 코드가 삭제된 `mp.solutions`를 씀 | Tasks API 공식 문서와 예제만 기준으로 삼기 |
| 학교 Wi-Fi에서 기기끼리 통신이 막힘 | 데모용 공유기 지참 |
| Wi-Fi 지연과 혼잡 | `setSleep(false)`, 덜 붐비는 채널, 공유기를 모형 가까이 |
| ESP32 brownout 재부팅 | 5V 2A 어댑터, 모터 전원 분리 |
| USB 포트를 열 때 ESP32 재부팅 | DTR/RTS 끄기, 접속 후 PING으로 상태 재동기화 |
| 라즈베리파이 발열로 FPS 저하 | 방열판 + 팬, 해상도 낮추기 |
| edge-tts가 막힘 | Piper로 전환하거나 미리 생성한 mp3 사용 |
| 하드웨어 완성이 늦어짐 | 가상 장치 모드로 SW 먼저 개발 |
| 발표장 네트워크 변경 | WiFiManager로 현장에서 다시 설정, USB 백업 |

---

## 12. HW팀 확인 사항 (괄호 안은 기본안)

- [ ] ESP32와 Arduino 연결 방식 (UART 중계, 3.3V/5V 레벨 변환)
- [ ] 적외선 센서 종류: 사람 감지용인지 리모컨 수신기인지, "릴레이 → 리모콘 제어"의 뜻
- [ ] 커튼 스텝모터 원점 잡는 방법 (리밋 스위치 유무)
- [ ] 5V 팬 구동 방식 (트랜지스터/MOSFET 또는 릴레이, 핀에 직접 연결 금지)
- [ ] ESP32 전원과 리빙박스 안 배치 (안테나를 금속으로 가리지 않기)
- [ ] 브리핑 표시 위치 (아이패드 대시보드 + 라즈베리파이 스피커)
- [ ] 과제에서 SerialPort 사용이 필수인지 (필수면 그 시연만 USB)
- [ ] 실제 발표 날짜

---

## 13. 저장소 구조 (제안)

```text
smart-home/
├─ docs/       protocol.md, api.md, gestures.md
├─ pc-hub/     C# .NET 10
│  ├─ Core/       DeviceState, CommandService, AutomationEngine
│  ├─ Transport/  ITransport, TcpTransport, SerialTransport, FakeTransport
│  ├─ Web/        REST 엔드포인트, WebSocket
│  ├─ UI/         MainForm, 그래프
│  └─ wwwroot/    웹 대시보드
├─ rpi/        gesture.py, state_machine.py, hub_client.py, briefing/, requirements.txt
└─ firmware/   esp32/, arduino/  (HW팀)
```

비밀값(토큰, iCal 주소, API 키, Wi-Fi 비밀번호)은 `.gitignore`에 넣은 설정 파일에만 둬요.

---

## 부록 A. ESP32 코드 뼈대

```cpp
#include <WiFi.h>
#include <WiFiManager.h>   // tzapu 2.x
#include <Preferences.h>
#include <ArduinoOTA.h>

Preferences prefs; WiFiClient hub;
String hubIp, rxNet, rxUsb;
const uint16_t HUB_PORT = 5001;
const char* TOKEN = "change-me";
unsigned long lastTry = 0, lastRx = 0, lastSens = 0;

void handleLine(const String& line, Print& out) {
  // "@17,FAN,ON" → Arduino로 중계(Serial2) 후 out.print("#ACK,17,FAN,ON\n");
  // "@0,PING"   → out.print("#STATE,FAN=1,LIGHT=0,CURTAIN=70,DOOR=0\n");
}
void readLines(Stream& s, String& buf, Print& out) {
  while (s.available()) { char c = s.read();
    if (c == '\n') { handleLine(buf, out); buf = ""; } else if (c != '\r') buf += c; }
}
void setup() {
  Serial.begin(115200);
  prefs.begin("cfg"); hubIp = prefs.getString("hub", "192.168.0.10");
  WiFiManager wm;
  WiFiManagerParameter pHub("hub", "PC 허브 IP", hubIp.c_str(), 16);
  wm.addParameter(&pHub); wm.setConfigPortalTimeout(180);
  if (!wm.autoConnect("SmartHome-Setup")) ESP.restart();
  hubIp = pHub.getValue(); prefs.putString("hub", hubIp);
  WiFi.setSleep(false);
  ArduinoOTA.setHostname("smarthome-esp32"); ArduinoOTA.begin();
}
void loop() {
  ArduinoOTA.handle();
  if (WiFi.status() == WL_CONNECTED && !hub.connected() && millis() - lastTry > 3000) {
    lastTry = millis();
    if (hub.connect(hubIp.c_str(), HUB_PORT)) {
      hub.setNoDelay(true); hub.printf("#HELLO,esp32-01,%s\n", TOKEN); lastRx = millis();
    }
  }
  if (hub.connected()) {
    if (hub.available()) lastRx = millis();
    readLines(hub, rxNet, hub);
    if (millis() - lastRx > 15000) hub.stop();      // PC가 조용하면 재접속
  }
  readLines(Serial, rxUsb, Serial);                 // USB 백업 경로
  if (millis() - lastSens > 1000) {
    lastSens = millis();
    String s = "#SENS,T=24.5,H=41.0,L=530,IR=0\n";  // 실제 값으로 교체
    if (hub.connected()) hub.print(s);
    Serial.print(s);
  }
}
```

## 부록 B. C# TcpTransport 뼈대

```csharp
using System.Net;
using System.Net.Sockets;
using System.Text;

public interface ITransport
{
    bool IsConnected { get; }
    event Action<string>? LineReceived;
    Task SendLineAsync(string line);
}

public sealed class TcpTransport : ITransport
{
    const string Token = "change-me";
    readonly TcpListener _listener = new(IPAddress.Any, 5001);
    readonly SemaphoreSlim _gate = new(1, 1);
    StreamWriter? _writer;
    public bool IsConnected => _writer != null;
    public event Action<string>? LineReceived;   // 백그라운드 스레드에서 호출됨 (UI는 BeginInvoke)

    public async Task RunAsync(CancellationToken ct)
    {
        _listener.Start();
        while (!ct.IsCancellationRequested)
        {
            using var client = await _listener.AcceptTcpClientAsync(ct);
            client.NoDelay = true;
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.ASCII);
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(ct);
            try
            {
                idle.CancelAfter(5000);
                var hello = await reader.ReadLineAsync(idle.Token);
                if (hello is null || !hello.StartsWith("#HELLO,") || !hello.EndsWith("," + Token)) continue;
                _writer = new StreamWriter(stream, Encoding.ASCII) { NewLine = "\n", AutoFlush = true };
                await SendLineAsync("@0,PING");                 // 전체 상태 다시 받기
                string? line;
                while ((line = await reader.ReadLineAsync(idle.Token)) != null)
                {
                    idle.CancelAfter(5000);                     // 5초 무응답이면 끊김 처리
                    LineReceived?.Invoke(line);
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested) { }
            catch (IOException) { }
            finally { _writer = null; }                         // 다시 접속 대기
        }
    }

    public async Task HeartbeatAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        while (await timer.WaitForNextTickAsync(ct))
            if (IsConnected) try { await SendLineAsync("@0,PING"); } catch { }
    }

    public async Task SendLineAsync(string line)
    {
        await _gate.WaitAsync();
        try
        {
            if (_writer is null) throw new InvalidOperationException("ESP32 offline");
            await _writer.WriteLineAsync(line);
        }
        finally { _gate.Release(); }
    }
}
```

앱이 시작될 때 `RunAsync`와 `HeartbeatAsync`를 둘 다 백그라운드로 실행해요.

---

## 참고 자료

- MediaPipe `mp.solutions` 삭제: https://github.com/google-ai-edge/mediapipe/issues/6192
- MediaPipe Python 지원 환경: https://developers.google.com/edge/mediapipe/solutions/setup_python
- MediaPipe 제스처 인식 Python 가이드: https://developers.google.com/edge/mediapipe/solutions/vision/gesture_recognizer/python
- 라즈베리파이 4 MediaPipe 최적화 예시: https://github.com/S-SUJAN-S/rpi-mediapipe-hand-tracking
- .NET 8/9 지원 종료: https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support
- HttpListener 비권장: https://learn.microsoft.com/en-us/dotnet/api/system.net.httplistener?view=net-10.0
- SerialPort.DataReceived 스레드: https://learn.microsoft.com/en-us/dotnet/api/system.io.ports.serialport.datareceived
- ESP32 시리얼 포트 열 때 리셋: https://www.coding-now.com/en/guides/esp32-serial-monitor-reset
- ScottPlot 실시간 데이터: https://scottplot.net/cookbook/5/LiveData/
- 기상청 단기예보 조회서비스: https://www.data.go.kr/data/15084084/openapi.do
- 구글 캘린더 API Python 빠른 시작: https://developers.google.com/workspace/calendar/api/quickstart/python
- 구글 캘린더 iCal 비밀 주소: https://calendarprint.com/en/blog/sync-google-calendar-secret-ical-address
- edge-tts: https://pypi.org/project/edge-tts
- 라즈베리파이 오프라인 TTS (eSpeak NG, Piper): https://sekin.in/easy-offline-text-to-speech-on-raspberry-pi-espeak-ng-and-piper-guide
- ESP32 ADC2와 Wi-Fi: https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-reference/peripherals/gpio.html
- ESP32 Wi-Fi 절전과 지연: https://docs.espressif.com/projects/esp-idf/en/stable/esp32/api-guides/wifi-driver/wifi-performance-and-power-save.html
- setSleep(false) 왕복 시간 사례: https://github.com/me-no-dev/ESPAsyncWebServer/issues/437
- 아이폰 핫스팟 '호환성 최대화': https://support.apple.com/ko-kr/119837
- ESP32 Wi-Fi 연결 문제(WPA2 권장): https://solderhub.com/articles/esp32-wifi-troubleshooting-connection-failed-reconnect
- ESP32 brownout: https://circuitbench.co.uk/esp32-brownout-detector-triggered-fix
- WiFiManager: https://github.com/tzapu/WiFiManager
- WiFiManager ESP32 튜토리얼: https://solderhub.com/articles/esp32-wifimanager-captive-portal-no-hardcoded-wifi
- ArduinoOTA: https://blog.hirnschall.net/esp32-ota-update
