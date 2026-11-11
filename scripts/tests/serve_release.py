#!/usr/bin/env python3
"""Small deterministic HTTP server for installer integration tests."""
import argparse
import http.server
import pathlib
import time
import urllib.parse


class Handler(http.server.BaseHTTPRequestHandler):
    def log_message(self, _format, *_args):
        return

    def _record(self):
        if self.server.requests_file:
            with open(self.server.requests_file, "a", encoding="utf-8") as stream:
                stream.write(self.path + "\n")

    def do_HEAD(self):
        self._record()
        if self.path.rstrip("/") == "/releases/latest":
            self.send_response(302)
            self.send_header("Location", "/releases/v1.2.3")
            self.end_headers()
            return
        self.send_response(200)
        self.end_headers()

    def do_GET(self):
        self._record()
        path = urllib.parse.urlparse(self.path).path
        if path.rstrip("/") == "/releases/latest":
            self.send_response(302)
            self.send_header("Location", "/releases/v1.2.3")
            self.end_headers()
            return
        if path.rstrip("/") == "/releases/v1.2.3":
            body = b"v1.2.3"
        else:
            relative = path.removeprefix("/releases/").lstrip("/")
            target = (self.server.root / relative).resolve()
            if self.server.root not in target.parents and target != self.server.root:
                self.send_error(404)
                return
            if not target.is_file():
                self.send_error(404)
                return
            body = target.read_bytes()
            if self.server.fault == "badsums" and target.name == "SHA256SUMS":
                body = b"0000000000000000000000000000000000000000000000000000000000000000  invalid\n"
        self.send_response(200)
        self.send_header("Content-Length", str(len(body)))
        self.send_header("Content-Type", "application/octet-stream")
        self.end_headers()
        if self.server.fault == "truncate" and "drawplate-" in path:
            self.wfile.write(body[: max(1, len(body) // 2)])
            self.wfile.flush()
            self.close_connection = True
            return
        if self.server.fault == "slow":
            for offset in range(0, len(body), 1024):
                self.wfile.write(body[offset : offset + 1024])
                self.wfile.flush()
                time.sleep(0.1)
        else:
            self.wfile.write(body)


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", required=True)
    parser.add_argument("--port-file", required=True)
    parser.add_argument("--fault", choices=("none", "badsums", "truncate", "slow"), default="none")
    parser.add_argument("--requests-file", default="")
    args = parser.parse_args()
    server = http.server.HTTPServer(("127.0.0.1", 0), Handler)
    server.root = pathlib.Path(args.root).resolve()
    server.fault = args.fault
    server.requests_file = args.requests_file
    pathlib.Path(args.port_file).write_text(str(server.server_address[1]), encoding="ascii")
    print(server.server_address[1], flush=True)
    try:
        server.serve_forever()
    finally:
        server.server_close()


if __name__ == "__main__":
    main()
