#!/usr/bin/env bash
# Tests for scripts/check/suite-env.sh (#455): under a contributor's hostile global git config and shell (commit
# signing with an unusable signer, failing global hooks, another default branch, the check's switches exported), a
# suite's scratch repos still commit and push, and every suite sources it.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}
has() { [[ "$out" == *"$1"* ]]; }
lacks() { [[ "$out" != *"$1"* ]]; }

# The hostile config. Its paths in the form this git reads: C:/... in Git Bash.
HOSTILE="$WORK/hostile"
mkdir -p "$HOSTILE/hooks" "$HOSTILE/template/hooks" || exit 2
hostile_dir="$(cd "$HOSTILE" && { pwd -W 2> /dev/null || pwd; })"
for hook in pre-commit commit-msg pre-push; do
  printf '#!/bin/sh\necho "global %s hook ran" >&2\nexit 1\n' "$hook" > "$HOSTILE/hooks/$hook"
  cp "$HOSTILE/hooks/$hook" "$HOSTILE/template/hooks/$hook"
done
chmod +x "$HOSTILE"/hooks/* "$HOSTILE"/template/hooks/* || exit 2
cat > "$HOSTILE/gitconfig" << EOF || exit 2
[user]
	name = hostile
	email = hostile@example.invalid
[commit]
	gpgsign = true
[gpg]
	program = false
[core]
	hooksPath = $hostile_dir/hooks
	autocrlf = true
[init]
	templateDir = $hostile_dir/template
	defaultBranch = trunk
[log]
	showSignature = true
EOF

# scenario [--isolated]: in a subshell with the hostile config and shell (suite-env.sh sourced on top with
# --isolated), init a repo, commit, push to a bare remote, and print what a suite would see.
scenario() {
  (
    export GIT_CONFIG_GLOBAL="$HOSTILE/gitconfig" METAPHOR_SCAN_ALL=1 METAPHOR_CHECK_SLOTS=9 GIT_AUTHOR_NAME=hostile
    # shellcheck source=../../scripts/check/suite-env.sh
    [ "${1:-}" != --isolated ] || source "$ROOT/scripts/check/suite-env.sh"
    local repo="$WORK/repo-$RANDOM-$RANDOM"
    git init -q --bare "$repo.git" && git init -q "$repo" && cd "$repo" || exit 2
    printf 'a\n' > a.txt && git add a.txt && git commit -q -m 'chore: start' && echo committed
    git push -q "$repo.git" HEAD 2>&1 && echo pushed
    echo "branch $(git branch --show-current), author $(git log -1 --format=%an 2> /dev/null)"
    echo "scan-all ${METAPHOR_SCAN_ALL:-unset}, slots ${METAPHOR_CHECK_SLOTS:-unset}"
  ) 2>&1
}

# The hostile config is hostile: without suite-env.sh, its signer and hooks stop the commit.
out="$(scenario)"
check "the hostile config stops a commit without suite-env.sh" lacks committed
check "the hostile shell reaches a suite without suite-env.sh" has "scan-all 1"

out="$(scenario --isolated)"
check "a commit needs no signer and runs no global hook" has committed
check "a push runs no global hook" has pushed
check "no global hook ran" lacks "hook ran"
check "the default branch and identity are the suites' own" has "branch main, author test"
check "the check's switches are unset" has "scan-all unset, slots unset"

# Every suite sources it, so one run by itself is isolated too.
while read -r suite; do
  check "$suite sources suite-env.sh" grep -q '^source "$ROOT/scripts/check/suite-env.sh"' "$ROOT/$suite"
done < <(bash "$ROOT/scripts/check/run-check-suites.sh" --list)

echo "test-suite-env: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
