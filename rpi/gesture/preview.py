"""브라우저로 보는 카메라 미리보기 (MJPEG).

라즈베리파이에 모니터가 없어도 손 인식 화면을 노트북 브라우저에서 볼 수 있어요.
    python gesture_app.py --no-window --preview-port 8080
    → http://라즈베리파이-IP:8080  (또는 ssh -L 8080:localhost:8080 후 http://localhost:8080)

/            미리보기 페이지
/stream      MJPEG 스트림
/snapshot.jpg 현재 화면 한 장
"""

from __future__ import annotations

import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer

import cv2

PAGE = """<!doctype html><html lang="ko"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>SmartHome Gesture</title>
<style>html,body{margin:0;background:#111;color:#ddd;font:14px system-ui,sans-serif}
main{max-width:960px;margin:0 auto;padding:12px}img{width:100%;border-radius:8px;display:block}
p{color:#888;margin:8px 2px}</style></head>
<body><main><img src="/stream" alt="camera"><p>손바닥 1초 → 명령 대기 · 화면 아래: 인식 이름, 점수, 손가락 수</p></main></body></html>"""


class PreviewServer:
    def __init__(self, port: int, fps: float = 15.0, quality: int = 70) -> None:
        self._min_interval = 1.0 / fps
        self._quality = quality
        self._cond = threading.Condition()
        self._jpeg: bytes | None = None
        self._seq = 0
        self._last = 0.0
        server = self

        class Handler(BaseHTTPRequestHandler):
            def log_message(self, *_args) -> None:  # 접속 로그는 안 찍어요
                pass

            def do_GET(self) -> None:
                if self.path in ("/", "/index.html"):
                    body = PAGE.encode("utf-8")
                    self.send_response(200)
                    self.send_header("Content-Type", "text/html; charset=utf-8")
                    self.send_header("Content-Length", str(len(body)))
                    self.end_headers()
                    self.wfile.write(body)
                elif self.path.startswith("/snapshot.jpg"):
                    _, jpg = server.wait_frame(-1)
                    if jpg is None:
                        self.send_error(503, "no frame yet")
                        return
                    self.send_response(200)
                    self.send_header("Content-Type", "image/jpeg")
                    self.send_header("Content-Length", str(len(jpg)))
                    self.end_headers()
                    self.wfile.write(jpg)
                elif self.path.startswith("/stream"):
                    self.send_response(200)
                    self.send_header("Content-Type", "multipart/x-mixed-replace; boundary=frame")
                    self.send_header("Cache-Control", "no-cache")
                    self.end_headers()
                    seq = -1
                    try:
                        while True:
                            seq, jpg = server.wait_frame(seq)
                            if jpg is None:
                                continue
                            self.wfile.write(b"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: %d\r\n\r\n" % len(jpg))
                            self.wfile.write(jpg)
                            self.wfile.write(b"\r\n")
                    except (BrokenPipeError, ConnectionResetError, ConnectionAbortedError):
                        pass
                else:
                    self.send_error(404)

        self._httpd = ThreadingHTTPServer(("0.0.0.0", port), Handler)
        self._httpd.daemon_threads = True
        threading.Thread(target=self._httpd.serve_forever, name="preview", daemon=True).start()

    def update(self, frame) -> None:
        """그림이 그려진 BGR 프레임을 넣어요. 초당 fps장까지만 인코딩해요."""
        now = time.monotonic()
        if now - self._last < self._min_interval:
            return
        self._last = now
        ok, buf = cv2.imencode(".jpg", frame, [cv2.IMWRITE_JPEG_QUALITY, self._quality])
        if not ok:
            return
        with self._cond:
            self._jpeg = buf.tobytes()
            self._seq += 1
            self._cond.notify_all()

    def wait_frame(self, last_seq: int, timeout: float = 2.0) -> tuple[int, bytes | None]:
        with self._cond:
            if self._seq == last_seq or self._jpeg is None:
                self._cond.wait(timeout)
            return self._seq, self._jpeg

    def close(self) -> None:
        self._httpd.shutdown()
