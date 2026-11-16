"""PC 허브로 명령을 보내는 클라이언트.

카메라 루프가 네트워크 때문에 멈추지 않게 별도 스레드에서 보내요.
POST /api/devices/{device}  {"action": "...", "value": ...}  헤더 X-Source: gesture
허브는 ESP32의 ACK를 받으면 응답해요 (docs/protocol.md).
"""

from __future__ import annotations

import queue
import threading
from typing import Callable

import requests

ResultCallback = Callable[[dict], None]


class HubClient:
    def __init__(self, base_url: str, timeout_s: float = 4.0, source: str = "gesture", dry_run: bool = False):
        self.base_url = base_url.rstrip("/")
        self.timeout_s = timeout_s
        self.source = source
        self.dry_run = dry_run
        self._session = requests.Session()
        self._queue: queue.Queue = queue.Queue()
        self._thread = threading.Thread(target=self._worker, name="hub-client", daemon=True)
        self._thread.start()

    def send(self, device: str, action: str, value: int | None = None, on_result: ResultCallback | None = None) -> None:
        self._queue.put((device, action, value, on_result))

    def send_sync(self, device: str, action: str, value: int | None = None) -> dict:
        return self._post(device, action, value)

    def state(self) -> dict | None:
        try:
            return self._session.get(f"{self.base_url}/api/state", timeout=self.timeout_s).json()
        except requests.RequestException:
            return None

    def close(self) -> None:
        self._queue.put(None)
        self._thread.join(timeout=self.timeout_s + 1)

    def _worker(self) -> None:
        while True:
            item = self._queue.get()
            if item is None:
                return
            device, action, value, on_result = item
            result = self._post(device, action, value)
            if on_result:
                on_result(result)

    def _post(self, device: str, action: str, value: int | None) -> dict:
        if self.dry_run:
            return {"ok": True, "device": device.upper(), "state": f"(dry-run) {action}", "ms": 0}
        body: dict = {"action": action}
        if value is not None:
            body["value"] = value
        try:
            r = self._session.post(
                f"{self.base_url}/api/devices/{device}",
                json=body,
                headers={"X-Source": self.source},
                timeout=self.timeout_s,
            )
            return r.json()
        except requests.RequestException as e:
            return {"ok": False, "device": device.upper(), "error": f"허브에 연결할 수 없어요 ({e.__class__.__name__})"}
        except ValueError:
            return {"ok": False, "device": device.upper(), "error": "허브 응답을 읽을 수 없어요"}
