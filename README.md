# 스마트홈 텀프로젝트 (SW)

손동작(MediaPipe)과 휴대폰으로 팬, 전등, 커튼, 문을 제어하는 스마트홈의 소프트웨어예요.

- 구현 계획: `docs/plan.md`
- 구조도: `docs/architecture.html` (브라우저로 열기)
- 통신 규격: `docs/protocol.md`
- 배선(참고안): `docs/wiring.md`
- 제스처 앱: `rpi/README.md`

## 폴더 구조

```text
smart-home/
├─ SmartHome.slnx              Visual Studio 솔루션
├─ docs/                       계획, 구조도, 통신 규격
├─ pc-hub/SmartHome.Hub/       PC 허브 (C# .NET 10 WinForms + 내장 웹 서버)
│  ├─ Core/        Protocol(줄 프로토콜), HomeState(상태 저장소), CommandService(명령/ACK), HubLog, DeviceSimulator
│  ├─ Transport/   ITransport, TcpTransport(Wi-Fi), SerialTransport(USB), FakeTransport(가상 장치), TransportRouter
│  ├─ Web/         WebServer (REST + WebSocket + 정적 파일)
│  ├─ UI/          MainForm (장치 제어, 센서, 그래프, 로그)
│  ├─ wwwroot/     휴대폰/아이패드 대시보드 (HTML/CSS/JS, 라이브러리 없음)
│  └─ hubsettings.json
├─ tools/Esp32Sim/             ESP32 시뮬레이터 (실제 ESP32처럼 Wi-Fi TCP로 접속)
├─ firmware/
│  ├─ esp32/SmartHomeEsp32/    ESP32: WiFiManager, OTA, TCP/USB, Arduino 중계 (단독 테스트 모드 포함)
│  └─ arduino/SmartHomeUno/    Arduino Uno 참고 구현: 팬/릴레이/서보/스텝, 온습도/조도/적외선
└─ rpi/                        제스처 앱 (Python, MediaPipe Tasks) + systemd 서비스
```

## 지금 되는 것 (2026-11-16)

- 휴대폰/아이패드 대시보드 → PC 허브 → ESP32(또는 가상 장치) → ACK → 결과 표시 (시나리오 1~7)
- Wi-Fi TCP 접속, 토큰 확인, 5초 하트비트, 6초 무응답 감지, 자동 재접속 대기
- USB 시리얼 백업 (설정에 COM 포트를 넣으면 동작, 3초마다 재연결)
- 통로 자동 전환: Wi-Fi TCP → USB → 가상 장치
- 명령 번호 + ACK 매칭, 2초 타임아웃 + 1회 재시도, 토글을 절대값으로 변환
- 모든 화면 실시간 동기화 (WebSocket), WinForms 온습도 그래프(ScottPlot), 통신 로그(파일 포함)
- ESP32 상태 표시: Wi-Fi 신호(dBm), Arduino 응답 여부, 단독 모드. Arduino가 없으면 명령이 바로 실패로 돌아와요
- ESP32/Arduino 펌웨어 (컴파일 확인, 보드 업로드는 아직)
- 제스처 앱: 상태머신 테스트 14개, 예제 사진 인식 → 허브 → 장치까지 확인. 노트북 웹캠 실행과 손 인식도 확인 (실시간 제스처 명령은 아직)

## 실행 방법

### Visual Studio 2026
1. `SmartHome.slnx` 열기
2. 시작 프로젝트를 `SmartHome.Hub`로 지정하고 F5
3. 처음 실행하면 Windows 방화벽 창이 떠요 → **개인 네트워크** 허용 (휴대폰 접속용)

### 명령줄
```powershell
dotnet run --project pc-hub/SmartHome.Hub                 # 기본 (가상 장치 켜짐)
dotnet run --project pc-hub/SmartHome.Hub -- --local      # 이 PC에서만 접속 (방화벽 창 안 뜸)
dotnet run --project pc-hub/SmartHome.Hub -- --no-fake    # 가상 장치 끄기 (실제 ESP32만)
dotnet run --project pc-hub/SmartHome.Hub -- --headless   # 창 없이 서버만
```

### 하드웨어 없이 테스트
- **가상 장치**: 기본으로 켜져 있어요. 허브만 실행하면 바로 버튼이 동작해요. 팬을 켜면 가상 온습도가 내려가요.
- **ESP32 시뮬레이터**: 실제 ESP32처럼 Wi-Fi TCP로 접속해요. 접속하면 허브가 자동으로 가상 장치 대신 이쪽을 써요.
  ```powershell
  dotnet run --project tools/Esp32Sim                         # 127.0.0.1:5001
  dotnet run --project tools/Esp32Sim -- --host 192.168.0.10  # 다른 PC의 허브
  dotnet run --project tools/Esp32Sim -- --mute               # ACK 안 보냄 (타임아웃 테스트)
  dotnet run --project tools/Esp32Sim -- --token wrong        # 인증 실패 테스트
  ```

### 휴대폰/아이패드에서 접속
1. PC와 휴대폰을 같은 공유기(Wi-Fi)에 연결
2. 허브 창 위쪽에 나오는 주소(예: `http://192.168.0.10:5000`)를 휴대폰 브라우저로 열기
3. 공유 → 홈 화면에 추가 (앱처럼 전체 화면으로 열려요)

안 열리면: 방화벽에서 5000, 5001 포트 허용 (관리자 PowerShell)
```powershell
Set-NetConnectionProfile -InterfaceAlias "Wi-Fi" -NetworkCategory Private
New-NetFirewallRule -DisplayName "SmartHome Hub" -Direction Inbound -Protocol TCP -LocalPort 5000,5001 -Action Allow -Profile Private
```

## 설정 (`hubsettings.json`)

| 키 | 기본값 | 설명 |
|---|---|---|
| WebPort | 5000 | 대시보드/REST 포트 |
| TcpPort | 5001 | ESP32 접속 포트 |
| ListenAddress | 0.0.0.0 | 127.0.0.1이면 이 PC만 |
| Token | change-me | ESP32 펌웨어 TOKEN과 같아야 함 |
| SerialPort | (빈칸) | USB 백업 포트, 예: COM4 |
| SerialBaud | 115200 | |
| UseFakeDevice | true | 가상 장치 사용 |
| CommandTimeoutMs | 2000 | ACK 대기 시간 |
| CommandRetries | 1 | 재시도 횟수 |

개인 설정은 `hubsettings.local.json`으로 복사해서 쓰면 깃에 안 올라가요(.gitignore).

## WinForms 화면 꾸미기 (팀원용)

1. Visual Studio에서 `SmartHome.slnx` 열기 → 솔루션 탐색기에서 `pc-hub/SmartHome.Hub/UI/MainForm.cs` 더블클릭하면 디자이너가 열려요.
2. 배치, 글꼴, 색, 크기, 배경은 디자이너에서 마음대로 바꿔도 돼요 (`MainForm.Designer.cs`가 자동으로 바뀌어요).
3. **컨트롤 이름(Name)만은 바꾸지 마세요.** 코드가 이름으로 써요:
   `lblLink`, `chkFake`, `lblStatus`, `lblUrls`, `lblFanState`, `lblLightState`, `lblCurtainState`, `lblDoorState`,
   `trkCurtain`, `lblTemp`, `lblHum`, `lblLux`, `lblIr`, `formsPlot`, `lstLog`, `btn`으로 시작하는 버튼들
4. 켜짐/꺼짐, 연결/오프라인처럼 **상태에 따라 바뀌는 색**은 `UI/Theme.cs`에서 바꿔요 (디자이너에서 정한 글자색을 코드가 덮어써요).
5. 버튼을 새로 넣거나 동작을 바꾸려면 `MainForm.cs`의 `btn..._Click` 함수를 참고하세요.
6. 바꾼 뒤 F5로 실행해서 버튼이 동작하는지 확인하고 커밋해 주세요. 하드웨어 없이도 가상 장치로 동작해요.

## 펌웨어 업로드

Arduino IDE 또는 arduino-cli. 라이브러리: WiFiManager(tzapu 2.x), Stepper, DHT sensor library, Adafruit Unified Sensor, Servo

```powershell
$cli = "C:\Program Files\Arduino IDE\resources\app\lib\backend\resources\arduino-cli.exe"
& $cli compile --fqbn esp32:esp32:esp32 firmware/esp32/SmartHomeEsp32 -u -p COM5     # ESP32
& $cli compile --fqbn arduino:avr:uno firmware/arduino/SmartHomeUno -u -p COM4        # Uno
```

ESP32 처음 설정: 폰으로 `SmartHome-Setup`(비번 smarthome) Wi-Fi에 접속 → 공유기 SSID/비번 + PC 허브 IP 입력.
부팅 직후 2초 안에 BOOT 버튼을 누르면 Wi-Fi 설정을 지우고 설정 화면을 다시 열어요.
이후 업로드는 Arduino IDE 포트 목록의 `smarthome-esp32` 네트워크 포트(OTA, 비번 smarthome-ota)로 해요.

## 다음 할 일

1. 실제 보드 업로드와 배선 확인 (`docs/wiring.md`의 시험 순서)
2. 제스처 앱 실시간 확인: 손바닥(Open_Palm)이 손가락 3개(Three_Fingers)로 잘못 세어지는지 화면 아래 표시로 확인 → 인식 점수/시간 조정
3. 자동화 규칙 (온습도 → 팬, 조도 → 커튼/전등, 수동 우선 10분)
4. 센서 기록 SQLite 저장, 브리핑(기상청 + 캘린더 + TTS)
