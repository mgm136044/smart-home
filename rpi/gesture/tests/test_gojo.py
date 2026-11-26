"""영역전개(무량공처) 손모양 판별 + 특수 손모양 상태머신 테스트"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from state_machine import GestureStateMachine, Mode  # noqa: E402

try:
    from recognizer import decide_label, is_crossed_fingers
except ImportError as e:  # mediapipe가 없는 환경
    is_crossed_fingers = None
    IMPORT_ERROR = e


def hand(index_tip, middle_tip, ring_up=False, pinky_up=False):
    """손바닥이 카메라를 보는 손 모양 21점 (0~1 좌표, y는 아래로 커짐)"""
    p = [(0.0, 0.0)] * 21
    p[0] = (0.50, 0.90)                                   # 손목
    p[1], p[2], p[3], p[4] = (0.40, 0.82), (0.36, 0.74), (0.38, 0.68), (0.41, 0.64)  # 엄지 (접힘)
    p[5], p[6], p[7] = (0.42, 0.60), (0.42, 0.45), (0.42, 0.35)        # 검지 뿌리/마디
    p[9], p[10], p[11] = (0.50, 0.58), (0.50, 0.42), (0.50, 0.32)      # 중지
    p[13], p[14] = (0.57, 0.60), (0.57, 0.50)                          # 약지
    p[17], p[18] = (0.63, 0.63), (0.63, 0.55)                          # 새끼
    p[8], p[12] = index_tip, middle_tip
    p[15], p[16] = ((0.57, 0.40), (0.57, 0.30)) if ring_up else ((0.57, 0.58), (0.57, 0.64))
    p[19], p[20] = ((0.63, 0.46), (0.63, 0.38)) if pinky_up else ((0.63, 0.60), (0.62, 0.66))
    return p


@unittest.skipIf(is_crossed_fingers is None, "mediapipe 없음")
class CrossedFingersTest(unittest.TestCase):
    def test_crossed_index_and_middle(self):
        self.assertTrue(is_crossed_fingers(hand((0.52, 0.25), (0.44, 0.22))))

    def test_victory_is_not_crossed(self):
        self.assertFalse(is_crossed_fingers(hand((0.36, 0.25), (0.56, 0.22))))

    def test_two_fingers_together_is_not_crossed(self):
        self.assertFalse(is_crossed_fingers(hand((0.42, 0.25), (0.50, 0.22))))

    def test_ring_finger_up_is_not_crossed(self):
        self.assertFalse(is_crossed_fingers(hand((0.52, 0.25), (0.44, 0.22), ring_up=True)))

    def test_works_when_hand_is_mirrored(self):
        mirrored = [(1 - x, y) for x, y in hand((0.52, 0.25), (0.44, 0.22))]
        self.assertTrue(is_crossed_fingers(mirrored))

    def test_crossed_wins_over_victory_label(self):
        self.assertEqual(decide_label("Victory", 0.9, 2, 0.6, crossed=True), "Gojo_Cross")


class InstantGestureTest(unittest.TestCase):
    FPS = 15

    def setUp(self):
        self.sm = GestureStateMachine({"Thumb_Up": "light:on", "Three_Fingers": "door:confirm"},
                                      instant={"Gojo_Cross": "effect:infinite_void"})
        self.t = 0.0

    def run_for(self, label, seconds):
        out = []
        for _ in range(int(round(seconds * self.FPS))):
            out += self.sm.update(label, self.t)
            self.t += 1 / self.FPS
        return out

    def effects(self, acts):
        return [a.action for a in acts if a.kind == "effect"]

    def test_fires_from_idle_without_wake(self):
        self.assertEqual(self.effects(self.run_for("Gojo_Cross", 1.0)), ["infinite_void"])
        self.assertEqual(self.sm.mode, Mode.IDLE)

    def test_short_hold_does_not_fire(self):
        self.assertEqual(self.effects(self.run_for("Gojo_Cross", 0.5)), [])

    def test_cooldown_blocks_repeat(self):
        acts = self.run_for("Gojo_Cross", 1.0) + self.run_for(None, 1.0) + self.run_for("Gojo_Cross", 1.0)
        self.assertEqual(self.effects(acts), ["infinite_void"])
        acts = self.run_for(None, 6.5) + self.run_for("Gojo_Cross", 1.0)
        self.assertEqual(self.effects(acts), ["infinite_void"])

    def test_not_during_door_confirm(self):
        acts = self.run_for("Open_Palm", 1.2) + self.run_for("Three_Fingers", 0.7) + self.run_for("Gojo_Cross", 1.0)
        self.assertEqual(self.effects(acts), [])


if __name__ == "__main__":
    unittest.main()
