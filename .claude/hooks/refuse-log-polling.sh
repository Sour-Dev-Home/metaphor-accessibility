#!/usr/bin/env bash
# PreToolUse hook (Bash and PowerShell): refuses a loop that sleeps while it waits on a log file, such as
# `until grep -q '^real' run.log; do sleep 10; done`. On 2026-11-18 a subagent stacked about ten of these while its own
# test run was still going, and each one held a shell (docs/agents/workflow.md, Start step 4). Polling GitHub with gh
# stays allowed: that is a remote state the harness can't report.
# Claude Code passes the tool call as JSON on stdin; exit 2 refuses it and shows stderr to the agent.
input="$(cat)"
case "$input" in
  *sleep* | *Start-Sleep*) ;;
  *) exit 0 ;;
esac
case "$input" in
  *until* | *while*) ;;
  *) exit 0 ;;
esac
case "$input" in
  *.log*) ;;
  *) exit 0 ;;
esac
echo "Refused: a sleep loop that waits on a log file. Run the command itself, alone in its own call, and read" \
  "its output when it returns or its completion notification arrives (docs/agents/workflow.md, Start step 4)." >&2
exit 2
