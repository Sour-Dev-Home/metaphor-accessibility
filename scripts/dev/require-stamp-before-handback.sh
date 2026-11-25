#!/usr/bin/env bash
# SubagentStop hook (#999): keeps a subagent from handing back while its working tree has staged changes in the
# security-review scope and no stamp. On 2026-11-22 and 2026-11-24 agents handed back that way, so the review's stamp
# never reached a commit (docs/agents/workflow.md, the guards' steps).
# Claude Code passes the stop as JSON on stdin; its `cwd` is the subagent's working tree and `agent_type` its agent.
# Exit 2 blocks the stop and shows stderr to the subagent as its next instruction; exit 0 lets it stop. Quiet and fast
# when nothing in the scope is staged. Read-only agents never commit, so they always pass, the reviewer above all: it
# stops without a stamp whenever it has findings.
set -uo pipefail

input="$(cat)"

# field NAME: the string value of NAME in the JSON input, with \\ and \" unescaped (no jq on a stock Git Bash).
field() {
  local value
  value="$(grep -o "\"$1\"[[:space:]]*:[[:space:]]*\"\([^\"\\\\]\|\\\\.\)*\"" <<<"$input" | head -n 1)" || return 0
  value="${value#*:}"
  value="${value#"${value%%[![:space:]]*}"}"
  value="${value#\"}"
  value="${value%\"}"
  value="${value//\\\"/\"}"
  printf '%s' "${value//\\\\/\\}"
}

case "$(field agent_type)" in
  security-reviewer | Explore | Plan | claude-code-guide | web-fetch | statusline-setup) exit 0 ;;
esac

cwd="$(field cwd)"
if [ -n "$cwd" ]; then
  command -v cygpath > /dev/null 2>&1 && cwd="$(cygpath -u "$cwd")"
  cd "$cwd" 2> /dev/null || exit 0 # a folder that's gone holds nothing staged
fi
git rev-parse --is-inside-work-tree > /dev/null 2>&1 || exit 0

stamp="$(dirname "${BASH_SOURCE[0]}")/../ci/security-stamp.sh"
paths="$(bash "$stamp" touches)"
case $? in
  0) ;;
  1) exit 0 ;; # nothing in the scope is staged
  *) echo "Couldn't tell whether the staged changes touch the security-review scope (security-stamp.sh touches" \
       "failed); check with 'bash scripts/ci/security-stamp.sh touches' before you hand back." >&2
     exit 2 ;;
esac
bash "$stamp" stamped && exit 0

{
  echo "Staged changes in the security-review scope have no stamp:"
  sed 's/^/  /' <<<"$paths"
  echo "Stay in your turn: poll 'bash scripts/ci/security-stamp.sh stamped' every minute until the foreground" \
    "security-reviewer stamps, then commit (docs/agents/workflow.md, the guards' steps). If it reported findings, fix" \
    "them, restage and review again. If you can't get a stamp, unstage the scope files (git restore --staged" \
    "<paths>) and say so in your report."
} >&2
exit 2
