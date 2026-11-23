# Changelog fragments

A PR that players would notice adds one file here instead of a line in `CHANGELOG.md`, so two PRs never touch the same
file. The release recipe joins the fragments into `CHANGELOG.md` and deletes them
([cut a release](../docs/contributors/how-to/cut-a-release.md)).

- **Name:** `<id>.<kind>.md`. `<id>` is the issue number (or a short slug such as `+installer` when there is none).
  `<kind>` is `added`, `changed`, `fixed` or `removed`; they become the headings Added, Changed, Fixed and Removed.
- **Content:** one bullet, exactly as it should read in `CHANGELOG.md`: starts with `- `, a bold lead, then the
  sentence, wrapped at 120 columns, ending with the issue number and, if it has not been heard yet, the tag
  `[NEEDS VERIFICATION by ear]`. It lists only what a player would notice.
- **Links:** use full GitHub URLs, since a relative link breaks when the text moves to the root.
- **Check:** `bash scripts/release/join-changelog.sh --check`.
- **Transition:** many open PRs still add lines under `## Unreleased` in `CHANGELOG.md`. Both ways work: the join
  puts those lines above the kinds, unchanged. New PRs use a fragment; once the older PRs have merged, nothing should
  be added under `## Unreleased` by hand.

Fragment links to other files are full GitHub URLs, because the fragment moves from here to the repository root.
