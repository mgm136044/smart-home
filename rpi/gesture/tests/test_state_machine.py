"""상태머신 단위 테스트:  python -m unittest discover -s tests  (rpi/gesture 폴더에서)"""

import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))

from state_machine import GestureStateMachine, Mode, parse_target  # noqa: E402

MAPPING = {
    "Thumb_Up": "light:on",
    "Thumb_Down": "light:off",
    "Victory": "fan:toggle",
    "Pointing_Up": "curtain:open",
    "Closed_Fist": "curtain:close",
    "ILoveYou": "briefing",
    "Three_Fingers": "door:confirm",
}
FPS = 15


class Runner:
    """카메라 프레임(15fps)을 흉내 내서 제스처를 일정 시간 동안 넣어요."""

    def __init__(self, sm: GestureStateMachine) -> None:
        self.sm, self.t = sm, 0.0

    def run(self, *seq):
        actions = []
        for label, seconds in seq:
            for _ in range(int(round(seconds * FPS))):
                actions += self.sm.update(label, self.t)
                self.t += 1 / FPS
        return actions


def commands(actions):
    return [(a.device, a.action, a.value) for a in actions if a.kind == "command"]


class StateMachineTest(unittest.TestCase):
    def setUp(self) -> None:
        self.sm = GestureStateMachine(MAPPING)
        self.r = Runner(self.sm)

    def test_wake_then_command(self):
        acts = self.r.run(("Open_Palm", 1.2), ("Thumb_Up", 0.7))
        self.assertEqual(commands(acts), [("light", "on", None)])
        self.assertEqual(self.sm.mode, Mode.COOLDOWN)

    def test_command_without_wake_is_ignored(self):
        self.assertEqual(commands(self.r.run(("Thumb_Up", 3.0))), [])
        self.assertEqual(self.sm.mode, Mode.IDLE)

    def test_short_palm_does_not_wake(self):
        acts = self.r.run(("Open_Palm", 0.6), (None, 0.5), ("Thumb_Up", 1.0))
        self.assertEqual(commands(acts), [])

    def test_holding_same_gesture_fires_once(self):
        acts = self.r.run(("Open_Palm", 1.2), ("Thumb_Up", 5.0))
        self.assertEqual(commands(acts), [("light", "on", None)])

    def test_chain_commands_after_cooldown(self):
        acts = self.r.run(("Open_Palm", 1.2), ("Thumb_Up", 0.7), (None, 1.6), ("Victory", 0.7))
        self.assertEqual(commands(acts), [("light", "on", None), ("fan", "toggle", None)])

    def test_armed_timeout(self):
        acts = self.r.run(("Open_Palm", 1.2), (None, 5.5), ("Thumb_Up", 1.0))
        self.assertEqual(commands(acts), [])
        self.assertEqual(self.sm.mode, Mode.IDLE)

    def test_palm_keeps_listening(self):
        # 손바닥을 계속 보여주는 동안은 대기 시간이 늘어나요
        acts = self.r.run(("Open_Palm", 7.0), ("Pointing_Up", 0.7))
        self.assertEqual(commands(acts), [("curtain", "open", None)])

    def test_door_needs_confirmation(self):
        acts = self.r.run(("Open_Palm", 1.2), ("Three_Fingers", 0.7))
        self.assertEqual(commands(acts), [])
        self.assertEqual(self.sm.mode, Mode.CONFIRM)
        acts = self.r.run(("Thumb_Up", 0.7))
        self.assertEqual(commands(acts), [("door", "open", None)])

    def test_door_close_with_thumb_down(self):
        acts = self.r.run(("Open_Palm", 1.2), ("Three_Fingers", 0.7), ("Thumb_Down", 0.7))
        self.assertEqual(commands(acts), [("door", "close", None)])

    def test_door_confirm_timeout(self):
        acts = self.r.run(("Open_Palm", 1.2), ("Three_Fingers", 0.7), (None, 3.5), ("Thumb_Up", 1.0))
        self.assertEqual(commands(acts), [])
        self.assertEqual(self.sm.mode, Mode.IDLE)

    def test_brief_dropout_is_tolerated(self):
        # 인식이 0.1초 끊겨도 손바닥 유지 시간이 이어져요
        acts = self.r.run(("Open_Palm", 0.6), (None, 0.1), ("Open_Palm", 0.6))
        self.assertEqual(self.sm.mode, Mode.ARMED)
        self.assertTrue(any(a.kind == "mode" for a in acts))

    def test_briefing_action(self):
        acts = self.r.run(("Open_Palm", 1.2), ("ILoveYou", 0.7))
        self.assertEqual([a.kind for a in acts], ["mode", "briefing"])

    def test_set_value_target(self):
        a = parse_target("curtain:set:50", "Victory")
        self.assertEqual((a.device, a.action, a.value), ("curtain", "set", 50))

    def test_bad_mapping_rejected_at_start(self):
        with self.assertRaises(ValueError):
            GestureStateMachine({"Victory": "fan"})


if __name__ == "__main__":
    unittest.main()
