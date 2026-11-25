#!/usr/bin/env bash
# Tests for scripts/dev/require-stamp-before-handback.sh (#999), the SubagentStop hook: it blocks a hand-back (exit 2)
# while scope files are staged with no stamp, and lets every other stop through quietly.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/check/wsl-run.sh
source "$ROOT/scripts/check/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)
# shellcheck source=../../scripts/check/suite-env.sh
source "$ROOT/scripts/check/suite-env.sh" # the contributor's git config and shell don't reach it (#455)
HOOK="$ROOT/scripts/dev/require-stamp-before-handback.sh"
STAMP="$ROOT/scripts/ci/security-stamp.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; printf '  %s\n' "$out"; fi
}

# new_repo DIR: a repository with one commit.
new_repo() {
  mkdir -p "$1" && git -C "$1" init -q && printf 'first\n' > "$1/readme.txt" &&
    git -C "$1" add readme.txt && git -C "$1" -c user.name=t -c user.email=t@example.invalid commit -q -m "docs: first"
}

# json_path PATH: PATH as a JSON string body, in the form Claude Code sends (a Windows path under Git Bash).
json_path() {
  local path="$1"
  command -v cygpath > /dev/null 2>&1 && path="$(cygpath -w "$path")"
  path="${path//\\/\\\\}"
  printf '%s' "${path//\"/\\\"}"
}

# stop DIR [AGENT]: run the hook for a stop of AGENT (default general-purpose) working in DIR, from another folder.
stop() {
  out="$(cd "$WORK" && printf '{"session_id":"s","hook_event_name":"SubagentStop","stop_hook_active":false,"cwd":"%s","agent_type":"%s"}' \
    "$(json_path "$1")" "${2:-general-purpose}" | bash "$HOOK" 2>&1)"
  code=$?
}

repo="$WORK/repo \"quoted\" dir"
new_repo "$repo"

stop "$repo"
check "nothing staged: passes quietly" [ "$code" -eq 0 ] && check "nothing staged: prints nothing" [ -z "$out" ]

printf 'notes\n' > "$repo/notes.md" && git -C "$repo" add notes.md
stop "$repo"
check "only files outside the scope staged: passes" [ "$code" -eq 0 ]

mkdir -p "$repo/src/Metaphor.Hooks" && printf 'class A {}\n' > "$repo/src/Metaphor.Hooks/A.cs"
git -C "$repo" add src/Metaphor.Hooks/A.cs
stop "$repo"
check "staged scope file without a stamp: blocks" [ "$code" -eq 2 ]
says() { [[ "$out" == *"$1"* ]]; } # says TEXT: the last run's output holds TEXT
check "the block names the file" says src/Metaphor.Hooks/A.cs
check "the block says to poll the stamp" says "security-stamp.sh stamped"

stop "$repo" security-reviewer
check "the reviewer itself stops without a stamp" [ "$code" -eq 0 ]
stop "$repo" Explore
check "a read-only agent passes" [ "$code" -eq 0 ]

(cd "$repo" && bash "$STAMP" stamp > /dev/null)
stop "$repo"
check "staged scope file with its stamp: passes" [ "$code" -eq 0 ]

printf 'class A { int x; }\n' > "$repo/src/Metaphor.Hooks/A.cs" && git -C "$repo" add src/Metaphor.Hooks/A.cs
stop "$repo"
check "an edit staged after the stamp: blocks again" [ "$code" -eq 2 ]

mkdir -p "$WORK/plain"
stop "$WORK/plain"
check "a folder outside git: passes" [ "$code" -eq 0 ]
stop "$WORK/gone"
check "a folder that's gone: passes" [ "$code" -eq 0 ]

printf 'require-stamp-before-handback tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
