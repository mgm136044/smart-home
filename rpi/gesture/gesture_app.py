"""스마트홈 제스처 앱 (라즈베리파이 / PC 공용)

실행
    python gesture_app.py                      카메라 + 화면 표시 (q 또는 ESC로 종료)
    python gesture_app.py --no-window          화면 없이 (라즈베리파이 서비스용)
    python gesture_app.py --dry-run            허브로 보내지 않고 출력만
    python gesture_app.py --hub http://192.168.0.10:5000
    python gesture_app.py --image a.jpg b.jpg  사진으로 인식 결과만 확인
    python gesture_app.py --video clip.mp4     녹화 영상으로 전체 흐름 시험
    python gesture_app.py --no-window --preview-port 8080 --verbose
                                               라즈베리파이: 브라우저 미리보기 + 인식 로그

사용법 (기본 설정)
    손바닥 1초 → 명령 대기(5초)
      엄지 위 = 전등 켜기 / 엄지 아래 = 전등 끄기 / 브이 = 팬 켜기·끄기
      검지 위 = 커튼 열기 / 주먹 = 커튼 닫기 / 아이러브유 = 브리핑
      손가락 3개 → 엄지 위 = 문 열기 / 엄지 아래 = 문 닫기
    특수: 검지와 중지를 꼬아서 0.8초 → 영역전개 · 무량공처 (깨우지 않아도 됨)
"""

from __future__ import annotations

import argparse
import sys
import time
import tomllib
from pathlib import Path

import cv2

from effects import EffectPlayer
from hub_client import HubClient
from preview import PreviewServer
from recognizer import HAND_CONNECTIONS, GestureReader, HandReading
from state_machine import Action, GestureStateMachine, Mode, Timing

HERE = Path(__file__).resolve().parent
FONT = cv2.FONT_HERSHEY_SIMPLEX

# OpenCV 기본 글꼴은 한글을 못 그려서 화면 표시는 영어로 해요
MODE_TEXT = {
    Mode.IDLE: "IDLE  (open palm 1s to start)",
    Mode.ARMED: "LISTENING",
    Mode.CONFIRM: "DOOR?  thumb up=open / down=close",
    Mode.COOLDOWN: "DONE",
}
MODE_COLOR = {
    Mode.IDLE: (180, 180, 180),
    Mode.ARMED: (0, 200, 255),
    Mode.CONFIRM: (0, 140, 255),
    Mode.COOLDOWN: (90, 220, 90),
}


# ───────────────────────── 설정 ─────────────────────────

def load_config(path: str | None) -> dict:
    candidates = [Path(path)] if path else [HERE / "config.local.toml", HERE / "config.toml"]
    for p in candidates:
        if p.exists():
            with open(p, "rb") as f:
                cfg = tomllib.load(f)
            cfg["_path"] = str(p)
            return cfg
    raise SystemExit("설정 파일(config.toml)을 찾을 수 없어요")


def build_state_machine(cfg: dict) -> GestureStateMachine:
    t = cfg.get("timing", {})
    timing = Timing(**{k: float(v) for k, v in t.items() if k in Timing.__dataclass_fields__})
    door = cfg.get("door", {})
    return GestureStateMachine(
        mapping=dict(cfg.get("gestures", {})),
        wake=cfg.get("wake", {}).get("gesture", "Open_Palm"),
        timing=timing,
        door_open=door.get("open", "Thumb_Up"),
        door_close=door.get("close", "Thumb_Down"),
        instant=dict(cfg.get("instant", {})),
    )


def build_reader(cfg: dict, video: bool) -> GestureReader:
    rec = cfg.get("recognizer", {})
    return GestureReader(
        HERE / rec.get("model", "models/gesture_recognizer.task"),
        video=video,
        min_score=float(rec.get("min_score", 0.6)),
        num_hands=int(rec.get("num_hands", 1)),
    )


def open_camera(cam: dict) -> cv2.VideoCapture:
    index = int(cam.get("index", 0))
    if sys.platform.startswith("linux"):
        cap = cv2.VideoCapture(index, cv2.CAP_V4L2)
        cap.set(cv2.CAP_PROP_FOURCC, cv2.VideoWriter_fourcc(*"MJPG"))  # USB 카메라 FPS 올리기
    elif sys.platform == "win32":
        cap = cv2.VideoCapture(index, cv2.CAP_DSHOW)
    else:
        cap = cv2.VideoCapture(index)
    cap.set(cv2.CAP_PROP_FRAME_WIDTH, int(cam.get("width", 640)))
    cap.set(cv2.CAP_PROP_FRAME_HEIGHT, int(cam.get("height", 480)))
    cap.set(cv2.CAP_PROP_FPS, int(cam.get("fps", 30)))
    cap.set(cv2.CAP_PROP_BUFFERSIZE, 1)  # 가장 최근 프레임만 (지연 줄이기)
    return cap


# ───────────────────────── 동작 처리 ─────────────────────────

class Status:
    """화면에 띄울 마지막 결과"""
    def __init__(self) -> None:
        self.text = ""
        self.bad = False
        self.at = 0.0

    def set(self, text: str, bad: bool = False) -> None:
        self.text, self.bad, self.at = text, bad, time.monotonic()


def handle(action: Action, hub: HubClient, status: Status, fx: EffectPlayer | None = None) -> None:
    stamp = time.strftime("%H:%M:%S")
    if action.kind == "mode":
        print(f"[{stamp}] {action.note}")
        return

    if action.kind == "effect":
        print(f"[{stamp}] 제스처 {action.gesture} → 특수 효과 {action.action}")
        if fx:
            fx.start(action.action or "")
        hub.send_effect(action.action or "")
        return

    if action.kind == "briefing":
        print(f"[{stamp}] 제스처 {action.gesture} → 브리핑 (다음 단계에서 구현: 날씨 + 일정 + 음성)")
        status.set("briefing (coming soon)")
        return

    label = f"{action.device} {action.action}" + (f" {action.value}" if action.value is not None else "")
    print(f"[{stamp}] 제스처 {action.gesture} → {label}")
    status.set(f"{label} ...")

    def done(r: dict) -> None:
        if r.get("ok"):
            print(f"    완료: {r.get('device')} → {r.get('state')} ({r.get('ms')}ms)")
            status.set(f"{label}: OK {r.get('state')} ({r.get('ms')}ms)")
        else:
            print(f"    실패: {r.get('error')}")
            status.set(f"{label}: FAILED", bad=True)

    hub.send(action.device or "", action.action or "", action.value, done)


# ───────────────────────── 화면 ─────────────────────────

def draw(frame, reading: HandReading | None, sm: GestureStateMachine, now: float, status: Status, fps: float,
         fx: EffectPlayer | None = None) -> None:
    h, w = frame.shape[:2]
    color = MODE_COLOR[sm.mode]

    if fx and fx.active:
        center = (int(reading.points[9][0] * w), int(reading.points[9][1] * h)) if reading else None
        fx.render(frame, center)

    if reading:
        pts = [(int(x * w), int(y * h)) for x, y in reading.points]
        for a, b in HAND_CONNECTIONS:
            cv2.line(frame, pts[a], pts[b], (90, 200, 120), 2)
        for p in pts:
            cv2.circle(frame, p, 3, (255, 255, 255), -1)
        label = reading.label or f"({reading.canned})"
        cv2.putText(frame, f"{label}  {reading.score:.2f}  fingers={reading.fingers}", (10, h - 15), FONT, 0.6, (255, 255, 255), 2)

    text = MODE_TEXT[sm.mode]
    if sm.mode in (Mode.ARMED, Mode.CONFIRM):
        text += f"  {sm.remaining(now):.1f}s"
    cv2.rectangle(frame, (0, 0), (w, 36), (0, 0, 0), -1)
    cv2.putText(frame, text, (10, 25), FONT, 0.65, color, 2)
    cv2.putText(frame, f"{fps:4.1f} fps", (w - 100, 25), FONT, 0.55, (200, 200, 200), 1)

    # 지금 제스처를 얼마나 유지했는지 막대로 표시
    if sm.candidate and sm.mode is not Mode.COOLDOWN:
        need = sm.t.wake_hold_s if sm.mode is Mode.IDLE else sm.t.command_hold_s
        progress = min(1.0, sm.held(now) / need)
        cv2.rectangle(frame, (10, 44), (10 + int(200 * progress), 52), color, -1)
        cv2.rectangle(frame, (10, 44), (210, 52), (120, 120, 120), 1)

    if status.text and time.monotonic() - status.at < 4:
        cv2.putText(frame, status.text, (10, 80), FONT, 0.6, (60, 60, 255) if status.bad else (90, 220, 90), 2)


class ReadingLogger:
    """인식 결과가 바뀔 때만 한 줄씩 출력 (--verbose). 인식 점수/손가락 수 조정에 써요."""

    def __init__(self) -> None:
        self._last: tuple | None = None

    def log(self, reading: HandReading | None, sm: GestureStateMachine) -> None:
        key = (reading.label, reading.canned, reading.fingers) if reading else None
        if key == self._last:
            return
        self._last = key
        stamp = time.strftime("%H:%M:%S")
        if reading is None:
            print(f"[{stamp}] 손 없음  ({sm.mode.value})")
        else:
            print(f"[{stamp}] label={reading.label} canned={reading.canned} score={reading.score:.2f} "
                  f"fingers={reading.fingers} hand={reading.handedness}  ({sm.mode.value})")


class FpsMeter:
    def __init__(self) -> None:
        self.value = 0.0
        self._last = time.monotonic()

    def tick(self) -> None:
        now = time.monotonic()
        dt = now - self._last
        self._last = now
        if dt > 0:
            self.value = 1 / dt if self.value == 0 else self.value * 0.9 + (1 / dt) * 0.1


# ───────────────────────── 실행 모드 ─────────────────────────

def run_images(paths: list[str], cfg: dict) -> list[HandReading | None]:
    reader = build_reader(cfg, video=False)
    results = []
    for p in paths:
        img = cv2.imread(p)
        if img is None:
            print(f"{p}: 이미지를 읽을 수 없어요")
            results.append(None)
            continue
        r = reader.read(cv2.cvtColor(img, cv2.COLOR_BGR2RGB))
        results.append(r)
        if r is None:
            print(f"{p}: 손을 찾지 못했어요")
        else:
            print(f"{p}: label={r.label} canned={r.canned} score={r.score:.2f} fingers={r.fingers} hand={r.handedness}")
    reader.close()
    return results


def run_live(args: argparse.Namespace, cfg: dict) -> None:
    hub_cfg = cfg.get("hub", {})
    hub_url = args.hub or hub_cfg.get("url", "http://127.0.0.1:5000")
    hub = HubClient(hub_url, timeout_s=float(hub_cfg.get("timeout_s", 4.0)), dry_run=args.dry_run)
    reader = build_reader(cfg, video=True)
    sm = build_state_machine(cfg)
    cam = cfg.get("camera", {})

    if args.camera is not None:
        cam = {**cam, "index": args.camera}
    if args.video:
        cap = cv2.VideoCapture(args.video)
        video_fps = cap.get(cv2.CAP_PROP_FPS) or 30.0
        mirror = False
    else:
        cap = open_camera(cam)
        video_fps = 0.0
        mirror = bool(cam.get("mirror", True))
    if not cap.isOpened():
        raise SystemExit("카메라(또는 영상)를 열 수 없어요. camera.index를 확인하세요.")

    if not args.dry_run and not args.video:
        state = hub.state()
        print(f"허브 {hub_url}: " + ("연결됨 · " + str(state.get("link")) if state else "응답 없음 (명령은 실패로 표시돼요)"))
    print(f"설정: {cfg['_path']}  ·  손바닥 1초 → 명령 대기  ·  종료: q/ESC 또는 Ctrl+C")

    preview = PreviewServer(args.preview_port) if args.preview_port else None
    if preview:
        print(f"미리보기: http://<이 기기 IP>:{args.preview_port}")
    logger = ReadingLogger() if args.verbose else None
    fx = EffectPlayer()
    status, fps = Status(), FpsMeter()
    start, frame_index, last_ts = time.monotonic(), 0, -1
    last_fps_print = time.monotonic()
    try:
        while True:
            ok, frame = cap.read()
            if not ok:
                if args.video:
                    break
                time.sleep(0.05)
                continue
            if mirror:
                frame = cv2.flip(frame, 1)

            now = frame_index / video_fps if args.video else time.monotonic() - start
            frame_index += 1
            ts = max(int(now * 1000), last_ts + 1)  # VIDEO 모드는 시간이 계속 늘어야 해요
            last_ts = ts

            reading = reader.read(cv2.cvtColor(frame, cv2.COLOR_BGR2RGB), ts)
            if logger:
                logger.log(reading, sm)
            for action in sm.update(reading.label if reading else None, now):
                handle(action, hub, status, fx)
            fps.tick()

            if args.no_window and time.monotonic() - last_fps_print >= 10:
                last_fps_print = time.monotonic()
                print(f"[{time.strftime('%H:%M:%S')}] {fps.value:.1f} fps")

            if not args.no_window or preview:
                draw(frame, reading, sm, now, status, fps.value, fx)
                if preview:
                    preview.update(frame)
                if not args.no_window:
                    cv2.imshow("SmartHome Gesture", frame)
                    if cv2.waitKey(1) & 0xFF in (ord("q"), 27):
                        break
    except KeyboardInterrupt:
        pass
    finally:
        cap.release()
        reader.close()
        hub.close()  # 보내는 중인 명령은 끝까지 보내요
        if preview:
            preview.close()
        if not args.no_window:
            cv2.destroyAllWindows()


def main(argv: list[str] | None = None) -> None:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    p = argparse.ArgumentParser(description="스마트홈 제스처 앱")
    p.add_argument("--config", help="설정 파일 (기본: config.local.toml → config.toml)")
    p.add_argument("--hub", help="허브 주소 (설정보다 우선)")
    p.add_argument("--no-window", action="store_true", help="화면 없이 실행")
    p.add_argument("--dry-run", action="store_true", help="허브로 보내지 않고 출력만")
    p.add_argument("--image", nargs="+", help="사진으로 인식 결과만 확인")
    p.add_argument("--video", help="카메라 대신 영상 파일")
    p.add_argument("--camera", type=int, help="카메라 번호 (설정보다 우선)")
    p.add_argument("--preview-port", type=int, help="브라우저 미리보기 포트 (예: 8080)")
    p.add_argument("--verbose", action="store_true", help="인식 결과가 바뀔 때마다 출력")
    args = p.parse_args(argv)

    cfg = load_config(args.config)
    if args.image:
        run_images(args.image, cfg)
    else:
        run_live(args, cfg)


if __name__ == "__main__":
    main()
