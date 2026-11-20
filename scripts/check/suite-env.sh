#!/usr/bin/env bash
# The check-script suites' environment (#455): every suite sources this once ROOT is set, right after
# scripts/check/wsl-run.sh, so the contributor's git config and shell can't change what a suite's git or check runs do.
# Sourced; builtins only.
#
# - git reads scripts/check/suite.gitconfig as its global config and no system config: a contributor's commit signing,
#   global hooks (core.hooksPath, init.templateDir), log.showSignature, init.defaultBranch or core.autocrlf don't reach
#   the suites' scratch repos, and those repos get a fixed identity. A repo's own config still applies.
# - The check's switches, and git's identity and config variables, are unset: a case that needs one sets it. The
#   runners' own variables (METAPHOR_NO_WSL, METAPHOR_TEST_JOBS, METAPHOR_LIVE_LOG, METAPHOR_LIVE_PID,
#   METAPHOR_CHECK_HELD) stay.

export GIT_CONFIG_GLOBAL="$ROOT/scripts/check/suite.gitconfig" GIT_CONFIG_NOSYSTEM=1
unset GIT_CONFIG GIT_CONFIG_PARAMETERS GIT_CONFIG_COUNT GIT_CONFIG_SYSTEM \
  GIT_AUTHOR_NAME GIT_AUTHOR_EMAIL GIT_AUTHOR_DATE GIT_COMMITTER_NAME GIT_COMMITTER_EMAIL GIT_COMMITTER_DATE \
  PII_PATTERNS REQUIRE_PRIVATE_PATTERNS REQUIRE_DOCS_TOOLS SECURITY_REVIEW_SKIP SKIP_SCRIPT_TESTS \
  GITHUB_ACTIONS GITHUB_STEP_SUMMARY \
  METAPHOR_SCAN_ALL METAPHOR_FULL_TESTS METAPHOR_CHECK_SLOTS METAPHOR_CHECK_LOCK METAPHOR_CHECK_POLL \
  METAPHOR_CHECK_NOW METAPHOR_CHECK_PS_CMD METAPHOR_PESTER_BATCH METAPHOR_GAME_EXE METAPHOR_STATUS_URL
