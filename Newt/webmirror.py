import json
import threading
import time
from http import HTTPStatus
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from tempfile import gettempdir

import pygame


VIEWER_HTML = """<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>Newt Live View</title>
  <style>
    :root {
      color-scheme: dark;
      --bg: #000000;
      --panel: #000000;
      --line: #ffffff;
      --text: #ffffff;
      --muted: #ffffff;
      --accent: #ffffff;
    }

    * { box-sizing: border-box; }

    body {
      margin: 0;
      min-height: 100vh;
      font-family: "Segoe UI", Tahoma, sans-serif;
      color: var(--text);
      background: var(--bg);
      display: grid;
      place-items: center;
      padding: 24px;
    }

    .shell {
      width: min(100%, 1500px);
      background: var(--panel);
      border: 1px solid var(--line);
      border-radius: 0;
      overflow: hidden;
    }

    .topbar {
      display: flex;
      justify-content: space-between;
      gap: 16px;
      padding: 14px 18px;
      border-bottom: 1px solid var(--line);
      align-items: center;
    }

    .title {
      font-size: 18px;
      font-weight: 700;
      letter-spacing: 0.04em;
      text-transform: uppercase;
    }

    .status {
      color: var(--muted);
      font-size: 14px;
      white-space: nowrap;
    }

    .stage {
      padding: 18px;
      display: grid;
      place-items: center;
      background: #000000;
    }

    img {
      display: block;
      width: min(100%, var(--frame-width, 100%));
      height: auto;
      border: 1px solid #ffffff;
      image-rendering: pixelated;
      image-rendering: crisp-edges;
      background: #000;
    }

    .dot {
      display: inline-block;
      width: 10px;
      height: 10px;
      border-radius: 50%;
      background: var(--accent);
      margin-right: 8px;
      box-shadow: 0 0 0 1px #ffffff;
    }
  </style>
</head>
<body>
  <main class="shell">
    <header class="topbar">
      <div class="title">Newt Live View</div>
      <div id="status" class="status"><span class="dot"></span>Waiting for first frame...</div>
    </header>
    <section class="stage">
      <img id="frame" alt="Live Newt board" hidden>
    </section>
  </main>

  <script>
    const frame = document.getElementById("frame");
    const status = document.getElementById("status");
    let lastTimestamp = 0;

    function setStatus(text) {
      status.innerHTML = '<span class="dot"></span>' + text;
    }

    async function syncFrame() {
      try {
        const response = await fetch("/meta.json", { cache: "no-store" });
        if (!response.ok) {
          throw new Error("metadata unavailable");
        }

        const meta = await response.json();
        if (!meta.updated_at) {
          setStatus("Waiting for first frame...");
          return;
        }

        document.documentElement.style.setProperty("--frame-width", meta.width + "px");
        const frameTimestamp = Number(meta.updated_at);

        if (frameTimestamp !== lastTimestamp) {
          frame.src = "/frame.png?t=" + frameTimestamp;
          lastTimestamp = frameTimestamp;
        }

        frame.hidden = false;
        setStatus("Live at " + meta.width + " x " + meta.height);
      } catch (error) {
        setStatus("Viewer is trying to reconnect...");
      }
    }

    setInterval(syncFrame, 250);
    syncFrame();
  </script>
</body>
</html>
"""


class FrameMirror:
    def __init__(self, host, port, frame_rate):
        self.host = host
        self.port = port
        self.frame_interval = 1.0 / max(1, frame_rate)

        self._frame_bytes = b""
        self._frame_meta = {
            "width": 0,
            "height": 0,
            "updated_at": 0,
        }
        self._frame_lock = threading.Lock()
        self._last_publish = 0.0
        self._publish_failed = False

        self._server = None
        self._thread = None

        self._scratch_dir = Path(gettempdir()) / "newt-webmirror"
        self._scratch_dir.mkdir(parents=True, exist_ok=True)
        self._scratch_file = self._scratch_dir / "frame.png"

    def start(self):
        handler = self._build_handler()
        self._server = ThreadingHTTPServer((self.host, self.port), handler)
        self._thread = threading.Thread(target=self._server.serve_forever, daemon=True)
        self._thread.start()
        return self

    def stop(self):
        if self._server is None:
            return

        self._server.shutdown()
        self._server.server_close()
        self._server = None
        self._thread = None

    def publish(self, surface):
        now = time.monotonic()
        if now - self._last_publish < self.frame_interval:
            return

        try:
            pygame.image.save(surface, self._scratch_file)
            frame_bytes = self._scratch_file.read_bytes()
        except Exception as exc:
            if not self._publish_failed:
                print(f"Web mirror frame publish failed: {exc}")
                self._publish_failed = True
            return

        self._publish_failed = False
        width, height = surface.get_size()

        with self._frame_lock:
            self._frame_bytes = frame_bytes
            self._frame_meta = {
                "width": width,
                "height": height,
                "updated_at": int(time.time() * 1000),
            }

        self._last_publish = now

    def local_url(self):
        host = "localhost" if self.host == "0.0.0.0" else self.host
        return f"http://{host}:{self.port}"

    def _build_handler(self):
        mirror = self

        class RequestHandler(BaseHTTPRequestHandler):
            def do_GET(self):
                if self.path in ("/", "/index.html"):
                    self._send_html(VIEWER_HTML)
                    return

                if self.path.startswith("/meta.json"):
                    self._send_json(mirror._snapshot_meta())
                    return

                if self.path.startswith("/frame.png"):
                    frame_bytes = mirror._snapshot_frame()
                    if not frame_bytes:
                        self.send_response(HTTPStatus.NO_CONTENT)
                        self.end_headers()
                        return

                    self.send_response(HTTPStatus.OK)
                    self.send_header("Content-Type", "image/png")
                    self.send_header("Content-Length", str(len(frame_bytes)))
                    self.send_header("Cache-Control", "no-store, no-cache, must-revalidate")
                    self.end_headers()
                    self.wfile.write(frame_bytes)
                    return

                self.send_response(HTTPStatus.NOT_FOUND)
                self.end_headers()

            def log_message(self, format, *args):
                return

            def _send_html(self, body):
                payload = body.encode("utf-8")
                self.send_response(HTTPStatus.OK)
                self.send_header("Content-Type", "text/html; charset=utf-8")
                self.send_header("Content-Length", str(len(payload)))
                self.send_header("Cache-Control", "no-store, no-cache, must-revalidate")
                self.end_headers()
                self.wfile.write(payload)

            def _send_json(self, payload):
                body = json.dumps(payload).encode("utf-8")
                self.send_response(HTTPStatus.OK)
                self.send_header("Content-Type", "application/json; charset=utf-8")
                self.send_header("Content-Length", str(len(body)))
                self.send_header("Cache-Control", "no-store, no-cache, must-revalidate")
                self.end_headers()
                self.wfile.write(body)

        return RequestHandler

    def _snapshot_frame(self):
        with self._frame_lock:
            return self._frame_bytes

    def _snapshot_meta(self):
        with self._frame_lock:
            return dict(self._frame_meta)


def start_frame_mirror(host, port, frame_rate):
    mirror = FrameMirror(host, port, frame_rate).start()
    print(f"Web mirror live at {mirror.local_url()}")

    if host == "127.0.0.1":
        print("Set WEB_MIRROR_HOST to '0.0.0.0' in config.py if you want LAN viewers to connect.")

    return mirror
