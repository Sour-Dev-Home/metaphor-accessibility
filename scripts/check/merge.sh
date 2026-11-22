# shellcheck shell=bash
# check.sh's merge reuse (#728, docs/adr/0021-merge-checks-reuse-parent-passes.md): `full` on a merge whose parents'
# trees have recorded passes runs only what both parents' changes reach. Sourced by check.sh's full only; it uses
# cached_pass, record_pass, full_tree, take_lock and timed_step from run.sh, build_selected and run_selected_tests from build.sh,
# and report from report.sh.
#
# Run globals: merge_plan sets merge_note, merge_dotnet, merge_pester and merge_check, which main reads and resets
# (merge_note); merge_checks reads `solution`, `root`, `full_tree_id` and `failures`. `check_dir` is check.sh's
# constant. Reads GITHUB_ACTIONS, METAPHOR_FULL_TESTS and SKIP_SCRIPT_TESTS.

merge_note="" merge_dotnet="" merge_pester="" merge_check=""

# merge_parent_trees TREE: print the parents' trees, one line each, of the merge whose result TREE is: the merge being
# committed (MERGE_HEAD; its result is the index), or HEAD when it has two parents and TREE is its tree.
merge_parent_trees() {
  local git_dir heads=() commits trees
  git_dir="$(git rev-parse --absolute-git-dir 2> /dev/null)" || return 1
  if [ -f "$git_dir/MERGE_HEAD" ]; then
    mapfile -t heads < "$git_dir/MERGE_HEAD" || return 1
    heads=("${heads[@]%$'\r'}")
    [ "${#heads[@]}" -eq 1 ] || return 1 # an octopus merge
    git rev-parse "HEAD^{tree}" "${heads[0]}^{tree}" 2> /dev/null
    return
  fi
  commits="$(git rev-list --parents -n 1 HEAD 2> /dev/null)" || return 1
  read -ra commits <<< "$commits"
  [ "${#commits[@]}" -eq 3 ] || return 1
  trees="$(git rev-parse "${commits[0]}^{tree}" "${commits[1]}^{tree}" "${commits[2]}^{tree}" 2> /dev/null)" ||
    return 1
  [ "${trees%%$'\n'*}" = "$1" ] || return 1 # changes staged on top of the merge
  echo "${trees#*$'\n'}"
}

# merge_side PARENT TREE: what-runs.sh's lines for the files where TREE differs from the tree PARENT: the other
# parent's changes, and any file matching neither. --no-renames lists a moved file's old path too. A deleted file
# also reaches the check-script suites: test-repo-config.sh fails on a CODEOWNERS path that no longer exists.
merge_side() {
  local files deleted
  files="$(git diff --name-only --no-renames "$1" "$2")" || return 1
  [ -n "$files" ] || return 0
  deleted="$(git diff --name-only --no-renames --diff-filter=D "$1" "$2")" || return 1
  bash "$check_dir/ci/what-runs.sh" <<< "$files" || return 1
  [ -z "$deleted" ] || echo "check-tests ${deleted%%$'\n'*} deleted (a CODEOWNERS path may name it)"
}

# merge_reaches SIDE LINE: exit 0 when SIDE (one parent's what-runs lines, or * for a parent with no recorded pass)
# reaches the project LINE names: the same `dotnet` or `build` line, or a build-wide change. Builtin matches only.
merge_reaches() {
  local kind target rest
  [ "$1" != "*" ] || return 0
  read -r kind target rest <<< "$2"
  case "$kind" in
    dotnet | build) [[ $'\n'"$1" == *$'\n'"$kind $target "* || $'\n'"$1" == *$'\n'"solution "* ]] ;;
    solution) [[ $'\n'"$1" == *$'\n'"solution "* ]] ;;
    *) return 1 ;;
  esac
}

# merge_touches SIDE KIND...: exit 0 when SIDE (as above) has a line of one of KINDs.
merge_touches() {
  local side="$1" kind
  shift
  [ "$side" != "*" ] || return 0
  for kind in "$@"; do [[ $'\n'"$side" != *$'\n'"$kind "* ]] || return 0; done
  return 1
}

# merge_plan TREE: exit 0 when TREE is a merge with a recorded `full` pass for at least one parent's tree, and set the
# plan: merge_dotnet, the .NET lines both sides reach (a side without a pass reaches everything); merge_pester and
# merge_check, set when both sides touch the Pester tests or the check-script suites; merge_note, the record's tail
# naming the parent passes relied on. Never in CI or with METAPHOR_FULL_TESTS or SKIP_SCRIPT_TESTS.
merge_plan() {
  local tree="$1" trees t1 t2 s1="*" s2="*" p1=- p2=- side sides i line kind target rest
  [ "${GITHUB_ACTIONS:-}" != true ] && [ -z "${METAPHOR_FULL_TESTS:-}" ] && [ -z "${SKIP_SCRIPT_TESTS:-}" ] || return 1
  trees="$(merge_parent_trees "$tree")" || return 1
  { read -r t1 && read -r t2; } <<< "$trees" || return 1
  if cached_pass "$t1" full && side="$(merge_side "$t1" "$tree")"; then s1="$side" p1="$t1"; fi
  if cached_pass "$t2" full && side="$(merge_side "$t2" "$tree")"; then s2="$side" p2="$t2"; fi
  [ "$p1$p2" != -- ] || return 1
  local -A seen=()
  merge_dotnet=""
  sides=("$s1" "$s2")
  # Each side's .NET lines the other side reaches too: a project whose inputs one side left alone is that parent's.
  for i in 0 1; do
    [ "${sides[i]}" != "*" ] || continue
    while read -r kind target rest; do
      case "$kind" in dotnet | build | solution) ;; *) continue ;; esac
      line="$kind $target $rest"
      [ -z "${seen[$kind $target]:-}" ] && merge_reaches "${sides[1 - i]}" "$line" || continue
      seen[$kind $target]=1
      merge_dotnet+="$line"$'\n'
    done <<< "${sides[i]}"
  done
  merge_pester="" merge_check=""
  merge_touches "$s1" pester pester-reads && merge_touches "$s2" pester pester-reads && merge_pester=1
  merge_touches "$s1" check-tests && merge_touches "$s2" check-tests && merge_check=1
  merge_note="merge $p1 $p2"
}

# merge_steps: print how many timed steps the plan runs, for the live log's count.
merge_steps() {
  local n=0 tests=0 builds=0 kind rest
  if [ -n "$merge_dotnet" ]; then
    while read -r kind rest; do
      case "$kind" in dotnet) tests=$((tests + 1)) builds=$((builds + 1)) ;; build) builds=$((builds + 1)) ;; esac
    done <<< "$merge_dotnet"
    [[ $'\n'"$merge_dotnet" != *$'\n'"solution "* && "$builds" -gt 0 ]] || builds=1
    n=$((builds + tests + 1))
  fi
  [ -z "$merge_pester" ] || n=$((n + 1))
  [ -z "$merge_check" ] || n=$((n + 1))
  echo "$n"
}

# merge_checks: full's checks after the repo rules, for a merge with a plan. Runs what the plan names, then records a
# pass for the tree, its line naming the parent passes it relied on ("full <tree> merge <tree 1|-> <tree 2|->").
merge_checks() {
  local parents="${merge_note#merge }"
  echo "full: a merge; relying on the recorded passes of tree ${parents// / and } (- is a parent without one), and" \
    "running only what both sides' changes reach (docs/adr/0021-merge-checks-reuse-parent-passes.md)"
  export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
  if [ -n "$merge_dotnet" ]; then
    if build_selected "$solution" "$root" 0 "$merge_dotnet"; then
      run_selected_tests "$solution" 0 "$merge_dotnet"
    else
      report "build: failed, see the errors above"
    fi
    timed_step format dotnet format whitespace "$solution" --verify-no-changes ||
      report "format: files need formatting (run: dotnet format whitespace Metaphor.sln)"
  else
    echo "full: .NET build and tests reused: no project's inputs changed on both sides"
  fi
  if [ -n "$merge_pester" ]; then
    timed_step pester bash "$check_dir/check/run-pester.sh" ||
      report "tests: smoke or deploy script tests failed (run: bash scripts/check/run-pester.sh)"
  else
    echo "full: Pester tests reused: one side changed nothing they read"
  fi
  if [ -n "$merge_check" ]; then
    timed_step check-scripts bash "$check_dir/check/run-check-suites.sh" --select ||
      report "tests: check-script suites failed (run: bash scripts/check/run-check-suites.sh)"
  else
    echo "full: check-script suites reused: one side changed nothing they read"
  fi
  if [ "$failures" -eq 0 ] && [ -n "${full_tree_id:-}" ] && [ "$(full_tree)" = "$full_tree_id" ]; then
    # A plan that ran nothing took no slot; the record is written under one, as every other full's is.
    take_lock
    # shellcheck disable=SC2086 # the note's words are the record's fields
    record_pass full "$full_tree_id" $merge_note
  fi
}
