#!/usr/bin/env bash
# Black-box tests for scripts/check.sh: each test plants content in a temporary git repo, runs the check there and
# asserts the exit code and the message.
set -uo pipefail

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

test_full_stage_blocks_committed_content_that_skipped_the_hook() {
  new_repo
  mkdir -p extracted
  printf 'x' > extracted/model.bin
  git add -f extracted/model.bin
  git commit -q -m "sneak in" --no-verify
  run_check full
  expect 1 "game content" "extracted/model.bin"
}

test_full_stage_passes_clean_repo() {
  new_repo
  printf '# notes\n' > notes.md
  git add notes.md
  git commit -q -m "notes" --no-verify
  run_check full
  expect 0
}

test_rejects_unknown_stage() {
  new_repo
  run_check fastest
  expect 2 "usage"
}

test_fails_closed_outside_a_git_repo() {
  mkdir -p "$WORK/not-a-repo" && cd "$WORK/not-a-repo" || exit 2
  run_check full
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
  run_check full
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

for current in $(declare -F | awk '{print $3}' | grep '^test_'); do
  "$current"
  cd "$WORK" || exit 2
done

printf 'check tests: %d passed, %d failed\n' "$passed" "$failed"
[ "$failed" -eq 0 ]
