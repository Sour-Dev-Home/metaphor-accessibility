#!/usr/bin/env bash
# A small digest of a session's F8 marks (issue #759), so a session issue is filed from one small file instead of a
# search of the big logs. For each mark in the report log: its number, its session time and the report-log lines in the
# 60 seconds before it and 10 after, at most 40 per mark, and the spoken lines of the same window (#763).
#
# The spoken lines come from transcript.txt in the session folder, when there is one: each row starts with
# "<ms> ms ", the same clock as the report log (src/Metaphor.Core/Announcements/TranscriptAnnouncer.cs,
# src/Metaphor.Core/ReportLog.cs). Rows without that prefix (an older transcript) are left out.
#
# Usage: session-marks.sh SESSION_FOLDER REPORT_LOG
# Writes SESSION_FOLDER/session-marks.txt and prints its name. Refuses a folder inside a git repository, so the digest
# never lands in the repo. Lines are scrubbed of absolute user paths.
set -uo pipefail

usage() { echo "usage: session-marks.sh SESSION_FOLDER REPORT_LOG" >&2; exit 2; }
[ "$#" -eq 2 ] || usage
folder="$1" log="$2"
[ -d "$folder" ] || { echo "session-marks: no such folder" >&2; exit 2; }
[ -f "$log" ] || { echo "session-marks: no such report log" >&2; exit 2; }
if git -C "$folder" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
  echo "session-marks: the folder is inside a git repository; pick one outside it" >&2
  exit 2
fi

out="$folder/session-marks.txt"
before=60000 after=10000 cap=40
transcript="$folder/transcript.txt"
[ -f "$transcript" ] || transcript=/dev/null
awk -v before="$before" -v after="$after" -v cap="$cap" -v tfile="$transcript" '
  function scrub(s) {
    gsub(/[A-Za-z]:[\\\/]+Users[\\\/]+[^\\\/ ]+/, "<user-path>", s)
    gsub(/\/(home|Users)\/[^\/ ]+/, "<user-path>", s)
    return s
  }
  { lines[NR] = $0; ms[NR] = ($2 == "ms") ? $1 + 0 : -1 }
  END {
    nt = 0
    while ((getline row < tfile) > 0) {
      sub(/\r$/, "", row)
      if (row !~ /^[0-9]+ ms /) continue
      nt++; tlines[nt] = row; tms[nt] = row + 0
    }
    print "Session marks digest (report-log and transcript lines near each mark)"
    marks = 0
    for (i = 1; i <= NR; i++) {
      if (lines[i] !~ /^[0-9]+ ms mark number=[0-9]+ session=/) continue
      marks++
      n = lines[i]; sub(/.*number=/, "", n); sub(/ .*/, "", n)
      t = lines[i]; sub(/.*session=/, "", t)
      print ""
      print "mark " n " at " t " (" ms[i] " ms)"
      shown = 0
      for (j = 1; j <= NR && shown < cap; j++) {
        if (ms[j] < 0 || ms[j] < ms[i] - before || ms[j] > ms[i] + after) continue
        print "  " scrub(lines[j]); shown++
      }
      shown = 0
      for (j = 1; j <= nt && shown < cap; j++) {
        if (tms[j] < ms[i] - before || tms[j] > ms[i] + after) continue
        print "  said " scrub(tlines[j]); shown++
      }
    }
    if (marks == 0) print "no marks in this report log"
  }' "$log" > "$out" || { echo "session-marks: could not write the digest" >&2; exit 1; }
if grep -q '^no marks' "$out"; then echo "session-marks: no marks in this report log"; fi
echo "session-marks: wrote $(basename "$out") in the session folder"
