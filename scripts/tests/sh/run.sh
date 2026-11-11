#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname "$0")/../../.." && pwd)
INSTALLER="$ROOT/scripts/dist/install.sh"
BINARY="${DRAWPLATE_TEST_BINARY:?Set DRAWPLATE_TEST_BINARY to the published binary}"
RID="${DRAWPLATE_TEST_RID:?Set DRAWPLATE_TEST_RID}"
TMP=$(mktemp -d)
SERVER_PID=""
cleanup() {
    if [ -n "$SERVER_PID" ]; then kill "$SERVER_PID" 2>/dev/null || true; fi
    rm -rf "$TMP"
}
trap cleanup 0
trap 'cleanup; exit 130' INT TERM HUP
RELEASE="$TMP/release"
TAG_DIR="$RELEASE/download/v1.2.3"
mkdir -p "$TAG_DIR" "$TMP/home"
cp "$BINARY" "$TAG_DIR/drawplate"
cp "$ROOT/LICENSE" "$TAG_DIR/LICENSE"
cp "$ROOT/THIRD-PARTY-NOTICES.md" "$TAG_DIR/THIRD-PARTY-NOTICES.md"
tar -czf "$TAG_DIR/drawplate-1.2.3-$RID.tar.gz" -C "$TAG_DIR" drawplate LICENSE THIRD-PARTY-NOTICES.md
python3 - "$TAG_DIR" "$RID" <<'PY'
import hashlib, pathlib, sys
folder = pathlib.Path(sys.argv[1])
name = f"drawplate-1.2.3-{sys.argv[2]}.tar.gz"
digest = hashlib.sha256((folder / name).read_bytes()).hexdigest()
(folder / "SHA256SUMS").write_text(f"{digest}  {name}\n", encoding="ascii")
PY
PORT_FILE="$TMP/port"
REQUESTS="$TMP/requests"
python3 "$ROOT/scripts/tests/serve_release.py" --root "$RELEASE" --port-file "$PORT_FILE" --requests-file "$REQUESTS" &
SERVER_PID=$!
i=0
while [ ! -s "$PORT_FILE" ]; do
    i=$((i + 1))
    [ "$i" -lt 50 ] || { echo "server did not start" >&2; exit 1; }
    sleep 0.1
done
BASE_URL="http://127.0.0.1:$(cat "$PORT_FILE")/releases"
HOME="$TMP/home" DRAWPLATE_BASE_URL="$BASE_URL" DRAWPLATE_VERSION=v1.2.3 DRAWPLATE_INSTALL_DIR="$TMP/bin" DRAWPLATE_NO_SETUP=1 sh "$INSTALLER" >"$TMP/install.out"
test -x "$TMP/bin/drawplate"
test "$("$TMP/bin/drawplate" --version)" = "drawplate 1.2.3"
grep -q '\[1/8\]' "$TMP/install.out"
before=$(wc -l < "$REQUESTS")
HOME="$TMP/home" DRAWPLATE_BASE_URL="$BASE_URL" DRAWPLATE_VERSION=v1.2.3 DRAWPLATE_INSTALL_DIR="$TMP/bin" DRAWPLATE_NO_SETUP=1 sh "$INSTALLER" >"$TMP/repeat.out"
after=$(wc -l < "$REQUESTS")
test "$before" -eq "$after"
grep -q '\[skipped\] up to date' "$TMP/repeat.out"
mkdir -p "$TMP/home/.local/share/drawplate/.lock"
set +e
HOME="$TMP/home" DRAWPLATE_BASE_URL="$BASE_URL" DRAWPLATE_VERSION=v1.2.3 DRAWPLATE_INSTALL_DIR="$TMP/bin" sh "$INSTALLER" >"$TMP/locked.out" 2>"$TMP/locked.err"
status=$?
set -e
test "$status" -eq 15
grep -q 'Another installation is running' "$TMP/locked.err"
echo "POSIX installer smoke tests passed"
