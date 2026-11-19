# Outage output fixtures

Message shapes for `tests/bash/test-github-outage.sh` (issue #312). `shapes.txt` holds one block per shape, headed
`=== outage <name>` or `=== real <name>`. Hosts and repositories are placeholders.

- `push-*`: git over HTTPS. The `RPC failed; HTTP <code> curl 22 ...` and `hung up unexpectedly` lines come from git's
  `remote-curl.c` and transport code. `The requested URL returned error: <code>` and `Failed to connect ...` are curl's
  error strings, which git passes through.
- `gh-*`: the GitHub CLI prints API errors as `HTTP <code>: <status text> (<url>)`. The timeout line is Go's
  `net/http` client text.
- `real` blocks are failures that are not outages: auth, a rejected push, validation, a failing hook.
- The status API shape (`{"status":{"indicator":"none",...}}`) was read from
  `https://www.githubstatus.com/api/v2/status.json`, GitHub's public Statuspage endpoint, on 2026-11-18.

[NEEDS VERIFICATION] The shapes are rebuilt from those tools' message formats, not captured from a live GitHub outage.
Replace a block with a real capture the next time an outage hits.
