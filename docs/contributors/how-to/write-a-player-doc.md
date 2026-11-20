# Write a player doc

Player pages live in [`docs/players/`](../../players/README.md), as how-tos. Their readers use a screen reader and the
keyboard, so the page is the product.

1. **Name the audience.** Open with who the page is for and what they need first (the game, a screen reader). Say if it
   is a draft for testers on dev builds, as [install the mod](../../players/how-to/install-the-mod.md) does.
2. **Write keyboard-only steps.** One action per numbered step, naming the key or the control. Never "click" or "see":
   no step depends on the mouse, an image or a colour.
3. **Say what the screen reader announces.** After each step that changes something, give what NVDA says ("you hear
   ..."), so a listener knows the step worked. Headings give structure and link text makes sense on its own.
4. **Mark what you haven't heard.** Without a screen reader, write the announcement as you expect it and tag the step
   `[NEEDS VERIFICATION by ear]`. A tester with one checks it and removes the tag; the page's draft note says it
   exists ([ground rule 1](../../../AGENTS.md#ground-rules) applies to claims about the game).
5. **Run the checks.** `bash scripts/check.sh docs` runs markdownlint and Vale over the page and checks its links;
   lint and Vale only warn, a broken link fails ([checks](../reference/checks.md)). Then deliver as
   [the start-here map](../README.md#contributing-without-claude) says.
