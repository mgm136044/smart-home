"""인식 결과 → 제스처 이름 판정 테스트 (라즈베리파이 실측 로그에서 가져온 사례 포함)"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

try:
    from recognizer import decide_label
except ImportError as e:  # mediapipe가 없는 환경
    raise unittest.SkipTest(f"recognizer를 불러올 수 없어요: {e}")


class DecideLabelTest(unittest.TestCase):
    def test_low_score_palm_with_four_fingers_is_palm(self):
        # 실측: canned=Open_Palm score=0.57 fingers=4 → 예전엔 None
        self.assertEqual(decide_label("Open_Palm", 0.57, 4, 0.6), "Open_Palm")

    def test_four_fingers_without_model_label_is_palm(self):
        # 실측: canned=None score=0.51 fingers=4
        self.assertEqual(decide_label("None", 0.51, 4, 0.6), "Open_Palm")

    def test_palm_counted_as_three_stays_not_three(self):
        # 실측: canned=Open_Palm score=0.58 fingers=3 → 예전엔 Three_Fingers로 덮어씀
        self.assertNotEqual(decide_label("Open_Palm", 0.58, 3, 0.6), "Three_Fingers")

    def test_real_three_fingers(self):
        # 실측: canned=None score=0.54 fingers=3
        self.assertEqual(decide_label("None", 0.54, 3, 0.6), "Three_Fingers")

    def test_canned_gestures_need_min_score(self):
        self.assertEqual(decide_label("Thumb_Up", 0.61, 0, 0.6), "Thumb_Up")
        self.assertIsNone(decide_label("Thumb_Up", 0.55, 0, 0.6))
        self.assertEqual(decide_label("Victory", 0.77, 2, 0.6), "Victory")
        self.assertEqual(decide_label("Closed_Fist", 0.87, 0, 0.6), "Closed_Fist")


if __name__ == "__main__":
    unittest.main()
