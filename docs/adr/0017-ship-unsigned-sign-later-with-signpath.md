---
status: accepted
---

# Ship an unsigned installer now; sign later through SignPath Foundation

Decided by the maintainer on 2026-11-21 (#607, #608, #609).

Until v0.1.0 we ship a single-file installer exe beside the zip, unsigned (#608). Players click "More info", then "Run
anyway". Where Smart App Control blocks the exe, the zip is the fallback. The installer asks its questions in message
boxes (#607).

After v0.1.0 we apply to SignPath Foundation and sign from GitHub Actions (#609). We never pay for a Microsoft signing
service. A certificate may show the maintainer's name and country, but not a city. If SignPath declines, we stay
unsigned.

The exe is built reproducibly and attested, with signing as the last step, so unsigned and signed builds differ only by
the signature. Facts and sources: [code signing options](../raw-sources/code-signing-options-2026-11-21.md).

## Considered options

- **Microsoft Artifact Signing:** paid, and an individual's certificate shows city and state with no way to exclude them.
- **Certum Open Source:** paid, shows the maintainer's name, and its locality fields are unconfirmed
  [NEEDS VERIFICATION].
- **OV or EV certificate:** paid and tied to a hardware token or cloud HSM; EV no longer buys SmartScreen reputation.
- **SignPath Foundation (chosen, later):** free, certificate issued to the foundation. It requires team roles (author,
  reviewer, approver) and a verifiable build; whether a solo maintainer qualifies is unconfirmed [UNVERIFIED].

## Consequences

Players see a SmartScreen warning until we sign; the install docs say so. The build must stay verifiable for SignPath's
terms, so reproducibility and attestation are release work, not an afterthought.
