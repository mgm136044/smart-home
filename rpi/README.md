# 라즈베리파이 제스처 앱

USB 카메라로 손동작을 인식해서 PC 허브로 명령을 보내요. PC(Windows)에서도 똑같이 돌아가서,
노트북 웹캠으로 먼저 개발하고 라즈베리파이로 옮기면 돼요.

> MediaPipe 0.10.30부터 예전 `mp.solutions.hands`가 삭제됐어요. 이 코드는 새 Tasks API(`GestureRecognizer`)를 써요.
> 인터넷 예제나 AI가 짜준 코드에 `mp.solutions`가 보이면 그대로 쓰면 안 돼요.

## 제스처 (기본 설정, `gesture/config.toml`에서 바꿀 수 있어요)

| 단계 | 제스처 | 동작 |
|---|---|---|
| 깨우기 | 손바닥 1초 | 5초 동안 명령 대기 (손바닥을 계속 보여주면 연장) |
| 전등 | 엄지 위 / 엄지 아래 | 켜기 / 끄기 |
| 팬 | 브이 | 켜기·끄기 전환 |
| 커튼 | 검지 위 / 주먹 | 열기 / 닫기 |
| 브리핑 | 아이러브유 | 날씨 + 일정 (다음 단계에서 구현) |
| 문 | 손가락 3개 → 엄지 위 / 엄지 아래 | 열기 / 닫기 (한 번 더 확인) |

- 명령 제스처는 0.5초 유지해야 실행돼요. 같은 제스처를 계속 들고 있어도 한 번만 실행돼요.
- 명령 후 1.5초 쉬었다가 다시 명령 대기로 돌아가요. 5초 동안 아무것도 안 하면 대기 종료.

## PC(Windows)에서 실행

```powershell
cd rpi
py -3.14 -m venv .venv
.venv\Scripts\python -m pip install -r gesture\requirements.txt
cd gesture
..\.venv\Scripts\python gesture_app.py              # 웹캠 + 화면
..\.venv\Scripts\python gesture_app.py --dry-run    # 허브 없이 출력만
```

## 라즈베리파이 설치 (Raspberry Pi OS 64비트)

32비트 OS에서는 MediaPipe가 안 돼요. 방열판과 팬을 꼭 달아요 (발열로 속도가 떨어져요).

```bash
sudo apt update && sudo apt install -y python3-venv libgl1
cd ~/smart-home/rpi
python3 -m venv .venv
.venv/bin/pip install -r gesture/requirements.txt
cp gesture/config.toml gesture/config.local.toml
nano gesture/config.local.toml        # [hub] url = "http://PC-IP:5000"
cd gesture && ../.venv/bin/python gesture_app.py
```

모니터 없이 돌릴 때는 `--no-window`를 붙여요. 이 경우 `opencv-python` 대신 `opencv-python-headless`를 깔면 더 가벼워요.

### 부팅할 때 자동 실행

```bash
sudo cp ~/smart-home/rpi/smarthome-gesture.service /etc/systemd/system/
sudo nano /etc/systemd/system/smarthome-gesture.service   # User와 경로를 실제 계정에 맞게
sudo systemctl daemon-reload
sudo systemctl enable --now smarthome-gesture
journalctl -u smarthome-gesture -f     # 로그 보기
```

## 테스트

```bash
cd gesture
python -m unittest discover -s tests                 # 상태머신 단위 테스트 (카메라 필요 없음)
python tests/e2e_check.py                            # 공식 예제 사진 4장 인식 확인
python tests/e2e_check.py --hub http://PC-IP:5000    # 인식 → 상태머신 → 허브 → 장치까지
python gesture_app.py --image my_hand.jpg            # 내 사진 인식 결과
```

## 파일

| 파일 | 내용 |
|---|---|
| `gesture/gesture_app.py` | 카메라 루프, 화면 표시, 실행 옵션 |
| `gesture/recognizer.py` | MediaPipe Tasks 래퍼, 손가락 개수 계산(Three_Fingers) |
| `gesture/state_machine.py` | 깨우기 → 명령 → 쉬기 상태머신 (오작동 방지) |
| `gesture/hub_client.py` | 허브로 명령 전송 (백그라운드 스레드, `X-Source: gesture`) |
| `gesture/config.toml` | 허브 주소, 카메라, 시간, 제스처 매핑 |
| `smarthome-gesture.service` | systemd 자동 실행 |
