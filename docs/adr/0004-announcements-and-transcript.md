---
status: proposed
---

# Every feature speaks through one announcer, and dev builds keep a transcript

What the screen reader says is the main output of this mod, but no test can hear speech. So every feature hands its
announcements to one announcer interface in `Metaphor.Core`, never to a speech library directly. Unit tests use a fake
announcer and assert the exact text. Dev builds also append each announcement to a transcript file, so the smoke test
and a person debugging can check what the game would have said without listening.

## Considered options

- **Features call the speech library directly.** Simpler, but it puts Windows-only code in the features (ADR 0003
  forbids that) and leaves nothing to assert on.
- **A transcript in release builds too, for bug reports.** Useful, but it writes what a player hears to disk without them
  asking. Left out for now; an opt-in setting can add it later without changing the interface.

## Consequences

The announcer interface is the seam between features and speech, so its shape (interrupting, queueing, priority)
becomes a contract that every feature and the speech backend share. Getting it wrong later means touching every feature.
