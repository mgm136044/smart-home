"""제스처 상태머신.

카메라 프레임마다 인식된 제스처 이름을 넣으면, 실행할 동작(Action)을 돌려줘요.
카메라/MediaPipe와 분리돼 있어서 단위 테스트로 검증할 수 있어요 (tests/test_state_machine.py).

흐름
    IDLE ──(깨우기 제스처 1초 유지)──▶ ARMED ──(명령 제스처 0.5초 유지)──▶ 명령 실행 ─▶ COOLDOWN(1.5초) ─▶ ARMED
      ▲                                  │                                                         │
      └──────────(5초 동안 아무 명령 없음)──┴──(손가락 3개)──▶ CONFIRM ──(엄지 위/아래)──▶ 문 열기/닫기 ─┘

오작동 방지
    - 같은 제스처가 정해진 시간 이상 이어져야 실행
    - 한 번 실행한 제스처는 손 모양을 바꿔야 다시 실행 (계속 들고 있어도 한 번만)
    - 인식이 아주 잠깐(dropout_s 이하) 끊기거나 다른 이름이 끼어드는 건 같은 제스처로 봐요
      (라즈베리파이 실측: 손바닥을 들고 있어도 손가락 수가 4↔3으로 흔들려 이름이 잠깐씩 바뀜)
"""

from __future__ import annotations

from collections import Counter, deque
from dataclasses import dataclass
from enum import Enum


class Mode(Enum):
    IDLE = "IDLE"            # 깨우기 제스처를 기다리는 중
    ARMED = "ARMED"          # 명령 제스처를 기다리는 중
    CONFIRM = "CONFIRM"      # 문: 확인 제스처를 기다리는 중
    COOLDOWN = "COOLDOWN"    # 명령 직후 잠깐 쉬는 중


@dataclass
class Timing:
    wake_hold_s: float = 1.0
    command_hold_s: float = 0.5
    armed_timeout_s: float = 5.0
    cooldown_s: float = 1.5
    confirm_timeout_s: float = 3.0
    dropout_s: float = 0.2
    instant_hold_s: float = 0.8       # 특수 손모양(예: 무량공처) 유지 시간
    instant_cooldown_s: float = 8.0   # 특수 효과는 8초에 한 번만


@dataclass
class Action:
    """kind: command(장치 명령) | briefing(브리핑) | effect(특수 효과, action=효과 이름) | mode(상태 변화 알림)"""
    kind: str
    gesture: str = ""
    device: str | None = None
    action: str | None = None
    value: int | None = None
    note: str = ""

    def __str__(self) -> str:
        if self.kind == "command":
            v = f" {self.value}" if self.value is not None else ""
            return f"{self.device} {self.action}{v} ({self.gesture})"
        if self.kind == "effect":
            return f"effect {self.action} ({self.gesture})"
        return f"{self.kind}: {self.note or self.gesture}"


def parse_target(target: str, gesture: str) -> Action:
    """설정의 "light:on", "curtain:set:50", "briefing" 같은 문자열을 Action으로 바꿔요."""
    if target == "briefing":
        return Action("briefing", gesture=gesture)
    if target.startswith("effect:"):
        return Action("effect", gesture=gesture, action=target.split(":", 1)[1])
    parts = target.split(":")
    if len(parts) < 2:
        raise ValueError(f"제스처 설정 형식이 잘못됐어요: {target!r} (예: light:on)")
    value = int(parts[2]) if len(parts) >= 3 else None
    return Action("command", gesture=gesture, device=parts[0], action=parts[1], value=value)


class GestureStateMachine:
    CONFIRM_TARGET = "door:confirm"

    def __init__(
        self,
        mapping: dict[str, str],
        wake: str = "Open_Palm",
        timing: Timing | None = None,
        door_open: str = "Thumb_Up",
        door_close: str = "Thumb_Down",
        instant: dict[str, str] | None = None,
    ) -> None:
        self.mapping = mapping
        self.instant = instant or {}  # 깨우지 않아도 바로 되는 특수 손모양
        for target in self.instant.values():
            parse_target(target, "")
        self._last_instant = -1e9
        self.wake = wake
        self.t = timing or Timing()
        self.door_open = door_open
        self.door_close = door_close
        for target in mapping.values():
            if target != self.CONFIRM_TARGET:
                parse_target(target, "")  # 설정 오류를 시작할 때 미리 잡아요

        self.mode = Mode.IDLE
        self._mode_since = 0.0
        self._cand: str | None = None
        self._cand_start = 0.0
        self._cand_last = 0.0
        self._fired = False
        self._history: deque[tuple[float, str | None]] = deque()  # 다수결용 최근 인식 이름
        self._stable: str | None = None
        self._stable_since = 0.0

    # ───────── 외부에서 보는 값 ─────────

    @property
    def candidate(self) -> str | None:
        return self._cand

    def held(self, now: float) -> float:
        return now - self._cand_start if self._cand else 0.0

    def remaining(self, now: float) -> float:
        """ARMED/CONFIRM/COOLDOWN이 끝날 때까지 남은 시간"""
        limit = {
            Mode.ARMED: self.t.armed_timeout_s,
            Mode.CONFIRM: self.t.confirm_timeout_s,
            Mode.COOLDOWN: self.t.cooldown_s,
        }.get(self.mode, 0.0)
        return max(0.0, limit - (now - self._mode_since))

    # ───────── 프레임마다 호출 ─────────

    def update(self, label: str | None, now: float) -> list[Action]:
        label = self._smooth(label if label and label != "None" else None, now)
        held = self._track(label, now, self._stable_since)

        # 특수 손모양: 어느 상태에서든(문 확인 중 제외) 유지만 하면 바로 발동, 상태는 안 바꿔요
        if (label in self.instant and not self._fired and self.mode is not Mode.CONFIRM
                and held >= self.t.instant_hold_s and now - self._last_instant >= self.t.instant_cooldown_s):
            self._fired = True
            self._last_instant = now
            return [parse_target(self.instant[label], label)]

        if self.mode is Mode.COOLDOWN:
            if now - self._mode_since >= self.t.cooldown_s:
                self._enter(Mode.ARMED, now)
            return []

        if self.mode is Mode.IDLE:
            if label == self.wake and held >= self.t.wake_hold_s and not self._fired:
                self._fired = True
                self._enter(Mode.ARMED, now)
                return [Action("mode", gesture=label, note="명령 대기")]
            return []

        # ARMED / CONFIRM
        timeout = self.t.armed_timeout_s if self.mode is Mode.ARMED else self.t.confirm_timeout_s
        if label == self.wake and self.mode is Mode.ARMED:
            self._mode_since = now  # 손바닥을 보여주는 동안은 대기 시간을 늘려요
        if now - self._mode_since >= timeout:
            self._enter(Mode.IDLE, now)
            return [Action("mode", note="시간 초과, 대기 종료")]
        if label is None or self._fired or held < self.t.command_hold_s:
            return []

        if self.mode is Mode.ARMED:
            target = self.mapping.get(label)
            if target is None:
                return []
            self._fired = True
            if target == self.CONFIRM_TARGET:
                self._enter(Mode.CONFIRM, now)
                return [Action("mode", gesture=label, note="문: 엄지 위 = 열기, 엄지 아래 = 닫기")]
            self._enter(Mode.COOLDOWN, now)
            return [parse_target(target, label)]

        # CONFIRM
        if label == self.door_open:
            action = "open"
        elif label == self.door_close:
            action = "close"
        else:
            return []
        self._fired = True
        self._enter(Mode.COOLDOWN, now)
        return [Action("command", gesture=label, device="door", action=action)]

    # ───────── 내부 ─────────

    def _enter(self, mode: Mode, now: float) -> None:
        self.mode = mode
        self._mode_since = now

    def _smooth(self, label: str | None, now: float) -> str | None:
        """최근 dropout_s 동안 나온 이름 중 과반인 것을 지금 제스처로 봐요 (동점이면 이전 값 유지).
        한두 프레임씩 끼어드는 다른 이름이나 인식 끊김을 걸러줘요."""
        self._history.append((now, label))
        while self._history and now - self._history[0][0] > self.t.dropout_s:
            self._history.popleft()
        top, count = Counter(l for _, l in self._history).most_common(1)[0]
        if count * 2 > len(self._history) and top != self._stable:
            self._stable = top
            # 다수결은 몇 프레임 늦게 알아차리니까, 유지 시간은 창 안에서 처음 보인 때부터 세요
            self._stable_since = next(t for t, l in self._history if l == top)
        return self._stable

    def _track(self, label: str | None, now: float, since: float) -> float:
        """지금 제스처를 몇 초째 유지하고 있는지"""
        if label is None:
            if self._cand is not None and now - self._cand_last > self.t.dropout_s:
                self._cand = None
                self._fired = False
            return 0.0
        if label != self._cand:
            self._cand = label
            self._cand_start = min(since, now)
            self._fired = False
        self._cand_last = now
        return now - self._cand_start
