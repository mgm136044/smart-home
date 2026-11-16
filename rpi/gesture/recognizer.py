"""MediaPipe Tasks 제스처 인식 래퍼.

주의: mediapipe 0.10.30부터 예전 `mp.solutions.hands` API가 삭제됐어요.
인터넷 예제나 AI가 짜준 코드가 mp.solutions를 쓰면 동작하지 않아요. 여기서는 Tasks API만 써요.

기본 제스처 7가지: Closed_Fist, Open_Palm, Pointing_Up, Thumb_Down, Thumb_Up, Victory, ILoveYou
추가 제스처 1가지: Three_Fingers (엄지를 뺀 손가락 3개, 손 랜드마크로 직접 계산)
"""

from __future__ import annotations

import math
import urllib.request
from dataclasses import dataclass, field
from pathlib import Path

import mediapipe as mp
from mediapipe.tasks import python as mp_python
from mediapipe.tasks.python import vision

MODEL_URL = (
    "https://storage.googleapis.com/mediapipe-models/gesture_recognizer/"
    "gesture_recognizer/float16/latest/gesture_recognizer.task"
)
CANNED = {"Closed_Fist", "Open_Palm", "Pointing_Up", "Thumb_Down", "Thumb_Up", "Victory", "ILoveYou"}

# 손 랜드마크 연결선 (화면 그리기용)
HAND_CONNECTIONS = [
    (0, 1), (1, 2), (2, 3), (3, 4),
    (0, 5), (5, 6), (6, 7), (7, 8),
    (5, 9), (9, 10), (10, 11), (11, 12),
    (9, 13), (13, 14), (14, 15), (15, 16),
    (13, 17), (17, 18), (18, 19), (19, 20),
    (0, 17),
]


@dataclass
class HandReading:
    label: str | None          # 상태머신에 넣을 최종 제스처 이름 (없으면 None)
    canned: str                # MediaPipe 기본 분류 결과
    score: float
    fingers: int               # 펴진 손가락 수 (엄지 제외)
    handedness: str
    points: list[tuple[float, float]] = field(default_factory=list)  # 0~1 정규화 좌표


def ensure_model(path: str | Path) -> Path:
    """모델 파일이 없으면 내려받아요 (약 8MB)."""
    path = Path(path)
    if not path.exists():
        path.parent.mkdir(parents=True, exist_ok=True)
        print(f"모델 내려받는 중: {MODEL_URL}")
        urllib.request.urlretrieve(MODEL_URL, path)
    return path


def count_fingers(points: list[tuple[float, float]], aspect: float = 1.0) -> int:
    """엄지를 뺀 펴진 손가락 수. 손목에서 끝마디가 가운데 마디보다 충분히 멀면 펴진 것으로 봐요.
    손이 기울어져도 동작하게 '거리'로 판단해요. aspect = 화면 가로/세로 비율."""
    def dist(a: int, b: int) -> float:
        (ax, ay), (bx, by) = points[a], points[b]
        return math.hypot((ax - bx) * aspect, ay - by)

    tips, pips = (8, 12, 16, 20), (6, 10, 14, 18)
    return sum(1 for t, p in zip(tips, pips) if dist(t, 0) > dist(p, 0) * 1.15)


class GestureReader:
    def __init__(self, model_path: str | Path, video: bool = True, min_score: float = 0.6, num_hands: int = 1):
        self.min_score = min_score
        self.video = video
        options = vision.GestureRecognizerOptions(
            base_options=mp_python.BaseOptions(model_asset_path=str(ensure_model(model_path))),
            running_mode=vision.RunningMode.VIDEO if video else vision.RunningMode.IMAGE,
            num_hands=num_hands,
            min_hand_detection_confidence=0.5,
            min_hand_presence_confidence=0.5,
            min_tracking_confidence=0.5,
        )
        self._recognizer = vision.GestureRecognizer.create_from_options(options)

    def read(self, rgb, timestamp_ms: int | None = None) -> HandReading | None:
        """rgb: (H, W, 3) uint8 RGB 배열. 손이 없으면 None."""
        image = mp.Image(image_format=mp.ImageFormat.SRGB, data=rgb)
        if self.video:
            result = self._recognizer.recognize_for_video(image, int(timestamp_ms or 0))
        else:
            result = self._recognizer.recognize(image)
        if not result.hand_landmarks:
            return None

        points = [(lm.x, lm.y) for lm in result.hand_landmarks[0]]
        top = result.gestures[0][0] if result.gestures and result.gestures[0] else None
        canned = top.category_name if top else "None"
        score = float(top.score) if top else 0.0
        hand = result.handedness[0][0].category_name if result.handedness and result.handedness[0] else ""
        h, w = rgb.shape[:2]
        fingers = count_fingers(points, aspect=w / h if h else 1.0)

        if fingers == 3:
            label = "Three_Fingers"  # 기본 분류에는 없는 모양이라 직접 판단해요
        elif canned in CANNED and score >= self.min_score:
            label = canned
        else:
            label = None
        return HandReading(label, canned, score, fingers, hand, points)

    def close(self) -> None:
        self._recognizer.close()
