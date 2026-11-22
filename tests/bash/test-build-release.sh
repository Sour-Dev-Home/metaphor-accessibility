#!/usr/bin/env bash
# Black-box tests for scripts/release/build-release.sh (#310), the one recipe CI's gate and the release workflow use to build the
# players' zip. A stand-in `dotnet` and `pwsh` replace the build and the zip (tests/pester/zip-package.Tests.ps1 covers
# the real zip), so these check what the script decides: the build names the commit as clean, the zip gets the
# commit's time and a SHA256SUMS, and a rebuild in a second checkout must match.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
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
# the package and the timestamp, and prints the result's SHA-256, as zip-package.ps1 does; for installer-exe.ps1 it
# "builds" the exe from the zip it is given (tests/pester/installer-exe.Tests.ps1 covers the real one), and
# FAKE_EXE_VARY makes each exe differ.
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
script="" path="" dest="" time="" zip=""
while [ $# -gt 0 ]; do
  case "$1" in
    -File) script="$2"; shift ;;
    -Path) path="$2"; shift ;;
    -Destination) dest="$2"; shift ;;
    -Timestamp) time="$2"; shift ;;
    -Zip) zip="$2"; shift ;;
  esac
  shift
done
mkdir -p "$(dirname "$dest")"
if [ "$(basename "$script")" = installer-exe.ps1 ]; then
  # The exe: the zip it carries, then "exe" (and a count with FAKE_EXE_VARY, so a rebuild differs).
  printf 'pwsh %s %s\n' "$script" "$zip" >> "$FAKE_LOG"
  [ -f "$zip" ] || exit 1
  n=""
  if [ -n "${FAKE_EXE_VARY:-}" ]; then
    n="$(cat "$FAKE_EXE_VARY" 2> /dev/null || echo 0)"
    echo $((n + 1)) > "$FAKE_EXE_VARY"
  fi
  { cat "$zip"; printf 'exe%s' "$n"; } > "$dest"
  sha256sum "$dest" | cut -d' ' -f1
  exit 0
fi
printf 'pwsh %s %s\n' "$script" "$time" >> "$FAKE_LOG"
{ (cd "$path" && find . -type f | LC_ALL=C sort | xargs cat); printf '%s' "$time"; } > "$dest"
sha256sum "$dest" | cut -d' ' -f1
EOF
chmod +x "$WORK/bin/dotnet" "$WORK/bin/pwsh"
export PATH="$WORK/bin:$PATH"
export FAKE_LOG="$WORK/log"

# A repository holding the scripts the recipe runs, with one commit at a known time.
REPO="$WORK/repo"
mkdir -p "$REPO/scripts/release" "$REPO/scripts/check" "$REPO/src/Metaphor.Hooks"
cp "$ROOT/scripts/release/build-release.sh" "$ROOT/scripts/release/package-mod.sh" \
  "$ROOT/scripts/release/zip-package.ps1" "$ROOT/scripts/release/installer-exe.ps1" "$REPO/scripts/release/" 2> /dev/null
cp "$ROOT/scripts/check/tree-hash.sh" "$REPO/scripts/check/" 2> /dev/null
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
  out="$(cd "$REPO" && bash scripts/release/build-release.sh "$@" 2> "$WORK/err")"
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
exe="$WORK/out/release/$NAME-installer.exe"
check "it writes the installer exe beside the zip" [ -f "$exe" ]
exe_sha="$(sha256sum "$exe" 2> /dev/null | cut -d' ' -f1)"
check "SHA256SUMS holds the zip's and the exe's hash and name, as sha256sum --check reads it" \
  [ "$(cat "$WORK/out/release/SHA256SUMS" 2> /dev/null)" = "$zip_sha  $NAME.zip"$'\n'"$exe_sha  $NAME-installer.exe" ]
check "SHA256SUMS checks out" bash -c 'cd "$1" && sha256sum --check --strict --quiet SHA256SUMS' _ "$WORK/out/release"
check "the release folder holds only the zip, the exe and SHA256SUMS" \
  [ "$(ls -A "$WORK/out/release" 2> /dev/null | LC_ALL=C sort | tr '\n' ' ')" = "SHA256SUMS $NAME-installer.exe $NAME.zip " ] # ls sorts by locale
check "the exe carries the release's zip" grep -q "^pwsh .*/installer-exe\.ps1 $zip$" "$FAKE_LOG"
check "the rebuild builds its exe with its own checkout's script" \
  [ "$(grep '/installer-exe\.ps1 ' "$FAKE_LOG" | cut -d' ' -f2 | sort -u | wc -l)" -eq 2 ]
check "the rebuild builds its exe around its own zip" \
  [ "$(grep '/installer-exe\.ps1 ' "$FAKE_LOG" | cut -d' ' -f3 | sort -u | wc -l)" -eq 2 ]
check "both zips take the commit's time" [ "$(grep -c '^pwsh .* 1700000000$' "$FAKE_LOG")" -eq 2 ]
check "it builds twice: the package and the rebuild" [ "$(grep -c '^dotnet publish' "$FAKE_LOG")" -eq 2 ]
check "the rebuild zips with its own checkout's script" \
  [ "$(grep '/zip-package\.ps1 ' "$FAKE_LOG" | cut -d' ' -f2 | sort -u | wc -l)" -eq 2 ]
check "it says the rebuild matched" grep -q "rebuild in a second checkout .* matches" <<<"$err"
check "it removes the second checkout" [ "$(git -C "$REPO" worktree list | wc -l)" -eq 1 ]

summary="$WORK/summary.md"
: > "$summary"
GITHUB_STEP_SUMMARY="$summary" run "$NAME" "$WORK/out"
check "on CI it writes the zip's hash and the commit to the run summary" \
  grep -q "$NAME.zip.*$zip_sha.*$COMMIT" "$summary"
check "on CI it writes the exe's hash and the commit to the run summary" \
  grep -q "$NAME-installer.exe.*$exe_sha.*$COMMIT" "$summary"

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

# An exe that rebuilds differently fails the same way (#613), though package and zip matched.
FAKE_EXE_VARY="$WORK/exe-vary" run "$NAME" "$WORK/out"
rm -f "$WORK/exe-vary"
check "an exe that rebuilds differently fails" [ "$status" -eq 1 ]
check "an exe that rebuilds differently says so" grep -q "Rebuild differs: the installer exe" <<<"$err"
check "an exe that rebuilds differently still removes the second checkout" \
  [ "$(git -C "$REPO" worktree list | wc -l)" -eq 1 ]

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
