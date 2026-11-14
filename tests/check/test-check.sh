#!/usr/bin/env bash
# Black-box tests for scripts/check.sh: each test plants content in a temporary git repo, runs the check there and
# asserts the exit code and the message.
set -uo pipefail
unset PII_PATTERNS REQUIRE_PRIVATE_PATTERNS # tests set these per run

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
  for ext in pak exe DLL asi dmp mdmp gpr idb i64 til nam id0 id1 id2 bndb dd64; do
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

test_commits_stage_names_each_bad_commit_in_the_range() {
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

for current in $(declare -F | awk '{print $3}' | grep '^test_'); do
  "$current"
  cd "$WORK" || exit 2
done

printf 'check tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
