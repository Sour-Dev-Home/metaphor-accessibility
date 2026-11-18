#!/usr/bin/env bash
# Black-box tests for scripts/check.sh: each test plants content in a temporary git repo, runs the check there and
# asserts the exit code and the message.
set -uo pipefail
unset PII_PATTERNS REQUIRE_PRIVATE_PATTERNS REQUIRE_DOCS_TOOLS GITHUB_ACTIONS SECURITY_REVIEW_SKIP # set per run

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
CHECK="$ROOT/scripts/check.sh"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

passed=0
failed=0

# new_repo: create an empty git repo and cd into it.
new_repo() {
  local dir="$WORK/repo-$RANDOM-$RANDOM"
  mkdir -p "$dir" && cd "$dir" || exit 2
  git init -q
  git config user.name test
  git config user.email test@example.invalid
  git config core.autocrlf false
  git config commit.gpgsign false
}

# run_check ARGS...: run the check, capturing its exit code and combined output.
run_check() {
  out="$(bash "$CHECK" "$@" 2>&1)"
  code=$?
}

# expect_absent TEXT...: the last run's output contains none of TEXT.
expect_absent() {
  local text ok=1
  for text in "$@"; do
    [[ "$out" == *"$text"* ]] && ok=0
  done
  if [ "$ok" -eq 1 ]; then
    passed=$((passed + 1))
  else
    failed=$((failed + 1))
    printf 'FAIL %s\n  want none of: %s\n  got output:\n%s\n' "$current" "$*" "$(sed 's/^/    /' <<<"$out")"
  fi
}

# expect CODE [TEXT...]: the last run exited with CODE and its output contains every TEXT.
expect() {
  local want="$1" ok=1 text
  shift
  [ "$code" -eq "$want" ] || ok=0
  for text in "$@"; do
    [[ "$out" == *"$text"* ]] || ok=0
  done
  if [ "$ok" -eq 1 ]; then
    passed=$((passed + 1))
  else
    failed=$((failed + 1))
    printf 'FAIL %s\n  want exit %s and text: %s\n  got exit %s, output:\n%s\n' \
      "$current" "$want" "$*" "$code" "$(sed 's/^/    /' <<<"$out")"
  fi
}

test_blocks_staged_cpk() {
  new_repo
  printf 'not really an archive' > data.cpk
  git add data.cpk
  run_check fast
  expect 1 "game content" "data.cpk"
}

test_blocks_every_game_extension_even_uppercase() {
  local ext
  for ext in pak exe DLL asi dmp mdmp gpr gzf gbf idb i64 til nam id0 id1 id2 bndb dd64; do
    new_repo
    printf 'x' > "file.$ext"
    git add -f "file.$ext"
    run_check fast
    expect 1 "game content" "file.$ext"
  done
}

test_names_build_output_for_our_own_dll() {
  new_repo
  printf 'x' > Mod.dll
  git add -f Mod.dll
  run_check fast
  expect 1 "Mod.dll" "game content or build output" "executable extension"
}

test_passes_ordinary_source() {
  new_repo
  printf 'class Mod {}\n' > Mod.cs
  git add Mod.cs
  run_check fast
  expect 0
}

test_fast_stage_prints_its_time_against_the_stage_1_budget() {
  new_repo
  run_check fast
  expect 0 "timing: stage 1 (fast) started" "(budget 10 s)"
}

test_failing_fast_stage_still_prints_its_time_and_fails() {
  new_repo
  printf 'x' > data.cpk
  git add data.cpk
  run_check fast
  expect 1 "game content" "timing: stage 1 (fast)"
}

test_untimed_stage_prints_no_time() {
  new_repo
  run_check rules
  expect 0
  expect_absent "timing:"
}

test_blocks_file_in_ignored_game_folder_added_with_force() {
  new_repo
  printf 'game/\n' > .gitignore
  mkdir -p game/sub
  printf 'harmless-looking text\n' > game/sub/notes.txt
  git add .gitignore
  git add -f game/sub/notes.txt
  run_check fast
  expect 1 "game content" "game/sub/notes.txt" "game folder"
}

test_blocks_file_inside_ghidra_project_folder() {
  new_repo
  mkdir -p work/Metaphor.rep/idata
  printf 'x' > work/Metaphor.rep/idata/00.prp
  git add -f work/Metaphor.rep/idata/00.prp
  run_check fast
  expect 1 "game content" "Metaphor.rep/idata/00.prp" "disassembler"
}

test_blocks_query_output_in_the_ghidra_folder() {
  new_repo
  mkdir -p ghidra/out
  printf 'void FUN_14021e1c0(void) {}\n' > ghidra/out/decompiled.txt
  git add -f ghidra/out/decompiled.txt
  run_check fast
  expect 1 "game content" "ghidra/out/decompiled.txt" "game folder"
}

test_blocks_large_binary_with_harmless_name() {
  new_repo
  head -c $((2 * 1024 * 1024)) /dev/urandom > texture.png
  git add texture.png
  run_check fast
  expect 1 "game content" "texture.png" "larger than"
}

test_blocks_small_file_whose_hash_is_listed() {
  new_repo
  mkdir -p scripts
  # SHA-256 of the three bytes "abc" (FIPS 180-2 test vector).
  printf '# known game files\nBA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD  some game file\n' \
    > scripts/game-content.sha256
  printf 'abc' > readme-snippet.txt
  git add scripts/game-content.sha256 readme-snippet.txt
  run_check fast
  expect 1 "game content" "readme-snippet.txt" "hash"
}

test_rules_stage_blocks_committed_content_that_skipped_the_hook() {
  new_repo
  mkdir -p extracted
  printf 'x' > extracted/model.bin
  git add -f extracted/model.bin
  git commit -q -m "sneak in" --no-verify
  run_check rules
  expect 1 "game content" "extracted/model.bin"
}

test_rules_stage_passes_clean_repo() {
  new_repo
  printf '# notes\n' > notes.md
  git add notes.md
  git commit -q -m "notes" --no-verify
  run_check rules
  expect 0
}

test_full_stage_fails_closed_without_a_solution() {
  new_repo
  printf '# notes\n' > notes.md
  git add notes.md
  git commit -q -m "notes" --no-verify
  run_check full
  expect 2 "Metaphor.sln"
}

test_signatures_stage_fails_closed_without_a_game_exe() {
  new_repo
  out="$(unset METAPHOR_GAME_EXE; bash "$CHECK" signatures 2>&1)"
  code=$?
  expect 2 "METAPHOR_GAME_EXE"
}

test_signatures_stage_fails_closed_when_the_game_exe_is_missing() {
  new_repo
  METAPHOR_GAME_EXE="$WORK/no-such-game.exe" run_check signatures
  expect 2 "no file at"
}

test_rejects_unknown_stage() {
  new_repo
  run_check fastest
  expect 2 "usage"
}

test_fails_closed_outside_a_git_repo() {
  mkdir -p "$WORK/not-a-repo" && cd "$WORK/not-a-repo" || exit 2
  run_check rules
  expect 2 "could not list files"
}

test_matches_hash_only_line_with_crlf_ending() {
  new_repo
  mkdir -p scripts
  printf 'ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad\r\n' > scripts/game-content.sha256
  printf 'abc' > snippet.txt
  git add scripts/game-content.sha256 snippet.txt
  run_check fast
  expect 1 "game content" "snippet.txt" "hash"
}

test_passes_nested_source_folder_named_game() {
  new_repo
  mkdir -p src/Mod/Game
  printf 'class GameState {}\n' > src/Mod/Game/GameState.cs
  git add src/Mod/Game/GameState.cs
  run_check fast
  expect 0
}

test_blocks_root_game_folder_in_any_case() {
  new_repo
  mkdir -p Game
  printf 'x' > Game/data.txt
  git add Game/data.txt
  run_check fast
  expect 1 "game content" "Game/data.txt" "game folder"
}

test_skips_submodule_entries() {
  new_repo
  git update-index --add --cacheinfo 160000,1111111111111111111111111111111111111111,vendor/lib
  run_check fast
  expect 0
  run_check rules
  expect 0
}

test_judges_staged_content_not_working_tree() {
  new_repo
  head -c $((2 * 1024 * 1024)) /dev/urandom > big.bin
  git add big.bin
  printf 'small now' > big.bin
  run_check fast
  expect 1 "big.bin" "larger than"
}

test_blocks_private_pattern_with_trailing_backslash() {
  new_repo
  mkdir -p .claude
  printf '# private\nD:\\Games\\Secret\\\n' > .claude/pii-patterns.txt
  printf 'see D:\\Games\\Secret\\save for details\n' > notes.md
  git add notes.md
  run_check fast
  expect 1 "personal information" "notes.md:1" "private pattern"
}

test_warns_when_private_patterns_are_missing() {
  new_repo
  printf 'hello\n' > notes.md
  git add notes.md
  run_check fast
  expect 0 "warning" "no private patterns" ".claude/pii-patterns.txt"
}

test_reads_private_patterns_from_environment() {
  new_repo
  printf 'contact jane-q-public for access\n' > notes.md
  git add notes.md
  PII_PATTERNS=$'# from a secret\r\njane-q-public\r\n' run_check fast
  expect 1 "personal information" "notes.md:1" "private pattern"
}

test_fails_when_required_private_patterns_are_missing() {
  new_repo
  printf 'hello\n' > notes.md
  git add notes.md
  REQUIRE_PRIVATE_PATTERNS=1 run_check fast
  expect 2 "no private patterns"
}

# Planted personal information is assembled at run time, so this file never holds it and passes its own scan.
users="Use""rs"
home="ho""me"
at="@"

test_blocks_windows_user_path() {
  new_repo
  printf 'ok\nbuilt in C:\\%s\\alice\\src\n' "$users" > build.md
  git add build.md
  run_check fast
  expect 1 "personal information" "build.md:2" "local user path"
}

test_blocks_unix_home_and_mac_user_paths() {
  new_repo
  printf 'cd /%s/alice/mods\n' "$home" > a.sh
  printf 'open /%s/alice/Desktop\n' "$users" > b.sh
  git add a.sh b.sh
  run_check fast
  expect 1 "a.sh:1" "b.sh:1" "local user path"
}

test_blocks_git_bash_and_wsl_user_paths() {
  new_repo
  printf 'cd /c/%s/alice/mods\n' "$users" > a.sh
  printf 'cd /mnt/c/%s/alice/mods\n' "$users" > b.sh
  git add a.sh b.sh
  run_check fast
  expect 1 "a.sh:1" "b.sh:1" "local user path"
}

test_passes_ssh_remotes_and_retina_image_names() {
  new_repo
  printf 'clone git%sgithub.com:owner/repo.git\nuse icon%s2x.png\n' "$at" "$at" > notes.md
  git add notes.md
  run_check fast
  expect 0
}

test_trims_whitespace_around_private_patterns() {
  new_repo
  mkdir -p .claude
  printf '  jane-q-public  \n' > .claude/pii-patterns.txt
  printf 'signed, jane-q-public.\n' > notes.md
  git add notes.md
  run_check fast
  expect 1 "notes.md:1" "private pattern"
}

test_blocks_email_address() {
  new_repo
  printf 'mail alice.smith%sgmail.com\n' "$at" > notes.md
  git add notes.md
  run_check fast
  expect 1 "personal information" "notes.md:1" "email address"
}

test_passes_placeholders_and_example_addresses() {
  new_repo
  printf 'no paths like C:\\%s\\... or C:/%s/ here\n' "$users" "$users" > a.md
  printf 'write to you%sexample.com, bot%sexample.invalid or 1+me%susers.noreply.github.com\n' "$at" "$at" "$at" > b.md
  printf 'see https://api.github.com/users/octocat\n' > c.md
  git add a.md b.md c.md
  run_check fast
  expect 0
}

test_blocks_private_pattern_in_file_name() {
  new_repo
  mkdir -p .claude
  printf 'jane-q-public\n' > .claude/pii-patterns.txt
  printf 'nothing personal\n' > Jane-Q-Public-notes.md
  git add Jane-Q-Public-notes.md
  run_check fast
  expect 1 "personal information" "Jane-Q-Public-notes.md: file name matches a private pattern"
}

test_ignores_blank_lines_in_private_patterns() {
  new_repo
  mkdir -p .claude
  printf 'jane-q-public\n\n   \n' > .claude/pii-patterns.txt
  printf 'nothing personal\n' > notes.md
  git add notes.md
  run_check fast
  expect 0
}

# raw_source PATH [skip-source|skip-saved|skip-build]: stage a raw source with the three header lines the existing
# sources use, leaving out one when asked.
raw_source() {
  local path="$1" skip="${2:-}"
  mkdir -p "$(dirname "$path")"
  {
    printf '# A saved page\n\n'
    [ "$skip" = skip-source ] || printf -- '- **Source:** <https://example.org/page>\n'
    [ "$skip" = skip-saved ] || printf -- '- **Saved:** 2026-11-13.\n'
    [ "$skip" = skip-build ] || printf -- '- **Game build:** none; this is about tooling.\n'
    printf '\n## Findings\n\n- Something.\n'
  } > "$path"
  git add "$path"
}

test_passes_raw_source_with_full_header() {
  new_repo
  raw_source docs/raw-sources/page-2026-11-13.md
  run_check fast
  expect 0
}

test_blocks_raw_source_without_source() {
  new_repo
  raw_source docs/raw-sources/page-2026-11-13.md skip-source
  run_check fast
  expect 1 "raw source: docs/raw-sources/page-2026-11-13.md: header has no Source line"
  expect_absent "no Saved line" "no Game build line"
}

test_blocks_raw_source_without_saved_date() {
  new_repo
  raw_source docs/raw-sources/page-2026-11-13.md skip-saved
  run_check fast
  expect 1 "raw source: docs/raw-sources/page-2026-11-13.md: header has no Saved line"
  expect_absent "no Source line" "no Game build line"
}

test_blocks_raw_source_with_saved_line_but_no_date() {
  new_repo
  mkdir -p docs/raw-sources
  printf '# Page\n\n- **Source:** a tool\n- **Saved:** last week\n- **Game build:** none\n' > docs/raw-sources/p.md
  git add docs/raw-sources/p.md
  run_check fast
  expect 1 "raw source: docs/raw-sources/p.md: header has no Saved line"
}

test_passes_raw_source_with_reworded_labels() {
  new_repo
  mkdir -p docs/raw-sources
  printf '# Page\n\n- **Source:** a tool\n- **Date saved:** 2026-11-13\n- **Game build(s) the sources mention:** PC\n' \
    > docs/raw-sources/p.md
  git add docs/raw-sources/p.md
  run_check fast
  expect 0
}

test_blocks_raw_source_without_game_build() {
  new_repo
  raw_source docs/raw-sources/page-2026-11-13.md skip-build
  run_check fast
  expect 1 "raw source: docs/raw-sources/page-2026-11-13.md: header has no Game build line"
  expect_absent "no Source line" "no Saved line"
}

test_ignores_header_lines_below_the_first_section() {
  new_repo
  mkdir -p docs/raw-sources
  printf '# Page\n\n- **Source:** a tool\n- **Saved:** 2026-11-13\n\n## Notes\n\n- **Game build:** none\n' \
    > docs/raw-sources/p.md
  git add docs/raw-sources/p.md
  run_check fast
  expect 1 "raw source: docs/raw-sources/p.md: header has no Game build line"
}

test_passes_raw_sources_readme_without_header() {
  new_repo
  mkdir -p docs/raw-sources
  printf '# Raw sources\n\nSaved copies.\n' > docs/raw-sources/README.md
  git add docs/raw-sources/README.md
  run_check fast
  expect 0
}

# finding PATH BUILD-LINE: stage a finding note whose header holds BUILD-LINE (empty for none).
finding() {
  mkdir -p "$(dirname "$1")"
  printf '# Something about a format\n\n%s\n\n## Evidence\n\n- A capture.\n' "$2" > "$1"
  git add "$1"
}

build_sha="$(printf 'a%.0s' {1..64})"

test_passes_finding_naming_a_game_build_by_sha() {
  new_repo
  finding docs/findings/format.md "- **Game build:** $build_sha (Steam build 123)"
  run_check fast
  expect 0
}

test_passes_header_values_in_code_quotes() {
  new_repo
  note docs/raw-sources/p.md '- **Source:** `gh api`' '- **Saved:** 2026-11-13' "- **Game build:** \`$build_sha\` (SHA-256)"
  finding docs/findings/format.md "- **Game build:** \`$build_sha\`"
  run_check fast
  expect 0
}

test_blocks_finding_without_game_build() {
  new_repo
  finding docs/findings/format.md ""
  run_check fast
  expect 1 "finding: docs/findings/format.md: header has no Game build line"
}

test_blocks_finding_whose_game_build_is_not_a_sha() {
  new_repo
  finding docs/findings/format.md "- **Game build:** latest"
  run_check fast
  expect 1 "finding: docs/findings/format.md: header has no Game build line"
}

# note PATH LINES...: stage a note whose header is the given lines, followed by one section.
note() {
  local path="$1"
  shift
  mkdir -p "$(dirname "$path")"
  { printf '# A note\n\n'; printf '%s\n' "$@"; printf '\n## Section\n\n- Text.\n'; } > "$path"
  git add "$path"
}

test_blocks_raw_source_whose_values_are_blank_or_junk() {
  new_repo
  note docs/raw-sources/p.md $'- **Source:**\t' '- **Saved:** 2026-11-13x' '- **Game build:** -'
  run_check fast
  expect 1 "header has no Source line" "header has no Saved line with a YYYY-MM-DD date" "header has no Game build line"
}

test_passes_raw_source_with_colon_outside_bold_and_plural_label() {
  new_repo
  note docs/raw-sources/p.md '- **Sources**: a tool' '- **Saved**: 2026-11-13' '- **Game build**: none'
  run_check fast
  expect 0
}

test_blocks_non_markdown_files_in_note_folders() {
  new_repo
  mkdir -p docs/raw-sources docs/findings
  printf 'capture\n' > docs/raw-sources/capture.txt
  printf 'note\n' > docs/findings/NOTE.MD
  git add docs
  run_check fast
  expect 1 "raw source: docs/raw-sources/capture.txt: not a .md file" "finding: docs/findings/NOTE.MD: not a .md file"
}

test_passes_findings_readme_without_game_build() {
  new_repo
  note docs/findings/README.md '- **Owner:** nobody'
  run_check fast
  expect 0
}

# check_message TEXT: run the commit-msg stage on a message file holding TEXT (printf escapes allowed).
check_message() {
  local file="$WORK/message-$RANDOM"
  printf "$1" > "$file"
  run_check commit-msg "$file"
}

test_passes_typed_subject_with_body_and_trailers() {
  new_repo
  check_message 'feat(check): block bad commit messages\n\nWhy it matters, in two lines\nat most five.\n\nCo-Authored-By: Someone <someone@example.com>\nSigned-off-by: Someone <someone@example.com>\n'
  expect 0
}

test_blocks_subject_without_type_prefix() {
  new_repo
  check_message 'Add a thing\n'
  expect 1 "commit message: subject needs a type prefix"
}

test_blocks_subject_over_72_characters() {
  new_repo
  check_message "feat: $(printf 'x%.0s' {1..67})\n" # 73 characters
  expect 1 "commit message: subject is 73 characters; the limit is 72"
}

test_counts_subject_length_in_characters_not_bytes() {
  new_repo
  check_message "docs: $(printf 'é%.0s' {1..66})\n" # 72 characters, 138 bytes
  expect 0
}

test_blocks_body_over_five_lines() {
  new_repo
  check_message 'fix: a bug\n\none\ntwo\n\nthree\nfour\nfive\nsix\n\nCo-Authored-By: Someone <someone@example.com>\n'
  expect 1 "commit message: body has 6 lines; the limit is 5"
}

test_blocks_body_without_blank_line_after_subject() {
  new_repo
  check_message 'fix: a bug\nno blank line\n'
  expect 1 "commit message: leave a blank line after the subject"
}

test_ignores_comment_lines_and_text_below_the_scissors() {
  new_repo
  check_message 'fix: a bug\n\nWhy.\n# Please enter the commit message\n# a\n# b\n# c\n# d\n# ------------------------ >8 ------------------------\ndiff --git a/x b/x\n1\n2\n3\n4\n5\n6\n'
  expect 0
}

test_passes_git_generated_subjects() {
  local subject
  for subject in 'Revert "feat: a thing"' 'fixup! feat: a thing' 'squash! fix: b' 'amend! docs: c'; do
    new_repo
    check_message "$subject\n"
    expect 0
  done
}

test_passes_subject_followed_only_by_trailers() {
  new_repo
  check_message 'feat: pair work\n\nCo-Authored-By: a <a@example.com>\nCo-Authored-By: b <b@example.com>\nCo-Authored-By: c <c@example.com>\nCo-Authored-By: d <d@example.com>\nCo-Authored-By: e <e@example.com>\nSigned-off-by: f <f@example.com>\n'
  expect 0
}

test_blocks_empty_message() {
  new_repo
  check_message '# only a comment\n\n'
  expect 1 "commit message: message is empty"
  expect_absent "type prefix"
}

test_ignores_trailing_spaces_and_crlf_line_endings() {
  new_repo
  check_message "fix: $(printf 'x%.0s' {1..67})   \r\n\r\nWhy.\r\n" # 72 characters before the spaces
  expect 0
}

test_commits_stage_names_each_bad_commit_in_the_range() {
  local base bad
  new_repo
  git commit -q --allow-empty -m 'not typed, but before the range'
  base="$(git rev-parse HEAD)"
  git commit -q --allow-empty -m 'feat: a good one'
  git commit -q --allow-empty -m 'a bad one'
  bad="$(git rev-parse --short HEAD)"
  run_check commits "$base..HEAD"
  expect 1 "commit $bad: subject needs a type prefix"
  expect_absent "before the range" "good one"
}

test_commits_stage_passes_a_clean_range_and_skips_merges() {
  local base
  new_repo
  git commit -q --allow-empty -m 'chore: start'
  base="$(git rev-parse HEAD)"
  git switch -q -c side
  git commit -q --allow-empty -m 'feat: on a branch'
  git switch -q -
  git merge -q --no-ff --no-edit side # "Merge branch 'side'" has no type prefix
  run_check commits "$base..HEAD"
  expect 0
}

test_commits_stage_fails_closed_on_a_bad_range() {
  new_repo
  git commit -q --allow-empty -m 'chore: start'
  run_check commits "nope..HEAD"
  expect 2 "could not list the commits in nope..HEAD"
}

# docs_repo: a new repo holding the real docs-check configs, with the installed docs tools on PATH.
docs_repo() {
  new_repo
  mkdir -p tools/docs
  cp "$ROOT/tools/docs/.vale.ini" "$ROOT/tools/docs/.markdownlint-cli2.jsonc" tools/docs/
  cp -r "$ROOT/tools/docs/.vale" tools/docs/
  PATH="$ROOT/.tools/bin:$ROOT/tools/docs/node_modules/.bin:$PATH"
}

test_docs_stage_fails_on_a_broken_relative_link() {
  docs_repo
  printf '# Notes\n\nSee [the guide](missing.md).\n' > notes.md
  git add .
  run_check docs
  expect 1 "broken link" "notes.md"
}

test_docs_stage_fails_on_a_missing_anchor() {
  docs_repo
  printf '# Other\n\n## Real part\n' > other.md
  printf '# Notes\n\nSee [it](other.md#no-such-part).\n' > notes.md
  git add .
  run_check docs
  expect 1 "broken link" "notes.md:3" "other.md#no-such-part"
}

test_docs_stage_warns_on_a_filler_word() {
  docs_repo
  printf '# Notes\n\nThis is basically how the loader works.\n' > notes.md
  git add .
  run_check docs
  expect 0 "warning: notes.md:3" "basically"
}

test_docs_stage_warns_on_a_markdown_lint_finding() {
  docs_repo
  printf '# Notes\n\n\n\nToo many blank lines above.\n' > notes.md
  git add .
  run_check docs
  expect 0 "warning: notes.md:3" "MD012"
}

# markdownlint-cli2 loads a config from each linted file's folder, even with --config, and a .cjs or .mjs one runs code
# (issue #322), so the stage fails before running it.
test_docs_stage_fails_on_a_markdownlint_config_outside_tools_docs() {
  docs_repo
  mkdir -p docs
  printf '# Notes\n\nPlain words.\n' > docs/notes.md
  printf 'require("fs").writeFileSync("ran", "");\nmodule.exports = {};\n' > docs/.markdownlint-cli2.cjs
  git add .
  run_check docs
  expect 1 "docs/.markdownlint-cli2.cjs" "tools/docs/"
  out="the config ran" code=0
  [ ! -e ran ] || code=1
  expect 0
}

# words N: print N words of plain prose.
words() { local i; for ((i = 0; i < $1; i++)); do printf 'word '; done; echo; }

test_docs_stage_warns_on_an_adr_over_250_words() {
  docs_repo
  mkdir -p docs/adr
  { echo '# Long decision'; echo; words 260; } > docs/adr/0009-long.md
  git add .
  run_check docs
  expect 0 "warning: docs/adr/0009-long.md: 260 words" "250"
}

test_docs_stage_ignores_title_and_code_in_adr_word_count() {
  docs_repo
  mkdir -p docs/adr
  { echo '# A title of several words here'; echo; words 240; echo; echo '```text'; words 50; echo '```'; } \
    > docs/adr/0009-short.md
  git add .
  run_check docs
  expect 0
  expect_absent "0009-short.md: "
}

test_pr_body_stage_warns_over_300_words() {
  docs_repo
  { echo '## Summary'; words 310; } > "$WORK/body.md"
  run_check pr-body "$WORK/body.md"
  expect 0 "warning: PR description: 310 words" "300"
}

test_pr_body_stage_leaves_out_the_evidence_section() {
  docs_repo
  { echo '## Summary'; words 250; echo '## Evidence'; words 200; echo '## Merge Danger'; words 20; } > "$WORK/body.md"
  run_check pr-body "$WORK/body.md"
  expect 0
  expect_absent "warning"
}

test_pr_body_stage_warns_on_a_filler_word() {
  docs_repo
  printf '## Summary\n\nThis basically adds a check.\n' > "$WORK/body.md"
  run_check pr-body "$WORK/body.md"
  expect 0 "warning: pr-description.md:3" "basically"
}

test_pr_body_stage_fails_closed_without_a_file() {
  new_repo
  run_check pr-body "$WORK/no-such-body.md"
  expect 2 "pr-body stage needs"
}

# run_check_without_docs_tools ARGS...: run_check with a PATH that has git and the shell's basics but no docs tools.
run_check_without_docs_tools() {
  out="$(PATH="$(dirname "$(command -v git)"):/usr/bin:/bin" bash "$CHECK" "$@" 2>&1)"
  code=$?
}

test_docs_stage_skips_missing_tools_with_a_warning() {
  new_repo
  printf '# Notes\n' > notes.md
  git add .
  run_check_without_docs_tools docs
  expect 0 "warning: lychee is not installed" "warning: vale is not installed"
}

test_docs_stage_fails_closed_on_a_missing_tool_in_ci() {
  new_repo
  printf '# Notes\n' > notes.md
  git add .
  REQUIRE_DOCS_TOOLS=1 run_check_without_docs_tools docs
  expect 2 "lychee is not installed, but REQUIRE_DOCS_TOOLS=1"
}

test_docs_stage_passes_good_links_and_skips_raw_sources() {
  docs_repo
  mkdir -p docs/raw-sources
  printf '# Other\n\n## Real part\n' > other.md
  printf '# Notes\n\nSee [it](other.md#real-part) and [here](#notes).\n' > notes.md
  printf '# Saved page\n\n[Their link](/wiki/elsewhere.md)\n' > docs/raw-sources/page.md
  git add .
  run_check docs
  expect 0
  expect_absent "broken link"
}

# pre-commit: the repo rules, then each extra check only when its file types are staged (#78).

test_pre_commit_still_runs_the_repo_rules() {
  new_repo
  printf 'not really an archive' > data.cpk
  git add data.cpk
  run_check pre-commit
  expect 1 "game content" "data.cpk"
}

test_pre_commit_runs_the_docs_checks_when_markdown_is_staged() {
  docs_repo
  printf '# Notes\n\nSee [it](missing.md).\n' > notes.md
  git add notes.md
  run_check pre-commit
  expect 1 "broken link" "notes.md"
}

test_pre_commit_blames_only_the_check_that_failed() {
  docs_repo
  printf 'not really an archive' > data.cpk
  printf '# Notes\n\nFine.\n' > notes.md
  git add .
  run_check pre-commit
  expect 1 "game content"
  expect_absent "docs: see the problems above"
}

test_pre_commit_skips_the_docs_checks_when_no_markdown_is_staged() {
  docs_repo
  printf '# Notes\n\nSee [it](missing.md).\n' > notes.md
  git add notes.md && git commit -q -m "docs: notes"
  printf 'plain\n' > other.txt
  git add other.txt
  run_check pre-commit
  expect 0
  expect_absent "broken link"
}

test_pre_commit_says_how_to_check_changed_signatures_without_the_game() {
  new_repo
  mkdir -p src/Metaphor.Core
  printf '[]\n' > src/Metaphor.Core/hook-signatures.json
  git add .
  out="$(env -u METAPHOR_GAME_EXE bash "$CHECK" pre-commit 2>&1)"
  code=$?
  expect 0 "hook signatures changed" "METAPHOR_GAME_EXE"
}

# pre-push: build and tests only when the pushed commits change code. Git passes the refs on stdin.

ZERO=0000000000000000000000000000000000000000

# push_repo FILE: a repo with a first commit, then a second adding FILE. Sets BASE and HEAD to the two commits.
push_repo() {
  new_repo
  printf 'first\n' > readme.txt
  git add . && git commit -q -m "docs: first"
  BASE="$(git rev-parse HEAD)"
  mkdir -p "$(dirname "$1")"
  printf 'second\n' > "$1"
  git add . && git commit -q -m "feat: second"
  HEAD="$(git rev-parse HEAD)"
}

# run_pre_push LOCAL REMOTE: run the pre-push stage for one ref, as git would.
run_pre_push() {
  # A private lock path, so the machine's real build slots never decide these runs.
  out="$(printf 'refs/heads/topic %s refs/heads/topic %s\n' "$1" "$2" |
    METAPHOR_CHECK_LOCK="${METAPHOR_CHECK_LOCK:-$PWD/test-build.lock}" bash "$CHECK" pre-push 2>&1)"
  code=$?
}

test_pre_push_skips_the_build_when_no_code_changed() {
  push_repo docs/notes.md
  run_pre_push "$HEAD" "$BASE"
  expect 0 "no code changes"
}

test_pre_push_builds_when_code_changed() {
  push_repo src/Menu.cs
  run_pre_push "$HEAD" "$BASE"
  expect 2 "pre-push stage needs Metaphor.sln"
}

test_pre_push_builds_when_it_cant_tell_what_changed() {
  # A new branch with no origin/main to compare against.
  push_repo docs/notes.md
  run_pre_push "$HEAD" "$ZERO"
  expect 2 "pre-push stage needs Metaphor.sln"
}

test_pre_push_skips_a_deleted_branch() {
  push_repo src/Menu.cs
  run_pre_push "$ZERO" "$HEAD"
  expect 0
  expect_absent "Metaphor.sln"
}

# hold_lock: every build slot (one) is held by a live process, so a build stage would have to wait.
hold_lock() {
  export METAPHOR_CHECK_LOCK="$PWD/test-build.lock" METAPHOR_CHECK_SLOTS=1 METAPHOR_CHECK_POLL=1
  mkdir "$METAPHOR_CHECK_LOCK"
  echo "$$" > "$METAPHOR_CHECK_LOCK/pid"
}

test_pre_push_stops_at_once_when_the_lock_is_held_and_no_pass_is_recorded() {
  push_repo src/Menu.cs
  hold_lock
  run_pre_push "$HEAD" "$BASE"
  unset METAPHOR_CHECK_LOCK METAPHOR_CHECK_SLOTS METAPHOR_CHECK_POLL
  expect 2 "every build slot is busy" "bash scripts/check.sh full"
}

test_pre_push_reuses_a_recorded_pass_without_taking_the_lock() {
  push_repo src/Menu.cs
  hold_lock
  echo "full $(git rev-parse "$HEAD^{tree}")" > "$(git rev-parse --absolute-git-dir)/check-pass-cache"
  run_pre_push "$HEAD" "$BASE"
  unset METAPHOR_CHECK_LOCK METAPHOR_CHECK_SLOTS METAPHOR_CHECK_POLL
  expect 0 "reused the recorded pass"
}

# The must-run security review: real commits through hooks that run the check, as scripts/githooks does.

STAMP="$ROOT/scripts/security-stamp.sh"
HOOKS="$WORK/hooks"
mkdir -p "$HOOKS"
printf '#!/usr/bin/env bash\nexec bash "%s" pre-commit\n' "$CHECK" > "$HOOKS/pre-commit"
printf '#!/usr/bin/env bash\nexec bash "%s" commit-msg "$1"\n' "$CHECK" > "$HOOKS/commit-msg"
chmod +x "$HOOKS/pre-commit" "$HOOKS/commit-msg"

# review_repo: a repo using the hooks, with a first commit, then a staged change to scope file FILE (default: the hook
# layer).
review_repo() {
  new_repo
  git config core.hooksPath "$HOOKS"
  printf 'first\n' > readme.txt
  git add readme.txt && git commit -q --no-verify -m "docs: first"
  stage_file "${1:-src/Metaphor.Hooks/Hooks/Speech.ps1}"
}

# stage_file FILE [TEXT]: write TEXT (default: a random line) to FILE and stage it.
stage_file() {
  mkdir -p "$(dirname "$1")"
  printf '%s\n' "${2:-line $RANDOM}" > "$1"
  git add "$1"
}

# commit SUBJECT: commit through the hooks, capturing the exit code and output.
commit() {
  out="$(git commit -q -m "$1" 2>&1)"
  code=$?
}

# expect_message TEXT...: the last commit's message contains every TEXT.
expect_message() {
  out="$(git log -1 --format=%B)"
  code=0
  expect 0 "$@"
}

test_review_refuses_a_scope_commit_without_a_stamp() {
  review_repo
  commit "feat: unreviewed hook"
  expect 1 "security review: the staged hook or deploy code has no matching stamp" "security-reviewer"
}

test_review_passes_a_stamped_scope_commit_and_adds_its_trailer() {
  local hash
  review_repo scripts/deploy-dev.ps1
  bash "$STAMP" > /dev/null
  hash="$(bash "$STAMP" hash)"
  commit "feat: reviewed deploy script"
  expect 0
  expect_message "Security-Review: $hash"
  [ "$(bash "$STAMP" hash HEAD)" = "$hash" ] || out="the committed scope hashes differently: $hash" code=1
  expect 0
}

test_review_refuses_an_edit_after_stamping() {
  review_repo
  bash "$STAMP" > /dev/null
  stage_file src/Metaphor.Hooks/Hooks/Speech.ps1 "changed after the review"
  commit "feat: hook changed after review"
  expect 1 "no matching stamp"
}

test_review_override_passes_and_records_its_reason() {
  review_repo
  out="$(SECURITY_REVIEW_SKIP="reviewer down, checked by hand" git commit -q -m "fix: urgent hook fix" 2>&1)"
  code=$?
  expect 0 "security review: skipped (reviewer down, checked by hand)"
  expect_message "Security-Review-Skipped: reviewer down, checked by hand"
  expect_absent "Security-Review: "
}

test_review_refuses_a_blank_override() {
  review_repo
  out="$(SECURITY_REVIEW_SKIP="  " git commit -q -m "fix: blank reason" 2>&1)"
  code=$?
  expect 1 "no matching stamp"
}

test_review_leaves_commits_outside_the_scope_alone() {
  review_repo src/Metaphor.Hooks.Tests/SpeechTests.ps1 # a sibling folder, not the hook layer
  stage_file scripts/check-other.sh
  commit "test: outside the scope"
  expect 0
  expect_message "test: outside the scope"
  expect_absent "Security-Review"
}

test_review_replaces_a_hand_written_trailer() {
  review_repo
  bash "$STAMP" > /dev/null
  commit $'feat: reviewed hook\n\nSecurity-Review-Skipped: written by hand'
  expect 0
  expect_message "Security-Review: $(bash "$STAMP" hash HEAD)"
  expect_absent "written by hand"
  # The hook layer's folder counts: the hash differs from the first commit's, which has no scope files.
  [ "$(bash "$STAMP" hash HEAD)" != "$(bash "$STAMP" hash HEAD~1)" ] || out="the hook layer wasn't hashed" code=1
  expect 0
}

test_review_message_keeps_its_shape_with_the_trailer() {
  review_repo
  bash "$STAMP" > /dev/null
  commit $'feat: reviewed hook\n\nWhy.\n\nCo-Authored-By: Someone <someone@example.com>'
  expect 0
  run_check commits HEAD~1..HEAD
  expect 0
}

test_commits_stage_fails_a_scope_commit_without_a_review_trailer() {
  local base
  review_repo
  base="$(git rev-parse HEAD)"
  git commit -q --no-verify -m "feat: skipped the hooks"
  run_check commits "$base..HEAD"
  expect 1 "commit $(git rev-parse --short HEAD): touches the security-review scope without a Security-Review trailer"
}

test_commits_stage_fails_a_review_trailer_for_other_content() {
  local base
  review_repo
  base="$(git rev-parse HEAD)"
  git commit -q --no-verify -m $'feat: stale trailer\n\nSecurity-Review: 0000000000000000000000000000000000000000000000000000000000000000'
  run_check commits "$base..HEAD"
  expect 1 "touches the security-review scope without a Security-Review trailer"
}

test_commits_stage_passes_reviewed_skipped_and_unscoped_commits() {
  local base
  review_repo
  base="$(git rev-parse HEAD)"
  bash "$STAMP" > /dev/null
  commit "feat: reviewed"
  stage_file scripts/smoke-test.ps1
  SECURITY_REVIEW_SKIP="by hand" git commit -q -m "fix: skipped" > /dev/null 2>&1
  stage_file docs/notes.txt
  commit "docs: unscoped"
  run_check commits "$base..HEAD"
  expect 0
  [ "$(git rev-list --count "$base..HEAD")" -eq 3 ] || out="expected 3 commits" code=1
  expect 0
}

# outside_repo [FILE]: a repo with one reviewed commit changing FILE (default: a scope file), ready for the commits
# stage; sets $base. A fake gh answers the timeline call with the lines in $TIMELINE (what the real call's --jq filter
# prints) and a permission call with admin for the logins in $FAKE_ADMINS (default: maint), write for any other; it
# fails the call for the login in $FAKE_PERMISSION_FAIL. $FAKE_TIMELINE_FAIL fails the timeline call, $FAKE_GH_FAIL every
# call.
FAKE_BIN="$WORK/fakebin"
mkdir -p "$FAKE_BIN"
cat > "$FAKE_BIN/gh" <<'EOF'
#!/usr/bin/env bash
if [ -n "${FAKE_GH_FAIL:-}" ]; then exit 1; fi
for arg; do
  case "$arg" in
    repos/*/collaborators/*/permission)
      login="${arg#*/collaborators/}" login="${login%/permission}"
      [ "$login" = "${FAKE_PERMISSION_FAIL:-}" ] && exit 1
      case " ${FAKE_ADMINS:-maint} " in *" $login "*) echo admin ;; *) echo write ;; esac
      exit ;;
  esac
done
[ -z "${FAKE_TIMELINE_FAIL:-}" ] || exit 1
printf "%b" "${TIMELINE:-}"
EOF
chmod +x "$FAKE_BIN/gh"

outside_repo() {
  review_repo "${1:-src/Metaphor.Hooks/Hooks/Speech.ps1}"
  base="$(git rev-parse HEAD)" # review_repo staged the change; review and commit it here
  bash "$STAMP" > /dev/null
  git commit -q -m "feat: reviewed change" > /dev/null 2>&1 || out="could not commit the reviewed change" code=1
}

# run_pr ASSOCIATION: the commits stage the way CI runs it, for a PR #7 by $PR_AUTHOR (default: contrib) updated at
# 2026-11-16T10:00:00Z.
run_pr() {
  out="$(PATH="$FAKE_BIN:$PATH" TIMELINE="${TIMELINE:-}" FAKE_GH_FAIL="${FAKE_GH_FAIL:-}" \
    FAKE_PERMISSION_FAIL="${FAKE_PERMISSION_FAIL:-}" FAKE_TIMELINE_FAIL="${FAKE_TIMELINE_FAIL:-}" \
    PR_AUTHOR_ASSOCIATION="$1" PR_AUTHOR="${PR_AUTHOR:-contrib}" \
    PR_NUMBER=7 GITHUB_REPOSITORY=o/r PR_UPDATED_AT=2026-11-16T10:00:00Z bash "$CHECK" commits "$base..HEAD" 2>&1)"
  code=$?
}

# Issue #309: every PR by someone other than the owner (or an admin) needs the label, collaborators and members too.
test_non_owner_pr_fails_without_the_label() {
  local assoc
  outside_repo
  TIMELINE=""
  for assoc in COLLABORATOR MEMBER CONTRIBUTOR NONE FIRST_TIME_CONTRIBUTOR; do
    run_pr "$assoc"
    expect 1 "add the security-reviewed label"
  done
}

test_non_owner_pr_outside_the_scope_still_needs_the_label() {
  outside_repo src/notes.txt
  TIMELINE=""
  run_pr COLLABORATOR
  expect 1 "add the security-reviewed label"
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tmaint\n'
  run_pr COLLABORATOR
  expect 0
}

test_non_owner_docs_only_pr_needs_no_label_or_timeline() {
  outside_repo docs/notes.txt
  TIMELINE=""
  FAKE_GH_FAIL=1 run_pr COLLABORATOR
  expect 0
  FAKE_GH_FAIL=1 run_pr CONTRIBUTOR
  expect 0
}

test_non_owner_pr_passes_with_a_maintainers_label_added_after_the_latest_push() {
  local assoc
  outside_repo
  TIMELINE='push\t2026-11-16T09:00:00Z\nlabeled\t2026-11-16T11:00:00Z\tmaint\n'
  for assoc in COLLABORATOR MEMBER CONTRIBUTOR; do
    run_pr "$assoc"
    expect 0
  done
}

test_non_owner_pr_fails_when_a_non_admin_added_the_label() {
  outside_repo
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tcollab\n'
  run_pr COLLABORATOR
  expect 1 "added by collab, who isn't an admin"
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tmaint\nunlabeled\t2026-11-16T11:10:00Z\tcollab\nlabeled\t2026-11-16T11:20:00Z\tcollab\n'
  run_pr COLLABORATOR
  expect 1 "added by collab, who isn't an admin"
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tbad/../login\n' # never reaches the API path
  run_pr COLLABORATOR
  expect 1 "added by bad/../login, who isn't an admin"
}

test_non_owner_pr_fails_closed_when_the_labellers_permission_cannot_be_read() {
  outside_repo
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tmaint\n'
  FAKE_PERMISSION_FAIL=maint run_pr COLLABORATOR
  expect 2 "could not read the permission of maint"
}

test_non_owner_scope_change_needs_the_trailer_and_the_label() {
  review_repo
  base="$(git rev-parse HEAD)"
  git commit -q --no-verify -m "feat: scope change, no review"
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tmaint\n'
  run_pr COLLABORATOR
  expect 1 "touches the security-review scope without a Security-Review trailer"
  run_pr CONTRIBUTOR
  expect 1 "touches the security-review scope without a Security-Review trailer"
}

# SECURITY_REVIEW_SKIP is the maintainer's, so a skip trailer only counts on the owner's PR.
test_non_owner_skip_trailer_does_not_count() {
  review_repo
  base="$(git rev-parse HEAD)"
  git commit -q --no-verify -m "feat: scope change" -m "Security-Review-Skipped: by hand"
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tmaint\n'
  run_pr COLLABORATOR
  expect 1 "touches the security-review scope without a Security-Review trailer"
  PR_AUTHOR=maint run_pr MEMBER
  expect 0
}

test_outside_pr_fails_when_the_label_predates_a_push() {
  outside_repo
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tmaint\npush\t2026-11-16T12:00:00Z\n'
  run_pr CONTRIBUTOR
  expect 1 "before the PR's latest push (2026-11-16T12:00:00Z)"
  TIMELINE='labeled\t2026-11-16T08:00:00Z\tmaint\n' # older than the event's own updated_at
  run_pr COLLABORATOR
  expect 1 "before the PR's latest push (2026-11-16T10:00:00Z)"
}

test_outside_pr_fails_when_the_label_was_removed_after_adding() {
  outside_repo
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tmaint\nunlabeled\t2026-11-16T11:30:00Z\tmaint\n'
  run_pr CONTRIBUTOR
  expect 1 "add the security-reviewed label"
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tmaint\nunlabeled\t2026-11-16T11:30:00Z\tmaint\nlabeled\t2026-11-16T12:00:00Z\tmaint\n'
  run_pr CONTRIBUTOR
  expect 0
}

test_outside_pr_fails_closed_when_the_timeline_cannot_be_read() {
  outside_repo
  FAKE_TIMELINE_FAIL=1 run_pr CONTRIBUTOR
  expect 2 "could not read the timeline of PR #7"
}

test_outside_pr_merge_commit_cannot_hide_a_scope_change() {
  review_repo docs/notes.txt
  git commit -q --no-verify -m "docs: notes"
  git checkout -q -b upstream HEAD~1
  stage_file readme.txt
  git commit -q --no-verify -m "docs: upstream"
  base="$(git rev-parse HEAD)"
  git checkout -q -
  git merge -q --no-ff --no-commit upstream > /dev/null
  stage_file src/Metaphor.Hooks/Hooks/Speech.ps1 # changed inside the merge commit only
  git commit -q --no-verify -m "Merge upstream"
  TIMELINE=""
  run_pr CONTRIBUTOR
  expect 1 "add the security-reviewed label"
}

test_outside_pr_gate_runs_from_a_copy_outside_the_repository() {
  local gate="$WORK/base-scripts" script
  outside_repo
  mkdir -p "$gate"
  for script in check.sh security-stamp.sh stage-timing.sh; do # what CI copies from the base branch
    cp "$(dirname "$CHECK")/$script" "$gate/$script"
  done
  TIMELINE=""
  CHECK="$gate/check.sh" run_pr CONTRIBUTOR
  expect 1 "add the security-reviewed label"
}

test_outside_pr_still_checks_commit_messages() {
  outside_repo
  git commit -q --no-verify --allow-empty -m "not typed"
  TIMELINE='labeled\t2026-11-16T11:00:00Z\tmaint\n'
  run_pr CONTRIBUTOR
  expect 1 "subject needs a type prefix"
}

# The owner, or an admin (the maintainer's own PRs show MEMBER in an organization's repository), is judged by trailers.
test_maintainer_pr_uses_trailers_and_ignores_the_label() {
  outside_repo
  TIMELINE=""
  FAKE_GH_FAIL=1 run_pr OWNER # neither the timeline nor a permission is read
  expect 0
  PR_AUTHOR=maint run_pr MEMBER
  expect 0
  PR_AUTHOR=maint run_pr COLLABORATOR
  expect 0
  FAKE_PERMISSION_FAIL=maint PR_AUTHOR=maint run_pr MEMBER
  expect 2 "could not read the permission of maint"
  git commit -q --no-verify --allow-empty -m "fix: no trailer"
  stage_file scripts/smoke-test.ps1
  git commit -q --no-verify -m "fix: scope without trailer"
  run_pr OWNER
  expect 1 "touches the security-review scope without a Security-Review trailer"
}

# The guards that enforce the ground rules are in the scope too (issue #306).
GUARD_FILES=(scripts/check.sh scripts/game-content.sha256 scripts/tree-hash.sh scripts/ci-changes.sh
  scripts/test-select.sh scripts/stage-timing.sh scripts/install-docs-tools.sh scripts/conflict-alert.sh
  scripts/integration-sync.sh scripts/check-status.sh scripts/githooks/pre-commit scripts/githooks/pre-push
  scripts/githooks/commit-msg scripts/security-stamp.sh scripts/github-outage.sh .github/workflows/ci.yml
  .github/workflows/slow.yml .github/workflows/a-new-one.yml .claude/agents/security-reviewer.md
  tests/check/test-check.sh tests/scripts/test-anything.sh tools/slow/survivors.mjs tools/docs/package-lock.json
  .config/dotnet-tools.json scripts/listen.ps1)

# Every script the gate runs, transitively from check.sh, the git hooks and the workflows, is in the scope, so a change
# to one can't take over the CI steps after it (BASH_ENV or PATH through $GITHUB_ENV) without a review.
test_scope_covers_every_script_the_gate_runs() {
  local scope_paths queue=() seen=" " file ref entry covered missing=""
  read -ra scope_paths <<<"$(sed -n '/^scope=(/,/)$/p' "$STAMP" | tr '\n' ' ' | sed 's/^scope=(//; s/).*$//')"
  read -ra queue <<<"$(cd "$ROOT" && echo scripts/check.sh scripts/githooks/* .github/workflows/*)"
  while [ "${#queue[@]}" -gt 0 ]; do
    file="${queue[0]}" queue=("${queue[@]:1}")
    case "$seen" in *" $file "*) continue ;; esac
    seen+="$file "
    covered=""
    for entry in "${scope_paths[@]}"; do
      if [ "$file" = "$entry" ] || { [[ "$entry" == */ ]] && [[ "$file" == "$entry"* ]]; }; then covered=1; fi
    done
    [ -n "$covered" ] || missing+=" $file"
    # Script names on lines that aren't comments; a bare name means one in scripts/.
    for ref in $(grep -vE '^[[:space:]]*#' "$ROOT/$file" |
      grep -oE '[A-Za-z0-9_./-]*[A-Za-z0-9_-]+\.(sh|mjs|cjs|js|ps1|psm1|py)\b' | sort -u); do
      ref="${ref#./}" ref="${ref#/}" # "$root/scripts/x.sh" leaves "/scripts/x.sh"
      [[ "$ref" == */* ]] || ref="scripts/$ref"
      [ -f "$ROOT/$ref" ] && queue+=("$ref")
    done
  done
  out="outside the scope:$missing" code=0
  [ -z "$missing" ] || code=1
  expect 0
}

test_stamp_touches_each_guard_file_but_not_docs() {
  local file
  for file in "${GUARD_FILES[@]}"; do
    review_repo "$file"
    out="$file" code=0
    bash "$STAMP" touches || code=$?
    expect 0
  done
  review_repo docs/notes.md
  out="docs only" code=0
  bash "$STAMP" touches || code=$?
  expect 1
}

# What dotnet restore reads before the security job's vulnerability audit (issue #322): MSBuild's auto-imported files in
# any folder, the SDK pin and the package sources. A change to one could hide a vulnerable package.
AUDIT_FILES=(Directory.Build.props src/App/Directory.Build.targets src/App/Directory.Build.rsp
  Directory.Packages.props src/App/Directory.Packages.props Directory.Solution.targets after.Metaphor.sln.targets
  global.json nuget.config NuGet.config src/App/NuGet.Config)

test_stamp_touches_each_file_the_audit_reads() {
  local file
  for file in "${AUDIT_FILES[@]}"; do
    review_repo "$file"
    out="$file" code=0
    bash "$STAMP" touches || code=$?
    expect 0
  done
  review_repo src/App/App.cs
  out="code only" code=0
  bash "$STAMP" touches || code=$?
  expect 1
}

# A commit's scope hash covers the audit files in subfolders too, and matches the staged hash the stamp recorded.
test_commit_scope_hash_covers_a_nested_audit_file() {
  local staged
  review_repo src/App/Directory.Build.targets
  staged="$(bash "$STAMP" hash)"
  git commit -q --no-verify -m "build: nested targets"
  out="staged $staged, committed $(bash "$STAMP" hash HEAD), before $(bash "$STAMP" hash HEAD~1)" code=0
  [ "$(bash "$STAMP" hash HEAD)" = "$staged" ] || code=1
  [ "$(bash "$STAMP" hash HEAD)" != "$(bash "$STAMP" hash HEAD~1)" ] || code=1
  expect 0
}

test_commits_stage_fails_an_audit_file_commit_without_a_review_trailer() {
  local base
  review_repo src/App/Directory.Packages.props
  base="$(git rev-parse HEAD)"
  git commit -q --no-verify -m "build: skipped the hooks"
  run_check commits "$base..HEAD"
  expect 1 "touches the security-review scope without a Security-Review trailer"
}

test_review_refuses_a_check_script_commit_without_a_stamp() {
  review_repo scripts/check.sh
  commit "perf(check): unreviewed scan change"
  expect 1 "no matching stamp" "security-reviewer"
}

test_commits_stage_fails_a_guard_commit_without_a_review_trailer() {
  local base
  review_repo scripts/check.sh
  base="$(git rev-parse HEAD)"
  git commit -q --no-verify -m "perf(check): skipped the hooks"
  run_check commits "$base..HEAD"
  expect 1 "touches the security-review scope without a Security-Review trailer"
}

# legacy_hash: the scope hash a commit stamped before #306 carries, over the old scope's files in HEAD.
legacy_hash() {
  git ls-tree -r HEAD -- src/Metaphor.Hooks/ scripts/deploy-dev.ps1 scripts/smoke-test.ps1 scripts/package-mod.sh \
    scripts/fetch-prism.ps1 scripts/ReloadedDev.psm1 scripts/capture.ps1 |
    awk -F'\t' '{ split($1, m, " "); print m[1], m[3], $2 }' | sha256sum | cut -d' ' -f1
}

test_commits_stage_passes_a_hook_commit_stamped_under_the_old_scope() {
  local base
  review_repo
  stage_file scripts/check.sh "already here before the hook change"
  git commit -q --no-verify -m "chore: guard file from before #306" -m "Security-Review-Skipped: setup"
  base="$(git rev-parse HEAD)"
  stage_file src/Metaphor.Hooks/Hooks/Speech.ps1
  git commit -q --no-verify -m "feat: hook reviewed before #306"
  git commit -q --no-verify --amend -m "feat: hook reviewed before #306" -m "Security-Review: $(legacy_hash)"
  run_check commits "$base..HEAD"
  expect 0
}

test_commits_stage_fails_a_guard_commit_with_an_old_scope_trailer() {
  local base
  review_repo
  base="$(git rev-parse HEAD)"
  stage_file scripts/check.sh
  git commit -q --no-verify -m "perf(check): old-scope trailer"
  git commit -q --no-verify --amend -m "perf(check): old-scope trailer" -m "Security-Review: $(legacy_hash)"
  run_check commits "$base..HEAD"
  expect 1 "touches the security-review scope without a Security-Review trailer"
}

test_stamp_script_rejects_unknown_arguments() {
  new_repo
  out="$(bash "$STAMP" nope 2>&1)"
  code=$?
  expect 2 "usage: scripts/security-stamp.sh"
}

# --- The batched file scan (issue #297) ---

# every_rule_fixture: commit a tree where every file rule trips at least once, beside files that pass.
every_rule_fixture() {
  new_repo
  mkdir -p scripts game tools/x.rep docs/raw-sources docs/findings "with space"
  printf 'BA7816BF8F01CFEA414140DE5DAE2223B00361A396177A9CB410FF61F20015AD  some game file\n' \
    > scripts/game-content.sha256
  printf 'abc' > hashed.txt
  printf 'x' > game/a.txt
  printf 'x' > tools/x.rep/00.prp
  printf 'x' > a.cpk && printf 'x' > b.EXE && printf 'x' > c.dmp && printf 'x' > d.idb
  head -c $((1024 * 1024 + 7)) /dev/zero > big.bin
  printf '# A page\n\n- **Saved:** soon\n\n## Body\n\n- **Source:** below the header\n' > docs/raw-sources/bad.md
  printf '# Readme\n' > docs/raw-sources/README.md
  printf 'x' > docs/raw-sources/data.txt
  printf '# A finding\n\n- **Game build:** 1.0\n' > docs/findings/f.md
  printf '{}' > docs/findings/x.json
  printf 'line one\nC:\\%s\\bob\\x and /%s/alice/y\nmail me: a.person%smail.com, b@example.com\n' \
    "$users" "$home" "$at" > paths.txt
  printf 'x\0y /%s/carol/z\n' "$home" > binary.dat
  printf 'git@github.com:o/r.git icon@2x.png\n' > clean.txt
  printf 'one\ntwo zz-private-name\n' > "with space/zz-private-name.txt"
  git add -f .
  git commit -q --no-verify -m "fixture"
}

test_rules_reports_every_rule_exactly_as_before() {
  every_rule_fixture
  PII_PATTERNS=zz-private-name run_check rules
  out="$(grep -v '^scan:' <<<"$out")"
  local want
  # Captured from the per-file scan before #297, which printed findings file by file in index order.
  want="$(cat <<'EOF'
game content: a.cpk: game archive extension
game content or build output: b.EXE: executable extension
game content: big.bin: larger than 1048576 bytes (1048583)
personal information: binary.dat:1: local user path
game content: c.dmp: memory dump extension
game content: d.idb: disassembler database extension
finding: docs/findings/f.md: header has no Game build line naming the executable's SHA-256
finding: docs/findings/x.json: not a .md file
raw source: docs/raw-sources/bad.md: header has no Source line
raw source: docs/raw-sources/bad.md: header has no Saved line with a YYYY-MM-DD date
raw source: docs/raw-sources/bad.md: header has no Game build line
raw source: docs/raw-sources/data.txt: not a .md file
game content: game/a.txt: inside a game folder
game content: hashed.txt: hash matches a known game file
personal information: paths.txt:2: local user path
personal information: paths.txt:3: email address
game content: tools/x.rep/00.prp: inside a disassembler project folder
personal information: with space/zz-private-name.txt: file name matches a private pattern
personal information: with space/zz-private-name.txt:2: matches a private pattern
EOF
)"
  if [ "$code" -eq 1 ] && [ "$out" = "$want" ]; then
    passed=$((passed + 1))
  else
    failed=$((failed + 1))
    printf 'FAIL %s\n  exit %s, diff (want, got):\n%s\n' "$current" "$code" \
      "$(diff <(echo "$want") <(echo "$out") | sed 's/^/    /')"
  fi
}

# with_origin_main: commit what is staged and point origin/main at it, so later commits count as changed.
with_origin_main() {
  git commit -q --no-verify --allow-empty -m "base"
  git update-ref refs/remotes/origin/main HEAD
}

test_rules_scans_only_files_changed_since_origin_main_locally() {
  new_repo
  printf 'x' > old.cpk
  git add old.cpk
  with_origin_main
  printf 'x' > new.pak
  git add new.pak
  git commit -q --no-verify -m "new"
  printf 'x' > staged.dll
  git add staged.dll
  run_check rules
  expect 1 "new.pak" "staged.dll" "scan: 2 files changed since origin/main"
  expect_absent "old.cpk"
}

test_rules_scans_the_whole_tree_in_ci_and_on_request() {
  local env
  for env in GITHUB_ACTIONS=true METAPHOR_SCAN_ALL=1; do
    new_repo
    printf 'x' > old.cpk
    git add old.cpk
    with_origin_main
    printf 'fine\n' > new.txt
    git add new.txt
    out="$(env "$env" bash "$CHECK" rules 2>&1)"
    code=$?
    expect 1 "old.cpk" "scan: every tracked file (2)"
  done
}

test_rules_scans_the_whole_tree_when_the_rules_change() {
  new_repo
  printf 'x' > old.cpk
  git add old.cpk
  with_origin_main
  mkdir -p scripts
  printf '# none yet\n' > scripts/game-content.sha256
  git add scripts/game-content.sha256
  run_check rules
  expect 1 "old.cpk" "scan: every tracked file"
}

# stage_blob MODE PATH CONTENT: put CONTENT in the index at PATH with MODE, without touching the working tree, so a path
# the file system can't hold (an invalid UTF-8 byte) or a symlink works on every platform.
stage_blob() {
  local blob
  blob="$(printf '%s' "$3" | git hash-object -w --stdin)"
  git update-index --add --cacheinfo "$1,$blob,$2"
}

# GNU grep 3.5 and later print "binary file matches" instead of the lines when a line isn't valid UTF-8 (Ubuntu CI).
test_rules_reads_names_and_headers_past_invalid_utf8() {
  new_repo
  stage_blob 100644 "$(printf 'bad\377name.txt')" 'fine'
  stage_blob 100644 zz-private-name.txt 'fine'
  stage_blob 100644 docs/findings/f.md "$(printf '# Bad \377 title\n\n- **Game build:** %064d\n' 0)"
  out="$(LC_ALL=C.UTF-8 PII_PATTERNS=zz-private-name bash "$CHECK" rules 2>&1)"
  code=$?
  expect 1 "personal information: zz-private-name.txt: file name matches a private pattern"
  expect_absent "header has no"
}

test_rules_and_fast_scan_a_symlink_turned_into_a_file() {
  new_repo
  stage_blob 120000 link.txt 'target'
  with_origin_main
  stage_blob 100644 link.txt "mail alice.smith${at}gmail.com"
  run_check rules
  expect 1 "link.txt:1: email address"
  run_check fast
  expect 1 "link.txt:1: email address"
}

test_rules_and_fast_scan_the_right_files_from_a_subfolder() {
  new_repo
  mkdir -p src
  printf 'ok\n' > src/keep.txt
  git add src/keep.txt
  with_origin_main
  printf 'x' > src/new.cpk
  git add src/new.cpk
  cd src || exit 2
  run_check rules
  expect 1 "game content: src/new.cpk" "scan: 1 files changed since origin/main"
  run_check fast
  expect 1 "game content: src/new.cpk"
}

# count_starts FILES: run `rules` on a fresh repo of FILES text files and notes, counting the external commands it starts
# through logging stand-ins for the tools the scan uses.
count_starts() {
  local files="$1" shims="$WORK/shims-$RANDOM" log="$WORK/starts-$RANDOM" tool real i
  mkdir -p "$shims"
  for tool in git grep sha256sum perl awk sed wc cut tr cat xargs find mkdir sort head; do
    real="$(command -v "$tool")" || continue
    printf '#!/bin/sh\necho %s >> "%s"\nexec "%s" "$@"\n' "$tool" "$log" "$real" > "$shims/$tool"
    chmod +x "$shims/$tool"
  done
  new_repo
  mkdir -p scripts docs/findings
  printf '0000000000000000000000000000000000000000000000000000000000000000  nothing\n' > scripts/game-content.sha256
  for ((i = 0; i < files; i++)); do
    printf 'text %s\n' "$i" > "file$i.txt"
    printf '# F\n\n- **Game build:** 1.0\n' > "docs/findings/f$i.md"
  done
  git add -A && git commit -q --no-verify -m files
  : > "$log"
  PII_PATTERNS=zz-nobody PATH="$shims:$PATH" bash "$CHECK" rules > /dev/null 2>&1
  wc -l < "$log" | tr -d '[:space:]'
}

test_rules_process_starts_do_not_grow_with_the_file_count() {
  local few many
  current=test_rules_process_starts_do_not_grow_with_the_file_count
  few="$(count_starts 2)"
  many="$(count_starts 40)"
  out="2x2 files: $few starts; 40x2 files: $many starts"
  code=0
  [ "$many" -le "$few" ] || code=1
  expect 0 "starts"
}

# An optional argument runs only the tests whose names match it (an extended regular expression).
for current in $(declare -F | awk '{print $3}' | grep '^test_' | grep -E -- "${1:-.}"); do
  "$current"
  cd "$WORK" || exit 2
done

printf 'check tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
