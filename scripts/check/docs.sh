# shellcheck shell=bash
# check.sh's docs checks (ADR 0013): the docs stage, the docs tool lookup (find_tool, missing_tool), the lint, style and
# link checks, and prose_words and max_pr_words, which main's pr-body stage uses. Sourced by scripts/check.sh only; it
# uses report, warn and die from report.sh.
#
# Run globals: check_lint, check_style and check_links read `root` (docs_stage sets its own local one; main sets it for
# pr-body); report and warn update `failures` and `warnings`. `check_dir` is check.sh's constant, which
# find_docs_tool_dirs reads. Reads REQUIRE_DOCS_TOOLS.
# Module global read outside it: `max_pr_words`, which check.sh's pr-body code in main reads.
# Private to this module: `docs_tool_dirs`, which find_docs_tool_dirs sets, and fence_re, heading_re, link_re,
# scheme_re and own_repo_url.

# find_tool NAME: print the path of a docs tool, looking first where scripts/setup/install-docs-tools.sh puts it for this
# script's pins: the current clone's common git dir, shared by its worktrees, one folder per pin (#353).
find_tool() {
  local candidate dir
  # Callers run it in $(...), so they look the folders up first (docs_tool_dirs), once per run, not once per tool.
  [ -n "${docs_tool_dirs+set}" ] || find_docs_tool_dirs
  while IFS= read -r -d : dir; do
    for candidate in "$dir/$1" "$dir/$1.exe"; do
      [ -n "$dir" ] && [ -x "$candidate" ] && { echo "$candidate"; return 0; }
    done
  done <<<"$docs_tool_dirs:"
  command -v "$1"
}

# find_docs_tool_dirs: set docs_tool_dirs to the current clone's docs tool folders, colon-separated, or to nothing.
find_docs_tool_dirs() {
  docs_tool_dirs="$(bash "$check_dir/setup/install-docs-tools.sh" --path 2> /dev/null)" || docs_tool_dirs=""
}

# docs_stage: the docs checks, on the working tree's tracked Markdown files. A broken relative link or anchor fails the
# run; lint, style and length findings are warnings. Missing tools are skipped with a warning, unless
# REQUIRE_DOCS_TOOLS=1 (CI), where a missing tool must not pass quietly. It works from the root, in a local `root` its
# helpers read, and returns to the caller's folder, so a step after it sees the run's root and folder unchanged (#365).
docs_stage() {
  local root here="$PWD" code
  root="$(git rev-parse --show-toplevel)" || die "could not find the repository root"
  cd "$root" || die "could not enter $root"
  docs_checks
  code=$?
  cd "$here" || die "could not return to $here"
  return "$code"
}
docs_checks() {
  local files=() file tool
  # Left out: raw sources, which are saved copies of other people's pages linking to the original site; the code of
  # conduct, which is upstream text kept verbatim; and CLAUDE.md, which is only an import line.
  while IFS= read -r -d '' file; do files+=("$file"); done < <(
    git ls-files -z -- '*.md' ':!:docs/raw-sources/**' ':!:.github/CODE_OF_CONDUCT.md' ':!:CLAUDE.md'
  )
  [ "${#files[@]}" -gt 0 ] || return 0

  find_docs_tool_dirs
  if tool="$(find_tool lychee)"; then
    check_links "$tool" "${files[@]}"
  else
    missing_tool lychee
  fi
  check_github_links "${files[@]}"
  if tool="$(find_tool vale)"; then
    check_style "$tool" "${files[@]}"
  else
    missing_tool vale
  fi
  # markdownlint-cli2 also loads a config from each linted file's folder, even with --config, and a .cjs or .mjs one
  # (or a custom rule a .jsonc one names) runs code in the docs step (#322). Only tools/docs/, in the security-review
  # scope, may hold one; with any other, markdownlint doesn't run. The name matches in any case, as Windows reads it
  # (#324); the folder doesn't, since the review scope's tools/docs/ doesn't cover a Tools/Docs/ that Windows merges in.
  local configs=()
  while IFS= read -r -d '' file; do configs+=("$file"); done < <(
    git ls-files -z -- ':(glob,icase)**/.markdownlint*' ':(exclude,glob)tools/docs/**'
  )
  if [ "${#configs[@]}" -gt 0 ]; then
    for file in "${configs[@]}"; do
      report "$file: markdownlint config outside tools/docs/; markdownlint would load it and run its code"
    done
  elif tool="$(find_tool markdownlint-cli2)"; then
    check_lint "$tool" "${files[@]}"
  else
    missing_tool markdownlint-cli2
  fi

  # An ADR is one paragraph plus optional sections.
  local count
  for file in "${files[@]}"; do
    case "$file" in
      docs/adr/*.md)
        count="$(prose_words < "$file")"
        [ "$count" -le "$max_adr_words" ] ||
          warn "$file: $count words; ADRs aim for at most $max_adr_words" "$file" ;;
    esac
  done
}

max_adr_words=250
max_pr_words=300

# prose_words: count the words on stdin, leaving out headings and fenced code blocks, which hold diagrams and sketches
# rather than prose.
# Builtins only: the docs stage runs it once per ADR, where tr, awk, wc and tr were four starts each (#328).
fence_re='^[[:space:]]*(```|~~~)'
heading_re='^#+ '
prose_words() {
  local line code=0 count=0 words
  while IFS= read -r line || [ -n "$line" ]; do
    line="${line//$'\r'/}"
    if [[ "$line" =~ $fence_re ]]; then
      code=$((1 - code))
      continue
    fi
    [ "$code" -eq 0 ] && ! [[ "$line" =~ $heading_re ]] || continue
    read -r -a words <<< "$line"
    count=$((count + ${#words[@]}))
  done
  echo "$count"
}

# check_lint MARKDOWNLINT FILE...: warn on each markdownlint finding (settings in tools/docs/.markdownlint-cli2.jsonc).
check_lint() {
  local markdownlint="$1" config="$root/tools/docs/.markdownlint-cli2.jsonc" output status line
  shift
  [ -f "$config" ] || die "markdownlint needs tools/docs/.markdownlint-cli2.jsonc"
  output="$("$markdownlint" --config "$config" -- "$@" 2>&1)"
  status=$?
  [ "$status" -le 1 ] || die "markdownlint failed to run: $output"
  while IFS= read -r line; do
    line="${line%$'\r'}"
    # path:line[:column] severity rule/name description
    [[ "$line" =~ ^([^:]+):([0-9]+)(:[0-9]+)?\ [a-z]+\ (MD[0-9]+.*)$ ]] || continue
    warn "${BASH_REMATCH[1]}:${BASH_REMATCH[2]}: ${BASH_REMATCH[4]}" "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}"
  done <<<"$output"
}

# check_style VALE FILE...: warn on each Vale finding, such as a word from tools/docs/.vale/styles/Project/Filler.yml.
check_style() {
  local vale="$1" config="$root/tools/docs/.vale.ini" output status line
  shift
  [ -f "$config" ] || die "Vale needs tools/docs/.vale.ini"
  output="$("$vale" --config="$config" --output=line --no-exit -- "$@" 2>&1)"
  status=$?
  [ "$status" -eq 0 ] || die "Vale failed to run: $output"
  while IFS= read -r line; do
    line="${line%$'\r'}"
    # path:line:column:rule:message
    [[ "$line" =~ ^([^:]+):([0-9]+):[0-9]+:[^:]+:(.*)$ ]] || continue
    warn "${BASH_REMATCH[1]}:${BASH_REMATCH[2]}: ${BASH_REMATCH[3]}" "${BASH_REMATCH[1]}" "${BASH_REMATCH[2]}"
  done <<<"$output"
}

# url_encode PATH: print PATH percent-encoded byte by byte, keeping / and : (C:/...). lychee's --remap splits on
# whitespace and its replacement reads $ as a group, so a clone path with a space, $ or é must be encoded (#436).
url_encode() {
  local LC_ALL=C path="$1" out="" char i
  for ((i = 0; i < ${#path}; i++)); do
    char="${path:i:1}"
    case "$char" in
      [A-Za-z0-9/._~:-]) out+="$char" ;;
      *) printf -v char '%%%02X' "'$char"; out+="$char" ;;
    esac
  done
  printf '%s' "$out"
}

# check_github_links FILE...: report each relative link in Markdown under .github/. GitHub shows those files outside
# their folder too, such as CONTRIBUTING on the Contributing tab, where a relative link 404s (#436). Links within the
# page (#...) and with a scheme pass; fenced code is skipped.
link_re='\]\(<?([^)> ]+)|^ {0,3}\[[^]]+\]:[[:space:]]*<?([^> ]+)'
scheme_re='^([A-Za-z][A-Za-z0-9+.-]*:|#)'
check_github_links() {
  local file line n code rest target
  for file in "$@"; do
    [[ "$file" == .github/* ]] || continue
    n=0 code=0
    while IFS= read -r line || [ -n "$line" ]; do
      n=$((n + 1))
      line="${line//$'\r'/}"
      if [[ "$line" =~ $fence_re ]]; then
        code=$((1 - code))
        continue
      fi
      [ "$code" -eq 0 ] || continue
      rest="$line"
      while [[ "$rest" =~ $link_re ]]; do
        target="${BASH_REMATCH[1]}${BASH_REMATCH[2]}"
        rest="${rest#*"${BASH_REMATCH[0]}"}"
        [[ "$target" =~ $scheme_re ]] ||
          report "relative link: $file:$n: $target (it 404s where GitHub shows the file elsewhere; use $own_repo_url/blob/main/...)"
      done
    done < "$file"
  done
}

# missing_tool NAME: skip a docs tool that isn't installed, or fail when CI requires it.
missing_tool() {
  [ "${REQUIRE_DOCS_TOOLS:-}" = 1 ] &&
    die "$1 is not installed, but REQUIRE_DOCS_TOOLS=1 (run scripts/setup/install-docs-tools.sh)"
  warn "$1 is not installed, so its checks were skipped (run scripts/setup/install-docs-tools.sh)"
}

# This repository on GitHub. Files GitHub shows outside their folder link to it absolutely (#435).
own_repo_url=https://github.com/Sour-Dev-Home/metaphor-accessibility

# check_links LYCHEE FILE...: report each broken relative link or anchor, and each link to a missing path or anchor on
# this repository's main branch, which lychee checks against the working tree (#436). Offline, so outside sites can't
# fail the run.
check_links() {
  local lychee="$1" output status line current="" target where reason found=0 tree
  shift
  # The working tree (docs_stage runs from the root) as a file URL: C:/... from Git Bash, the path itself elsewhere.
  tree="$(pwd -W 2> /dev/null)" && tree="file:///$(url_encode "$tree")/" || tree="file://$(url_encode "$PWD")/"
  output="$("$lychee" --offline --include-fragments --no-progress \
    --remap "^https?://(?:www\.)?github\.com/(?i:${own_repo_url#https://github.com/})/(?:blob|tree)/main/ $tree" \
    -- "$@" 2>&1)"
  status=$?
  [ "$status" -eq 0 ] && return 0
  [ "$status" -eq 2 ] || die "lychee failed to run: $output"
  while IFS= read -r line; do
    line="${line%$'\r'}"
    if [[ "$line" =~ ^\[(.+)\]:$ ]]; then
      current="${BASH_REMATCH[1]}"
    elif [[ "$line" =~ ^\[ERROR\]\ (.+)\ \(at\ ([0-9]+):[0-9]+\)\ \|\ (.*)$ ]]; then
      target="${BASH_REMATCH[1]#file://}"
      target="${target#/}"
      target="${target#"${root#/}"/}"
      where="${BASH_REMATCH[2]}"
      reason="${BASH_REMATCH[3]}"
      # A remapped link of this repository's: name it as written, not as the local file.
      if [[ "$reason" =~ ^(.*)\ \|\ Remaps:\ ([^ ]+)\ --\> ]]; then
        reason="${BASH_REMATCH[1]}"
        target="${BASH_REMATCH[2]}"
      fi
      report "broken link: $current:$where: $target ($reason)"
      found=1
    fi
  done <<<"$output"
  # lychee found something this parser can't read (its output format changed): fail rather than pass quietly.
  [ "$found" -eq 1 ] || die "lychee reported broken links in a format this script can't read: $output"
}
