#!/bin/sh
set -eu
ROOT=$(CDPATH= cd -- "$(dirname "$0")/../../.." && pwd)
INSTALLER="$ROOT/scripts/dist/install.sh"
BINARY="$DRAWPLATE_TEST_BINARY"
RID="$DRAWPLATE_TEST_RID"
[ -n "$BINARY" ] && [ -n "$RID" ] || { echo "missing test binary or RID" >&2; exit 1; }
TMP=$(mktemp -d)
SERVER_PID=""
SERVER_INDEX=0
REQUESTS="$TMP/requests"
RELEASE="$TMP/release"
TAG_DIR="$RELEASE/download/v1.2.3"
mkdir -p "$TAG_DIR"

cleanup() {
    if [ -n "$SERVER_PID" ]; then kill "$SERVER_PID" 2>/dev/null || true; wait "$SERVER_PID" 2>/dev/null || true; fi
    rm -rf "$TMP"
}
trap cleanup 0
trap 'cleanup; exit 130' INT TERM HUP

make_archive() {
    folder=$1
    version=$2
    binary=$3
    name="drawplate-$version-$RID.tar.gz"
    mkdir -p "$folder"
    cp "$binary" "$folder/drawplate"
    cp "$ROOT/LICENSE" "$folder/LICENSE"
    cp "$ROOT/THIRD-PARTY-NOTICES.md" "$folder/THIRD-PARTY-NOTICES.md"
    tar -czf "$folder/$name" -C "$folder" drawplate LICENSE THIRD-PARTY-NOTICES.md
    python3 - "$folder" "$name" <<'PY'
import hashlib, pathlib, sys
folder = pathlib.Path(sys.argv[1])
name = sys.argv[2]
digest = hashlib.sha256((folder / name).read_bytes()).hexdigest()
(folder / "SHA256SUMS").write_text(f"{digest}  {name}\n", encoding="ascii")
PY
}

start_server() {
    fault=$1
    SERVER_INDEX=$((SERVER_INDEX + 1))
    PORT_FILE="$TMP/port-$SERVER_INDEX"
    SERVER_LOG="$TMP/server-$SERVER_INDEX.log"
    python3 -u "$ROOT/scripts/tests/serve_release.py" --root "$RELEASE" --port-file "$PORT_FILE" --requests-file "$REQUESTS" --fault "$fault" >"$SERVER_LOG" 2>&1 &
    SERVER_PID=$!
    attempt=0
    while [ ! -s "$SERVER_LOG" ]; do
        attempt=$((attempt + 1))
        [ "$attempt" -lt 100 ] || { cat "$SERVER_LOG" >&2; echo "release server did not start" >&2; exit 1; }
        sleep 0.1
    done
    PORT=$(head -n 1 "$SERVER_LOG")
    case "$PORT" in *[!0-9]*|'') cat "$SERVER_LOG" >&2; echo "invalid release server port" >&2; exit 1 ;; esac
    BASE_URL="http://127.0.0.1:$PORT/releases"
}

stop_server() {
    if [ -n "$SERVER_PID" ]; then
        kill "$SERVER_PID" 2>/dev/null || true
        wait "$SERVER_PID" 2>/dev/null || true
        SERVER_PID=""
    fi
}

run_installer() {
    home=$1
    bin=$2
    version=$3
    output=$4
    errors=$5
    env HOME="$home" DRAWPLATE_BASE_URL="$BASE_URL" DRAWPLATE_VERSION="$version" DRAWPLATE_INSTALL_DIR="$bin" DRAWPLATE_NO_SETUP=1 sh "$INSTALLER" >"$output" 2>"$errors"
}

make_archive "$TAG_DIR" 1.2.3 "$BINARY"
start_server none
HOME_MAIN="$TMP/home-main"
BIN_MAIN="$TMP/bin-main"

# T-INS-01 fresh install.
run_installer "$HOME_MAIN" "$BIN_MAIN" v1.2.3 "$TMP/first.out" "$TMP/first.err"
test -x "$BIN_MAIN/drawplate"
test "$("$BIN_MAIN/drawplate" --version)" = "drawplate 1.2.3"
test -s "$HOME_MAIN/.local/share/drawplate/install.json"
grep -q 'Drawplate installer 1.2.3' "$TMP/first.out"

# T-INS-02 no-op repeat.
before=$(wc -l < "$REQUESTS")
run_installer "$HOME_MAIN" "$BIN_MAIN" v1.2.3 "$TMP/repeat.out" "$TMP/repeat.err"
after=$(wc -l < "$REQUESTS")
test "$before" -eq "$after"
grep -q 'up to date' "$TMP/repeat.out"

# T-INS-03 repair.
printf 'truncated' > "$BIN_MAIN/drawplate"
run_installer "$HOME_MAIN" "$BIN_MAIN" v1.2.3 "$TMP/repair.out" "$TMP/repair.err"
grep -q 'repairing' "$TMP/repair.out"
test "$("$BIN_MAIN/drawplate" --version)" = "drawplate 1.2.3"

# T-INS-04 bad sums.
stop_server
start_server badsums
set +e
run_installer "$TMP/home-badsums" "$TMP/bin-badsums" v1.2.3 "$TMP/badsums.out" "$TMP/badsums.err"
status=$?
set -e
test "$status" -eq 13
grep -q 'Checksum mismatch' "$TMP/badsums.err"
test ! -e "$TMP/bin-badsums/drawplate"
stop_server

# T-INS-05 truncated archive and retry.
stop_server
start_server truncate
set +e
run_installer "$TMP/home-truncate" "$TMP/bin-truncate" v1.2.3 "$TMP/truncate.out" "$TMP/truncate.err"
status=$?
set -e
test "$status" -eq 12
test ! -e "$TMP/bin-truncate/drawplate"
stop_server
start_server none
run_installer "$TMP/home-truncate" "$TMP/bin-truncate" v1.2.3 "$TMP/retry.out" "$TMP/retry.err"
test "$("$TMP/bin-truncate/drawplate" --version)" = "drawplate 1.2.3"

# T-INS-06 restore previous executable on failed new version.
BAD_DIR="$RELEASE/download/v1.2.4"
mkdir -p "$BAD_DIR"
printf '#!/bin/sh\nexit 1\n' > "$BAD_DIR/drawplate"
chmod +x "$BAD_DIR/drawplate"
cp "$ROOT/LICENSE" "$BAD_DIR/LICENSE"
cp "$ROOT/THIRD-PARTY-NOTICES.md" "$BAD_DIR/THIRD-PARTY-NOTICES.md"
tar -czf "$BAD_DIR/drawplate-1.2.4-$RID.tar.gz" -C "$BAD_DIR" drawplate LICENSE THIRD-PARTY-NOTICES.md
python3 - "$BAD_DIR" "$RID" <<'PY'
import hashlib, pathlib, sys
folder = pathlib.Path(sys.argv[1])
name = "drawplate-1.2.4-" + sys.argv[2] + ".tar.gz"
digest = hashlib.sha256((folder / name).read_bytes()).hexdigest()
(folder / "SHA256SUMS").write_text(digest + "  " + name + "\n", encoding="ascii")
PY
old_hash=$(python3 - "$BIN_MAIN/drawplate" <<'PY'
import hashlib, pathlib, sys
print(hashlib.sha256(pathlib.Path(sys.argv[1]).read_bytes()).hexdigest())
PY
)
set +e
run_installer "$HOME_MAIN" "$BIN_MAIN" v1.2.4 "$TMP/bad-binary.out" "$TMP/bad-binary.err"
status=$?
set -e
test "$status" -eq 14
new_hash=$(python3 - "$BIN_MAIN/drawplate" <<'PY'
import hashlib, pathlib, sys
print(hashlib.sha256(pathlib.Path(sys.argv[1]).read_bytes()).hexdigest())
PY
)
test "$old_hash" = "$new_hash"
test "$("$BIN_MAIN/drawplate" --version)" = "drawplate 1.2.3"

# T-INS-07 live lock.
mkdir -p "$HOME_MAIN/.local/share/drawplate/.lock"
set +e
run_installer "$HOME_MAIN" "$BIN_MAIN" v1.2.3 "$TMP/lock.out" "$TMP/lock.err"
status=$?
set -e
test "$status" -eq 15
grep -q 'Another installation is running' "$TMP/lock.err"
rmdir "$HOME_MAIN/.local/share/drawplate/.lock"

# T-INS-08 redirected output contract.
for marker in '25%' '50%' '75%' '100%'; do grep -q "$marker" "$TMP/first.out"; done
if grep -q "$(printf '\r')" "$TMP/first.out" || grep -q "$(printf '\033')" "$TMP/first.out"; then
    echo "terminal control character found" >&2
    exit 1
fi
if LC_ALL=C grep -q '[^ -~	]' "$TMP/first.out"; then
    echo "non-ASCII output found" >&2
    exit 1
fi

# T-INS-09 uninstall.
env HOME="$HOME_MAIN" DRAWPLATE_INSTALL_DIR="$BIN_MAIN" sh "$INSTALLER" --uninstall >"$TMP/uninstall.out"
test ! -e "$BIN_MAIN/drawplate"
test ! -e "$HOME_MAIN/.local/share/drawplate/install.json"
grep -q 'FFmpeg was not touched' "$TMP/uninstall.out"

# T-INS-12 latest redirect.
run_installer "$TMP/home-latest" "$TMP/bin-latest" "" "$TMP/latest.out" "$TMP/latest.err"
grep -q 'Finding release.*v1.2.3' "$TMP/latest.out"
test "$("$TMP/bin-latest/drawplate" --version)" = "drawplate 1.2.3"

sh "$INSTALLER" --help >"$TMP/help.out"
grep -q 'Usage: install.sh' "$TMP/help.out"
echo "POSIX installer tests passed: T-INS-01..09 and T-INS-12"
