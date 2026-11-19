#!/usr/bin/env bash
# Checks the policy files a contributor or GitHub looks for: the licenses (GPL-3.0 for code at the root, where GitHub
# detects it; CC BY 4.0 for docs), SECURITY.md, the code of conduct, the issue templates, and AGENTS.md as the one
# agent instruction file that CLAUDE.md only imports.
set -uo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=../../scripts/wsl-run.sh
source "$ROOT/scripts/wsl-run.sh" # in Git Bash, runs this suite in WSL instead when it's set up (#341)

passed=0
failed=0

check() { # check NAME CONDITION...
  local name="$1"
  shift
  if "$@"; then passed=$((passed + 1)); else failed=$((failed + 1)); echo "FAIL $name"; fi
}

check "LICENSE is the GPL-3.0 text" grep -qF "GNU GENERAL PUBLIC LICENSE" "$ROOT/LICENSE"
check "LICENSE is version 3" grep -qF "Version 3, 29 June 2007" "$ROOT/LICENSE"
check "docs/LICENSE is the CC BY 4.0 text" grep -qF "Attribution 4.0 International" "$ROOT/docs/LICENSE"

# Every agent reads the same rules: CLAUDE.md holds nothing but the import, so no rule can live only there.
check "AGENTS.md exists" [ -s "$ROOT/AGENTS.md" ]
check "CLAUDE.md only imports AGENTS.md" \
  bash -c '[ "$(tr -d "\r" <"$1" | grep -v "^[[:space:]]*$")" = "@AGENTS.md" ]' _ "$ROOT/CLAUDE.md"

# Vulnerabilities go to GitHub's private reporting, never a public issue.
check "SECURITY.md links to private vulnerability reporting" \
  grep -qF "/security/advisories/new" "$ROOT/.github/SECURITY.md"
check "the code of conduct exists" [ -s "$ROOT/.github/CODE_OF_CONDUCT.md" ]
check "CONTRIBUTING.md exists" [ -s "$ROOT/.github/CONTRIBUTING.md" ]

# The docs-lint configs live in tools/docs/; check.sh passes their paths explicitly, and Vale's StylesPath (relative to
# the .vale.ini) must still land on our rules.
check "the markdownlint config is in tools/docs" [ -s "$ROOT/tools/docs/.markdownlint-cli2.jsonc" ]
check "the Vale config is in tools/docs" [ -s "$ROOT/tools/docs/.vale.ini" ]
check "Vale's StylesPath resolves to the Project style" bash -c \
  'p="$(sed -n "s/^StylesPath *= *//p" "$1/tools/docs/.vale.ini" | tr -d "\r")"; [ -s "$1/tools/docs/$p/Project/Filler.yml" ]' _ "$ROOT"
check "check.sh's docs module passes both docs configs explicitly" \
  bash -c 'grep -qF -- "--config \"\$config\"" "$1" && grep -qF -- "--config=\"\$config\"" "$1"' _ "$ROOT/scripts/check/docs.sh"
check "the git hooks live in scripts/githooks" [ -f "$ROOT/scripts/githooks/pre-commit" ]

TEMPLATES="$ROOT/.github/ISSUE_TEMPLATE"
for template in bug finding accessibility; do
  check "the $template issue template exists" [ -s "$TEMPLATES/$template.yml" ]
done
# Blank issues skip the templates' questions, and the security link keeps reports out of public issues.
check "blank issues are off" grep -qE "^blank_issues_enabled:[[:space:]]*false" "$TEMPLATES/config.yml"
check "the issue chooser links to private vulnerability reporting" \
  grep -qF "/security/advisories/new" "$TEMPLATES/config.yml"

echo "policy-file tests: $passed passed, $failed failed"
[ "$failed" -eq 0 ]
