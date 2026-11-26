"""카메라 화면 특수 효과 (OpenCV로 직접 그려요. 애니메이션 이미지/소리는 쓰지 않아요).

infinite_void: 고죠 사토루 '영역전개 · 무량공처' 느낌
    화면이 깊은 우주처럼 어두워지고, 손 위치에 하얗게 빛나는 고리(사건의 지평선)가 열리면서
    별빛 줄기가 사방으로 쏟아져요. 약 6초.
라즈베리파이 4(초당 약 10장)에서도 돌 수 있게 흐림 처리는 작은 그림에서만 해요.
"""

from __future__ import annotations

import math
import time

import cv2
import numpy as np

FONT = cv2.FONT_HERSHEY_DUPLEX


class EffectPlayer:
    DURATION = 6.0

    def __init__(self, seed: int = 7) -> None:
        self.name: str | None = None
        self._start = 0.0
        rng = np.random.default_rng(seed)
        n = 260
        self._angle = rng.uniform(0, 2 * math.pi, n)
        self._speed = rng.uniform(0.25, 1.0, n)
        self._phase = rng.uniform(0, 1, n)
        self._hue = rng.uniform(0, 1, n)

    @property
    def active(self) -> bool:
        return self.name is not None and time.monotonic() - self._start < self.DURATION

    def start(self, name: str) -> None:
        self.name = name
        self._start = time.monotonic()

    def render(self, frame: np.ndarray, center: tuple[int, int] | None = None) -> None:
        """frame(BGR)에 바로 그려요."""
        if not self.active:
            return
        if self.name == "infinite_void":
            self._infinite_void(frame, time.monotonic() - self._start, center)

    # ───────────────────────── 무량공처 ─────────────────────────

    def _infinite_void(self, frame: np.ndarray, t: float, center: tuple[int, int] | None) -> None:
        h, w = frame.shape[:2]
        cx, cy = center if center else (w // 2, h // 2)
        # 들어올 때 0.6초, 나갈 때 1초 동안 서서히
        a = min(1.0, t / 0.6) * min(1.0, max(0.0, (self.DURATION - t) / 1.0))

        # 1) 배경: 카메라 화면을 깊은 남보라색 우주처럼 어둡게
        void = np.zeros_like(frame)
        void[:] = (28, 6, 10)  # BGR
        cv2.addWeighted(frame, 1.0 - 0.88 * a, void, 0.88 * a, 0, dst=frame)

        # 2) 빛 레이어(작게 그려서 흐리게 한 뒤 키워서 더해요 → 빛 번짐)
        sw, sh = w // 4, h // 4
        glow = np.zeros((sh, sw, 3), np.uint8)
        scx, scy = cx // 4, cy // 4
        ring_r = int((0.10 + 0.05 * min(1.0, t / 1.2)) * sw + 2 * math.sin(t * 6))
        cv2.circle(glow, (scx, scy), ring_r, (255, 230, 200), max(2, sw // 60))
        cv2.circle(glow, (scx, scy), int(ring_r * 1.6), (255, 120, 60), 1)
        glow = cv2.GaussianBlur(glow, (0, 0), sigmaX=4)
        glow = cv2.resize(glow, (w, h), interpolation=cv2.INTER_LINEAR)
        cv2.add(frame, (glow * a).astype(np.uint8), dst=frame)

        # 3) 별빛 줄기: 고리 중심에서 바깥으로 쏟아져요
        max_r = math.hypot(w, h)
        prog = (self._phase + t * self._speed * 0.35) % 1.0
        r1 = (prog ** 2) * max_r
        r0 = np.maximum(r1 - (8 + 60 * prog * self._speed), 0)
        cos, sin = np.cos(self._angle + t * 0.15), np.sin(self._angle + t * 0.15)
        for i in range(len(self._angle)):
            if r1[i] < ring_r * 4:
                continue
            p0 = (int(cx + cos[i] * r0[i]), int(cy + sin[i] * r0[i]))
            p1 = (int(cx + cos[i] * r1[i]), int(cy + sin[i] * r1[i]))
            b = int(255 * a * min(1.0, prog[i] * 2))
            color = (b, int(b * (0.75 + 0.25 * self._hue[i])), int(b * 0.85))
            cv2.line(frame, p0, p1, color, 1, cv2.LINE_AA)

        # 4) 사건의 지평선: 고리 안은 완전히 검게, 테두리는 하얗게
        big_r = ring_r * 4
        cv2.circle(frame, (cx, cy), max(1, big_r - 3), (0, 0, 0), -1, cv2.LINE_AA)
        cv2.circle(frame, (cx, cy), big_r, (255, 255, 255), 2, cv2.LINE_AA)
        for k in range(3):  # 고리를 따라 도는 빛 조각
            start = math.degrees(t * (2.2 + k * 0.7)) + k * 120
            cv2.ellipse(frame, (cx, cy), (big_r + 6 + k * 4, big_r + 6 + k * 4), 0, start, start + 70,
                        (255, 200, 140), 2, cv2.LINE_AA)

        # 5) 글자 (OpenCV 글꼴은 한글이 안 돼서 영어로)
        if t > 0.5:
            ta = a * min(1.0, (t - 0.5) / 0.5)
            self._center_text(frame, "DOMAIN EXPANSION", int(h * 0.80), 0.7, ta, (230, 200, 180))
        if t > 1.3:
            ta = a * min(1.0, (t - 1.3) / 0.6)
            self._center_text(frame, "INFINITE VOID", int(h * 0.90), 1.1, ta, (255, 255, 255))

    @staticmethod
    def _center_text(frame, text: str, y: int, scale: float, alpha: float, color) -> None:
        (tw, _), _ = cv2.getTextSize(text, FONT, scale, 2)
        x = (frame.shape[1] - tw) // 2
        c = tuple(int(v * alpha) for v in color)
        cv2.putText(frame, text, (x, y), FONT, scale, c, 2, cv2.LINE_AA)
