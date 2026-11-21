# Code signing options for a solo GPL-3.0 Windows project

- **Source:** web pages read with a fetch tool on the date below; each claim names its URL (Microsoft Learn, SignPath,
  Certum, SSL.com, CA/Browser Forum).
- **Saved:** 2026-11-21
- **Game build:** none (this note is about Windows code signing, not Metaphor: ReFantazio)
- **Not reached:** see the [UNVERIFIED] and [NEEDS VERIFICATION] marks below. Nothing was applied for or downloaded.

Dates after each URL are the page's own date or "retrieved" date (2026-11-21) where the page gives none. Items marked [UNVERIFIED] rest on a weak source.

## 1. SignPath Foundation (free OSS signing)
- Cost: free for OSS. https://signpath.org/ (retrieved 2026-11-21)
- Eligibility: OSI-approved licence, no commercial dual licensing, actively maintained, already released, documented on download page; "team responsible for code signing must also be the team responsible for development"; MFA for team members; own binaries only (no upstream). https://signpath.org/terms (retrieved 2026-11-21). Whether a lone individual may apply is not stated on the page; the terms speak of "teams" [UNVERIFIED for solo maintainers; ask via /apply].
- Publisher name: "SignPath Foundation" (cert issued to a legal entity, the foundation). Project must show "Free code signing provided by SignPath.io, certificate by SignPath Foundation." Your legal name does not appear. https://signpath.org/terms
- Key: generated and stored on SignPath's HSM; you never hold it. https://signpath.org/ CI signs without any token on your PC.
- Identity validation: site says "No need for personal identification"; the project/repo is vetted instead. https://signpath.org/
- Constraints: binaries must be built from source "in a verifiable way", technically enforced, build scripts and CI config in the repo are in scope; every release needs manual approval; metadata (product name/version) enforced; must publish a code signing policy page; no hacking tools; must offer uninstall; must announce system changes. The word "reproducible" is not on the terms page. Code of conduct still draft. https://signpath.org/terms
- GitHub Actions: uses the "GitHub.com" Trusted Build System; for OSS all workflow jobs must run on GitHub-hosted runners; artifact must be uploaded with actions/upload-artifact v4+ and passed to `submit-signing-request` as `github-artifact-id`. https://docs.signpath.io/trusted-build-systems/github (retrieved 2026-11-21). Supported file types (exe/ps1/msi) not confirmed on that page; see /artifact-configuration docs.

## 2. Microsoft Artifact Signing (renamed from Trusted Signing; GA announced Jan 2026)
- Cost: from $9.99/month (about $120/yr) per MS Learn; Basic SKU 5,000 signatures/month, Premium 100,000. Azure pricing page shows no numbers; use the calculator. https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation (ms.date 2026-05-04); https://azure.microsoft.com/en-us/pricing/details/artifact-signing/ (retrieved 2026-11-21). Needs a paid Azure subscription (no free/trial/sponsored). https://learn.microsoft.com/en-us/azure/artifact-signing/faq (updated 2026-11-20)
- Eligibility: Public Trust for organizations in US, Canada, EU, UK, Australia, NZ, Japan, South Korea, Singapore, Switzerland, Norway, Israel. Individual developers must be in the US or Canada. https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart (ms.date 2026-05-21, updated 2026-11-20). No open-source requirement. Individual validation needs an Azure billing account of type "Individual".
- Publisher name: for an individual, the legal name from the government ID / billing account appears in the cert (plus city/state/country; street/postal optional). CN/O cannot be customised; CN must be the validated legal name. https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart ; https://learn.microsoft.com/en-us/azure/artifact-signing/faq
- Key: stays in the service, FIPS 140-3 Level 3 HSMs; never given to you; certs are short-lived and auto-renewed. CI signs with Entra service principal/OIDC, no token. SignTool+dlib, PowerShell, GitHub Actions and SDK supported. https://learn.microsoft.com/en-us/azure/artifact-signing/overview (ms.date 2026-01-02) ; faq (above); integrations: https://learn.microsoft.com/en-us/azure/artifact-signing/how-to-signing-integrations
- Identity validation (individual): government photo ID (passport, licence, ID card) via third-party AU10TIX, Microsoft Authenticator Verified ID with Face Check, address proof (utility bill/bank statement) if needed; name/address must match billing account. Takes 1 to 30 business days (company validation page); identity validation must be renewed (reminders from 60 days before expiry). https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart ; faq
- Constraints: no EV, none planned. Signs any SignTool file type (so PowerShell scripts, exe, msi). Account may be suspended/certs revoked for abuse. No reproducible-build requirement. https://learn.microsoft.com/en-us/azure/artifact-signing/faq

## 3. Certum Open Source Code Signing (cloud, SimplySign)
- Cost: EUR 49 gross per the shop page (1 yr). https://shop.certum.eu/open-source-code-signing-on-simplysign.html (retrieved 2026-11-21). Reseller figures vary; confirm in shop.
- Eligibility: individuals only (not organisations), for free/open-source software; issued "only to individuals". Revoked if used to sign commercially distributed software. Any country that Certum serves (not checked). https://support.certum.eu/en/code-signing-required-documents/ (retrieved 2026-11-21)
- Publisher name: your legal (natural-person) name, CN includes "Open Source Developer", O = "Open Source Developer". Same Certum source.
- Key: Certum cloud virtual card (SimplySign); standard Certum cloud code signing states FIPS 140-2 L3 / CC EAL4+ and a 5,000 signatures/month cap. https://shop.certum.eu/standard-code-signing-in-the-cloud.html
- CI: not documented by Certum. Access uses SimplySign mobile app one-time codes + SimplySign Desktop. An unofficial tool (ssign, reverse-engineered, not endorsed) automates it from GitHub Actions with a stored TOTP seed; possible ToS conflict. https://docs.rs/ssign [UNVERIFIED; ask Certum]. Practical risk: CI needs the OTP seed as a secret.
- Identity validation: ID check (automatic, registration point, notary, or ID photos both sides), utility bill in your name, public URL of the open-source project showing your link to it; no certificate issued if project cannot be identified. Issue time 1-5 days. https://support.certum.eu/en/code-signing-required-documents/ ; https://shop.certum.eu/code-signing.html
- Validity: CA/B max now 460 days; Certum states 459 days from 2026-02-27. https://shop.certum.eu/standard-code-signing-in-the-cloud.html

## 4. Standard OV certificates and the 2023 HSM rule
- Since 2023-06-01 CA/B Forum Code Signing BRs require the subscriber private key be generated, stored and used in a hardware crypto module (commonly cited: FIPS 140-2 L2 / CC EAL4+, non-exportable). So a plain downloadable .pfx is no longer issued. Primary doc: https://cabforum.org/working-groups/code-signing/requirements/ (v3.11.0, 2026-06-16; section 6.2.7.4.2; text garbled in the fetch, so exact wording [UNVERIFIED]); summary https://www.ssl.com/article/code-signing-key-storage-requirements-will-change-on-june-1-2023/
- Max validity 460 days from 2026-03-01 (Ballot CSC-31, adopted 2025-11-17). https://cabforum.org/2025/11/17/ballot-csc-31-maximum-validity-reduction
- Cost: Certum Standard cloud from EUR 209 (shop page, retrieved 2026-11-21); reseller listing EUR 169/yr 1-yr term (unofficial). SSL.com page showed no prices.
- Eligibility/name: SSL.com "IV" (individual validation) for individuals with no business docs; "OV" and "EV" need an organisation; "EV Sole Proprietor" for sole traders. https://www.ssl.com/certificates/code-signing/ (retrieved 2026-11-21). Certum Standard can name a natural person, person+org, or org. Publisher = the validated name (your legal name for IV/individual).
- Key/CI: hardware token on your PC (CI cannot sign without it), or a cloud HSM service. SSL.com eSigner (cloud HSM, headless CI incl. GitHub Actions) https://www.ssl.com/how-to/cloud-code-signing-integration-with-github-actions/ ; DigiCert KeyLocker has a GitHub Action. Certum cloud: see section 3.

## 5. EV certificates
- Same HSM/hardware rule, organisation (or sole-proprietor) validation, typically highest price (not priced from a primary source here; Certum shop has an EV cloud product, https://shop.certum.eu/ev-code-signing-in-the-cloud.html).
- No longer gives instant SmartScreen reputation (see below). Microsoft: "Paying a premium for EV solely to avoid SmartScreen warnings is no longer justified." https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation (ms.date 2026-05-04, updated 2026-08-17)
- Not available via Artifact Signing (no plan). https://learn.microsoft.com/en-us/azure/artifact-signing/faq

## SmartScreen (same Microsoft page above, ms.date 2026-05-04)
- Two signals: publisher (certificate) reputation and file-hash reputation.
- Unsigned: "Windows protected your PC" prompt; reputation restarts from zero for every new version.
- Signed with valid OV/EV: still shows a warning (app flagged unrecognised, verified publisher name shown) until reputation accumulates. Applies to new publishers. Self-signed behaves like unsigned.
- Reputation accrues per file hash and per certificate; signed new versions can inherit certificate reputation; unsigned cannot. Signing identity must stay consistent (changing cert resets the publisher signal). No manual submission for consumer endpoints; it can take "several weeks and hundreds of clean installs". Artifact Signing FAQ gives the same advice and suggests submitting at https://www.microsoft.com/wdsi if prompts persist (faq, 2026-11-20).
- Only the Microsoft Store avoids the prompt.
- Caveat for SignPath/Certum: reputation then builds on the SignPath Foundation / Certum-issued certificate identity, which is shared in SignPath's case [inference, not stated by Microsoft].

## Smart App Control
- Yes, in effect. If cloud intelligence cannot confidently predict the file safe, SAC checks for a valid signature; unsigned or invalidly signed is blocked. Applies to all executables, not just downloads. https://support.microsoft.com/en-us/topic/what-is-smart-app-control-285ea03d-fa88-4d56-882e-6698afdb7003 (retrieved 2026-11-21, page captured ~2026-06-12); also the SmartScreen page's tip above.
- Nuance: a signed exe is allowed only if the signature is valid and chains to a trusted root; reputation can also allow unsigned files. It can be turned off in Windows Security; off in enterprise-managed/developer-mode devices (per the Microsoft page). PowerShell scripts under SAC: not verified in this research [UNVERIFIED].

## Quick comparison for a solo maintainer
| Option | $/yr | Publisher | Solo OK | CI w/o token |
|---|---|---|---|---|
| SignPath Foundation | 0 | SignPath Foundation | unclear, ask | yes (GitHub-hosted runners, manual approval each release) |
| Artifact Signing | ~120 | your legal name (US/Canada individuals) | yes, US/CA only | yes (OIDC/service principal) |
| Certum Open Source | ~49 EUR | your name + "Open Source Developer" | yes | not documented; OTP-based |
| OV/IV standard | ~170-210+ | your name (IV) or org | IV yes | only with cloud HSM service |
| EV | higher | org / sole proprietor | org needed | cloud HSM only; no SmartScreen advantage |

## Follow-up facts (second check, retrieved 2026-11-21)

Microsoft Learn, Artifact Signing quickstart and FAQ (https://learn.microsoft.com/en-us/azure/artifact-signing/quickstart ; https://learn.microsoft.com/en-us/azure/artifact-signing/faq):
- For an individual, the certificate shows city, state/province and country from the billing address. There is no option to exclude city or state, and a custom CN/O is not allowed.

SignPath Foundation terms (https://signpath.org/terms, draft):
- The certificate is issued to "SignPath Foundation".
- The terms set no minimum team size, but require team roles (author, reviewer, approver).

Certum Open Source:
- Whether the certificate's locality fields (city, state) appear is unconfirmed [NEEDS VERIFICATION].
