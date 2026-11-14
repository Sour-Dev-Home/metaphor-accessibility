# CI never sees game content, and runs only on GitHub-hosted runners

CI runs without any game content: none is committed, passed in through secrets, or downloaded during a run. Tests that
need game data run only on a developer's machine and skip themselves when the data is absent. In-game checks are a
script a person runs, not a CI job. We also use no self-hosted runners, because this repository is public: a pull request
from a fork could run code on the machine that has the game installed.

## Considered Options

- Encrypted or private test data passed through Actions secrets: rejected, because it puts game content in CI and one
  leak (a log, an artifact) would break ground rule 4.
- A self-hosted runner with the game installed, so CI could run in-game checks: rejected for the security reason above.

## Consequences

Format work is checked in CI only against fixtures we make ourselves. Proof that something works on real game data
comes from local runs, so each change that needs it records the game build it was checked on.
