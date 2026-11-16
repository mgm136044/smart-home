"""카메라 없이 하는 전체 흐름 점검.

1) MediaPipe 공식 예제 사진 4장(엄지 위/아래, 브이, 검지 위)을 내려받아 인식 결과 확인
2) --hub를 주면: 손바닥(깨우기) + 사진에서 인식한 제스처를 상태머신에 넣고, 나온 명령을 실제 허브로 보내요

    python tests/e2e_check.py
    python tests/e2e_check.py --hub http://127.0.0.1:5000
"""

import argparse
import sys
import urllib.request
from pathlib import Path

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE.parent))

import gesture_app  # noqa: E402
from hub_client import HubClient  # noqa: E402

SAMPLE_BASE = "https://storage.googleapis.com/mediapipe-tasks/gesture_recognizer/"
SAMPLES = {
    "thumbs_up.jpg": "Thumb_Up",
    "thumbs_down.jpg": "Thumb_Down",
    "victory.jpg": "Victory",
    "pointing_up.jpg": "Pointing_Up",
}


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    p = argparse.ArgumentParser()
    p.add_argument("--hub", help="허브 주소. 주면 인식 결과로 실제 명령까지 보내요")
    args = p.parse_args()

    data = HERE / "data"
    data.mkdir(exist_ok=True)
    for name in SAMPLES:
        if not (data / name).exists():
            urllib.request.urlretrieve(SAMPLE_BASE + name, data / name)

    cfg = gesture_app.load_config(None)
    print("── 1) 사진 인식 ──")
    readings = gesture_app.run_images([str(data / n) for n in SAMPLES], cfg)
    failed = [n for (n, want), r in zip(SAMPLES.items(), readings) if not r or r.label != want]
    print("인식 결과: " + ("전부 맞음" if not failed else f"틀림 {failed}"))

    if not args.hub:
        return 1 if failed else 0

    print("── 2) 상태머신 → 허브 ──")
    sm = gesture_app.build_state_machine(cfg)
    hub = HubClient(args.hub)
    t, fps = 0.0, 15

    def feed(label, seconds):
        nonlocal t
        out = []
        for _ in range(int(seconds * fps)):
            out += sm.update(label, t)
            t += 1 / fps
        return out

    ok = True
    for (name, _), r in zip(SAMPLES.items(), readings):
        label = r.label if r else None
        actions = feed("Open_Palm", 1.2) + feed(label, 0.7) + feed(None, 1.6)  # 깨우기 → 사진 제스처 → 쉬기
        for a in actions:
            if a.kind != "command":
                continue
            res = hub.send_sync(a.device, a.action, a.value)
            mark = "OK " if res.get("ok") else "실패"
            print(f"{name}: {a.gesture} → {a.device} {a.action}  [{mark}] {res.get('state') or res.get('error')} ({res.get('ms')}ms)")
            ok &= bool(res.get("ok"))
        feed(None, 6)  # 다음 사진 전에 대기 상태 끝내기
    hub.close()
    return 0 if ok and not failed else 1


if __name__ == "__main__":
    raise SystemExit(main())
