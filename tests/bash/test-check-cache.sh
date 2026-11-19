#!/usr/bin/env bash
# Black-box tests for the pass cache in scripts/check.sh (issue #241): `full` reuses a recorded pass on an identical
# tree, and `pre-push` takes the build lock only when it builds. A stand-in `dotnet` counts its calls.
set -uo pipefail
unset PII_PATTERNS REQUIRE_PRIVATE_PATTERNS REQUIRE_DOCS_TOOLS GITHUB_ACTIONS SECURITY_REVIEW_SKIP

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
# A copy of the scripts whose docs-tools installer, which `full` runs (#353), does nothing.
mkdir -p "$WORK/gate"
cp -r "$ROOT/scripts" "$WORK/gate/"
printf '#!/usr/bin/env bash\nexit 0\n' > "$WORK/gate/scripts/setup/install-docs-tools.sh"
CHECK="$WORK/gate/scripts/check.sh"

passed=0
failed=0

mkdir -p "$WORK/bin"
cat > "$WORK/bin/dotnet" <<'EOF'
#!/usr/bin/env bash
echo "$1" >> "$DOTNET_LOG"
[ -z "${DOTNET_FAIL:-}" ]
EOF
chmod +x "$WORK/bin/dotnet"
export PATH="$WORK/bin:$PATH" DOTNET_LOG="$WORK/dotnet.log" SKIP_SCRIPT_TESTS=1
export METAPHOR_CHECK_LOCK="$WORK/lock"

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}
has() { [[ "$out" == *"$1"* ]]; }
lacks() { [[ "$out" != *"$1"* ]]; }
builds() { grep -c '^build$' "$DOTNET_LOG" 2> /dev/null || true; }

new_repo() {
  rm -rf "$WORK/repo" "$WORK/lock" "$DOTNET_LOG"
  mkdir -p "$WORK/repo" && cd "$WORK/repo" || exit 2
  git init -q -b main
  git config user.name test && git config user.email test@example.invalid
  git config core.autocrlf false && git config commit.gpgsign false
  : > Metaphor.sln
  echo one > notes.md
  mkdir src && echo 'class A {}' > src/a.cs
  git add -A && git commit -q -m "chore: seed"
  git update-ref refs/remotes/origin/main HEAD
}
run() {
  out="$(bash "$CHECK" "$@" 2>&1 <<< "${STDIN:-}")"
  code=$?
}

# full: miss, then hit, then miss after a change
new_repo
run full
check "first full passes" [ "$code" -eq 0 ]
check "first full builds" [ "$(builds)" -eq 1 ]
tree="$(git write-tree)"
run full
check "second full passes" [ "$code" -eq 0 ]
check "second full reports the reuse and the tree" has "reused"
check "second full names the tree" has "$tree"
check "second full does not build again" [ "$(builds)" -eq 1 ]

echo two >> notes.md && git add notes.md
run full
check "a staged change runs the checks again" [ "$(builds)" -eq 2 ]
git commit -q -m "docs: change"
run full
check "a committed tree already passed is reused" has "reused"
echo three >> notes.md
run full
check "an unstaged change is not served from the cache" [ "$(builds)" -eq 3 ]
git checkout -q notes.md

# a failed run records nothing
new_repo
DOTNET_FAIL=1 run full
check "failing full fails" [ "$code" -eq 1 ]
run full
check "a failed run is not reused" lacks "reused"
check "the cache lives under the git dir" [ -n "$(ls "$(git rev-parse --absolute-git-dir)" | grep -i check)" ]

# pre-push
new_repo
echo two >> notes.md && git commit -q -am "docs: more"
head="$(git rev-parse HEAD)"
mkdir -p "$WORK/lock" && echo "$$" > "$WORK/lock/pid" # a live holder: a docs-only push must not wait for it
STDIN="refs/heads/x $head refs/heads/x 0000000000000000000000000000000000000000"
timeout 20 bash "$CHECK" pre-push <<< "$STDIN" > "$WORK/out" 2>&1
code=$?
out="$(cat "$WORK/out")"
check "docs-only pre-push passes without the lock" [ "$code" -eq 0 ]
check "docs-only pre-push says it skipped" has "skipped"
check "docs-only pre-push never waited" lacks "waiting for another"
rm -rf "$WORK/lock"

echo 'class B {}' > src/b.cs && git add -A && git commit -q -m "feat: code"
head="$(git rev-parse HEAD)"
STDIN="refs/heads/x $head refs/heads/x 0000000000000000000000000000000000000000"
run pre-push
check "code pre-push builds" [ "$(builds)" -eq 1 ]
run pre-push
check "pre-push on the same tree reuses the pass" has "reused"
check "pre-push reuse does not build" [ "$(builds)" -eq 1 ]

new_repo
echo 'class B {}' > src/b.cs && git add -A && git commit -q -m "feat: code"
run full
head="$(git rev-parse HEAD)"
STDIN="refs/heads/x $head refs/heads/x 0000000000000000000000000000000000000000"
run pre-push
check "a recorded full pass covers pre-push" has "reused"

printf 'check cache tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
