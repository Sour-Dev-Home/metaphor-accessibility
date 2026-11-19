#!/usr/bin/env bash
# Black-box tests for scripts/build-release.sh (#310), the one recipe CI's gate and the release workflow use to build the
# players' zip. A stand-in `dotnet` and `pwsh` replace the build and the zip (tests/smoke/zip-package.Tests.ps1 covers
# the real zip), so these check what the script decides: the build names the commit as clean, the zip gets the
# commit's time and a SHA256SUMS, and a rebuild in a second checkout must match.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/wsl-run.sh
source "$ROOT/scripts/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

# The stand-ins. dotnet "publishes" a package whose DLL names the checkout's commit, the way the real build stamps
# `+<commit>` into Metaphor.Hooks.dll; FAKE_SUFFIX, FAKE_COMMIT and FAKE_VARY bend that. pwsh "zips" by concatenating
# the package and the timestamp, and prints the result's SHA-256, as zip-package.ps1 does.
mkdir -p "$WORK/bin"
cat > "$WORK/bin/dotnet" << 'EOF'
#!/usr/bin/env bash
printf 'dotnet %s\n' "$*" >> "$FAKE_LOG"
out="" prev=""
for arg in "$@"; do [ "$prev" != -o ] || out="$arg"; prev="$arg"; done
mkdir -p "$out"
commit="$(git -C "$(dirname "$2")" rev-parse HEAD)"
printf '{"ModId":"metaphor.accessibility"}' > "$out/ModConfig.json"
printf 'Metaphor.Hooks 1.0.0+%s%s' "${FAKE_COMMIT:-$commit}" "${FAKE_SUFFIX:-}" > "$out/Metaphor.Hooks.dll"
if [ -n "${FAKE_VARY:-}" ]; then
  n="$(cat "$FAKE_VARY" 2> /dev/null || echo 0)"
  echo $((n + 1)) > "$FAKE_VARY"
  printf '%s' "$n" > "$out/build.txt"
fi
EOF
cat > "$WORK/bin/pwsh" << 'EOF'
#!/usr/bin/env bash
script="" path="" dest="" time=""
while [ $# -gt 0 ]; do
  case "$1" in
    -File) script="$2"; shift ;;
    -Path) path="$2"; shift ;;
    -Destination) dest="$2"; shift ;;
    -Timestamp) time="$2"; shift ;;
  esac
  shift
done
printf 'pwsh %s %s\n' "$script" "$time" >> "$FAKE_LOG"
mkdir -p "$(dirname "$dest")"
{ (cd "$path" && find . -type f | LC_ALL=C sort | xargs cat); printf '%s' "$time"; } > "$dest"
sha256sum "$dest" | cut -d' ' -f1
EOF
chmod +x "$WORK/bin/dotnet" "$WORK/bin/pwsh"
export PATH="$WORK/bin:$PATH"
export FAKE_LOG="$WORK/log"

# A repository holding the scripts the recipe runs, with one commit at a known time.
REPO="$WORK/repo"
mkdir -p "$REPO/scripts" "$REPO/src/Metaphor.Hooks"
cp "$ROOT/scripts/build-release.sh" "$ROOT/scripts/package-mod.sh" "$ROOT/scripts/tree-hash.sh" \
  "$ROOT/scripts/zip-package.ps1" "$REPO/scripts/" 2> /dev/null
: > "$REPO/src/Metaphor.Hooks/Metaphor.Hooks.csproj"
git -C "$REPO" init -q
git -C "$REPO" add -A
GIT_AUTHOR_DATE=@1700000000 GIT_COMMITTER_DATE=@1700000000 \
  git -C "$REPO" -c user.name=test -c user.email=test@example.invalid commit -qm "fixture"
COMMIT="$(git -C "$REPO" rev-parse HEAD)"
NAME="metaphor-accessibility-v1.2.3"

run() { # run ARGS...: the recipe in the fixture repository; sets status, out and err
  : > "$FAKE_LOG"
  rm -rf "$WORK/out"
  out="$(cd "$REPO" && bash scripts/build-release.sh "$@" 2> "$WORK/err")"
  status=$?
  err="$(cat "$WORK/err")"
}

# A clean build: the package, the zip and SHA256SUMS, and the rebuild matched.
run "$NAME" "$WORK/out"
check "a clean build that rebuilds the same exits 0" [ "$status" -eq 0 ]
zip="$WORK/out/release/$NAME.zip"
check "it packages the mod under OUT_DIR/mod" [ -f "$WORK/out/mod/Metaphor.Hooks.dll" ]
check "it writes the zip under OUT_DIR/release" [ -f "$zip" ]
zip_sha="$(sha256sum "$zip" 2> /dev/null | cut -d' ' -f1)"
check "it prints the zip's SHA-256 last" [ "$(tail -n1 <<<"$out")" = "$zip_sha" ]
check "SHA256SUMS holds the zip's hash and name, as sha256sum --check reads it" \
  [ "$(cat "$WORK/out/release/SHA256SUMS" 2> /dev/null)" = "$zip_sha  $NAME.zip" ]
check "the release folder holds only the zip and SHA256SUMS" \
  [ "$(ls "$WORK/out/release" 2> /dev/null | tr '\n' ' ')" = "$NAME.zip SHA256SUMS " ]
check "both zips take the commit's time" [ "$(grep -c '^pwsh .* 1700000000$' "$FAKE_LOG")" -eq 2 ]
check "it builds twice: the package and the rebuild" [ "$(grep -c '^dotnet publish' "$FAKE_LOG")" -eq 2 ]
check "the rebuild zips with its own checkout's script" \
  [ "$(grep '^pwsh' "$FAKE_LOG" | cut -d' ' -f2 | sort -u | wc -l)" -eq 2 ]
check "it says the rebuild matched" grep -q "rebuild in a second checkout .* matches" <<<"$err"
check "it removes the second checkout" [ "$(git -C "$REPO" worktree list | wc -l)" -eq 1 ]

summary="$WORK/summary.md"
: > "$summary"
GITHUB_STEP_SUMMARY="$summary" run "$NAME" "$WORK/out"
check "on CI it writes the zip's hash and the commit to the run summary" \
  grep -q "$NAME.zip.*$zip_sha.*$COMMIT" "$summary"

# --no-build packages what the caller already built and tested; the rebuild always builds from scratch.
run --no-build "$NAME" "$WORK/out"
check "--no-build exits 0" [ "$status" -eq 0 ]
check "--no-build reaches the first package only" \
  [ "$(grep '^dotnet publish' "$FAKE_LOG" | grep -c -- '--no-build')" -eq 1 ]

# The build must name this commit as a clean build, or nothing is zipped.
FAKE_SUFFIX=-dirty run "$NAME" "$WORK/out"
check "a dirty build fails" [ "$status" -eq 1 ]
check "a dirty build says why" grep -q "doesn't name commit $COMMIT as a clean build" <<<"$err"
check "a dirty build leaves no zip" [ ! -e "$zip" ]
FAKE_COMMIT=0123456789abcdef0123456789abcdef01234567 run "$NAME" "$WORK/out"
check "a build naming another commit fails" [ "$status" -eq 1 ]

# A rebuild that differs fails, and the second checkout still goes.
FAKE_VARY="$WORK/vary" run "$NAME" "$WORK/out"
rm -f "$WORK/vary"
check "a rebuild that differs fails" [ "$status" -eq 1 ]
check "a rebuild that differs says so" grep -q "Rebuild differs" <<<"$err"
check "a failed rebuild still removes the second checkout" [ "$(git -C "$REPO" worktree list | wc -l)" -eq 1 ]

# The name becomes a file name in the release, so only a plain one is taken.
for bad in "" "../escape" "a/b" "-flag" "with space"; do
  run "$bad" "$WORK/out"
  check "name '$bad' is refused as usage" [ "$status" -eq 2 ]
done
run "$NAME"
check "a missing OUT_DIR is refused as usage" [ "$status" -eq 2 ]
check "a refused call builds nothing" [ ! -s "$FAKE_LOG" ]

printf 'build-release tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
