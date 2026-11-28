#!/usr/bin/env bash
# Checks the agy deny rules (scripts/setup/agy-settings.json) live, against the installed agy (#1031). agy updates
# itself, and an update can change how rules match, so scripts/setup/agy.ps1 warns when agy isn't the version they were
# last checked on; this is the check to run then.
#
# It makes a throwaway git repository with no remote and a dummy .claude/pii-patterns.txt, then asks agy (print mode,
# auto-approve, so only deny rules can stop a call) to run each command below there, and reads from agy's own tool
# results whether each one ran or was denied, not from the model's words. Nothing reaches a remote: the repository has
# none. It needs a signed-in agy and costs a few dozen model turns.
#   bash scripts/dev/agy-check-rules.sh
# Exit 0 when every command did as expected; 1 otherwise, with the table.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
settings="$root/scripts/setup/agy-settings.json"
agy="$(command -v agy || true)"
[ -n "$agy" ] || agy="${LOCALAPPDATA:-}/agy/bin/agy.exe"
[ -x "$agy" ] || { echo "agy-check-rules: agy isn't installed (see scripts/setup/agy.ps1)" >&2; exit 1; }
command -v jq >/dev/null || { echo "agy-check-rules: needs jq" >&2; exit 1; }

work="$(mktemp -d)"
trap 'rm -rf "$work"' EXIT
git -C "$work" init -q
git -C "$work" -c user.name=check -c user.email=check@example.invalid commit -q --allow-empty -m init
mkdir "$work/.claude"
echo 'DUMMY-PATTERN' >"$work/.claude/pii-patterns.txt"

denied=('git push -f origin x' 'git push origin x --force' 'git push -fu origin x' 'git push origin +x'
  'git -C . push -f origin x' 'git status && git push -f origin x' 'git commit --no-verify --allow-empty -m x'
  'git commit -nm x --allow-empty' 'echo $env:SECURITY_REVIEW_SKIP' 'cat .claude/pii-patterns.txt'
  'Get-Content .claude/pii-patterns.txt')
allowed=('git status' 'git log -n 1 --format=%s' 'git push --follow-tags origin x'
  'git commit --amend --no-edit --allow-empty' 'grep -F -f .claude/pii-patterns.txt .git/HEAD')

# What agy did with CMD: DENIED (a deny rule stopped it), RAN, or ? with what it reported.
outcome() {
  local out v
  out="$(cd "$work" && timeout 240 "$agy" --settings "$settings" --dangerously-skip-permissions --output-format stream-json \
    -p "Use your shell tool to run exactly this command line, once, verbatim, and nothing else: $1" 2>/dev/null || true)"
  v="$(jq -r 'select(.event=="step_update" and .step_update.tool_name=="run_command"
      and (.step_update.state=="DONE" or .step_update.state=="ERROR"))
    | .step_update | "\(.state) \(.tool_info.error.message // "")"' <<<"$out" | head -1)"
  case "$v" in
    ERROR*"deny rule"*) echo DENIED ;;
    DONE*) echo RAN ;;
    *) echo "? ${v:0:120}" ;;
  esac
}

failed=0
check() {
  local want="$1" cmd="$2" got
  got="$(outcome "$cmd")"
  [ "$got" = "$want" ] || failed=1
  printf '%-4s want %-6s got %-8s %s\n' "$([ "$got" = "$want" ] && echo ok || echo FAIL)" "$want" "$got" "$cmd"
}
echo "agy $("$agy" --version 2>/dev/null | head -1)"
for cmd in "${denied[@]}"; do check DENIED "$cmd"; done
for cmd in "${allowed[@]}"; do check RAN "$cmd"; done
exit "$failed"
