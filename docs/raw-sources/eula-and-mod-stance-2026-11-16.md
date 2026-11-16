# EULA, Steam terms and SEGA/Atlus stance on mods

- **Source:** the following, all read 2026-11-16:
  - Steam store page, which links "Metaphor: ReFantazio EULA" and says "Requires agreement to a 3rd-party EULA":
    <https://store.steampowered.com/app/2679460/Metaphor_ReFantazio/>
  - That EULA, downloaded with `curl` and read as text: <https://store.steampowered.com/eula/2679460_eula_0>
    (HTML, 79,755 bytes, SHA-256 prefix `4e9e5be3d0a7b6d5`).
  - Steam Subscriber Agreement, downloaded with `curl` and read as text:
    <https://store.steampowered.com/subscriber_agreement/>
  - GitHub's DMCA notice repository, searched with `gh api search/code` (`repo:github/dmca` plus `atlus`, `sega`,
    `"persona 5"`, `refantazio`).
- **Not fetched:** <https://games.sega.com/EULA>, the "published at" address the EULA names for its amended versions,
  returned HTTP 404. `https://www.sega.com/terms-use`, `/terms-of-use`, `/legal` and `/eula` returned HTTP 429, so no
  general SEGA Terms of Use were read. `https://atlus.com/terms-of-use/` and `/legal/` returned 404. SEGA's support
  checklist (<https://support.sega.com/hc/en-us/articles/40493886180497-SUPPORT-CHECKLIST>) returned 403.
- **Saved:** 2026-11-16.
- **Game build:** the EULA and agreement aren't tied to a build. They apply to the Steam release as sold on that date,
  which the project's notes pin to executable SHA-256 `548ddc95…`.

## Game EULA (SEGA of America, "Effective Date: May 2, 2024")

Licensor, from the preamble: "THIS SOFTWARE IS LICENSED, NOT SOLD. SEGA OF AMERICA, INC. [...] (“SEGA” or “We”) reserve/s
all rights not expressly granted to you."

The "Editors" definition, from the preamble: "any third party software authorised for use with the Game Software by SEGA
which allows you to construct new variations, modifications, derivations, adaptations, copies or improvements of the Game
Software (“Editors”)".

**2. Limited use license:** "SEGA hereby grants you a non-exclusive, non-transferable, limited, fully revocable right and
license to install, access and use one (1) copy of the Product solely and exclusively for your personal and
non-commercial use."

**7. License conditions:** "You agree to only use the Product, or any part of it, in a manner that is consistent with
this Agreement, and you SHALL NOT:"

> (d) sell, rent, lease, license, distribute or otherwise transfer this Product or any copies;
>
> (e) reverse engineer, derive source code, modify, decompile, disassemble, copy, or create derivative works of the
> Product, in whole or in part, except where permitted by law;
>
> (f) remove, disable or circumvent any security protections, proprietary notices or labels contained on or within the
> Product, [...];
>
> (h) create data or executable programs which mimic data or functionality in the Product unless such functionality is
> provided to you in the Editors.
>
> If you commit any breach of this Agreement, your right to use the Product under this Agreement shall automatically and
> immediately terminate, without notice. Your breach of this Section shall constitute a material breach of this Agreement
> and/or of applicable copyright and other intellectual property rights laws and treaties, and may subject you to civil
> and criminal liability.

**16:** "YOU MAY ALSO HAVE STATUTORY OR OTHER RIGHTS UNDER YOUR LOCAL JURISDICTION, WHICH REMAIN UNAFFECTED."

**18 (Denuvo):** "This Product may be protected by anti-cheat/hacking software and/or Denuvo Anti-Tamper Protection
Technology [...]" and "If you disable or otherwise tamper with the Denuvo Anti-Tamper Technology, the Product may not
operate properly and you are in material breach of this Agreement."

**20:** the agreement "shall be governed in all respects by the laws of the State of California". Section 19 sets
binding individual arbitration for US and Canadian residents.

### Schedule: Modding Terms

> Where possible, SEGA encourages its users to express their creativity and enhance the experience of certain games
> through the creation of modifications [...] for use with the Product (“Mod” or “Mods”).
>
> Please note that SEGA prohibits the creation of Mods for certain games. [...] In such instances, you shall not be
> entitled to create Mods for that Product and the Modding Terms shall not apply.
>
> You can find out which Products allow for the creation of Mods by visiting the applicable Modding Page (as defined
> below). If you are unsure as to whether the creation of Mods is prohibited for your Product, please contact SEGA’s
> Customer Service Department ([address elided]). [...] The license we provide to you to create and use Mods with the
> Product may be revoked if you breach the Modding Terms.

"Modding Page" is not defined anywhere else in the text. The schedule defines "Modding Tools" and "Modding Libraries"
instead.

**Modding Terms 2 (License):** "SEGA hereby grants you a non-exclusive, non-transferable, limited, fully revocable right
and license to install, access and use the in-game modding tools, any graphical assets and/or other related tools
provided by SEGA (“Modding Tools”) solely and exclusively to create Mods for use with the Product and to publish those
Mods on the applicable SEGA approved third party Modding webpage for the Product (“Modding Libraries”)." And: "you shall
not sell, rent, lease, license, distribute or otherwise transfer or exploit the Modding Tools and/or Mods or any copies
thereof, for commercial gain or otherwise, except as permitted by this Agreement and/or the Third Party Platforms."

**Modding Terms 1 (Ownership):** "Any Mods that you create shall belong to you only insofar as the Mod contains your
original creative work." Elements based on the Product's IP, "including [...] any computer code [...] or text", belong
to SEGA.

**Modding Terms 3 (Content restrictions):** "Any element which you include in your Mod (except for the Modding Tools)
must be your own original work created by you or you must have obtained the necessary permissions to use such
materials." A Mod must not contain "any malicious code [...] or other content that contains malicious code or in any way
damages or interfere with the operation of the Product."

**Modding Terms 6:** "If you commit any breach of clauses 2 and 3 of the Modding Terms, your license to use the Product,
the Modding Tools and/or any Mods shall automatically terminate, without notice."

## Steam Subscriber Agreement ("last updated on September 10, 2026")

**1.** Subscriptions "may impose additional terms specific to that Subscription (“Subscription Terms”) (for example, an
end user license agreement specific to a particular game [...])".

**2.G (Restrictions on use):** "Except as otherwise permitted under this Agreement (including any Subscription Terms or
Rules of Use), or under applicable law notwithstanding these restrictions, you may not, in whole or in part, copy,
photocopy, reproduce, publish, distribute, translate, reverse engineer, derive source code from, modify, disassemble,
decompile, create derivative works based on, or remove any proprietary notices or labels from the Content and Services or
any software accessed via Steam without the prior consent, in writing, of Valve."

**4.B (Cheating):** "Cheats" are defined as processes "that may give a player an unfair competitive advantage when
playing multiplayer versions of any Content and Services or modifications of Content and Services". "You may not use
Cheats, mods, hacks, or any other unauthorized third-party software, to modify any Subscription Marketplace process, the
process of Steam account creation or otherwise in interacting with or controlling the processes or user interface of
Steam Content and Services, except to the degree expressly permitted."

## Official SEGA or Atlus statements

- **About mods for Metaphor: ReFantazio or Atlus PC titles:** none found. Web searches for an Atlus or SEGA statement,
  an atlus.com FAQ or a SEGA support article on mods turned up only community sites and news coverage. Search results
  attribute a "Modifications / Custom skins or tool" checkbox to SEGA's support checklist, but the page returned 403, so
  that is unread: [NEEDS VERIFICATION].
- **No Modding Page for this game was found.** The EULA's mod licence depends on one: [NEEDS VERIFICATION].
- **Accessibility:** no SEGA or Atlus statement about mods for accessibility was found. The game's own accessibility
  features and the related press are recorded in `game-builtin-accessibility-2026-11-15.md`.

## Takedown records (context only)

The `github/dmca` search found no notice that names Atlus, Persona 5 or this game's mods. The `sega` hits it checked are
a 2019 SEGA notice about the leaked source of a mobile game ("Xuccess Heaven"), a 2020 Gameloft notice about a licensed
Sonic Unleashed Java game, and a 2025 notice about a Sega Saturn hardware mod filed by that mod's own author. The only
`refantazio` hit (2026-09-22) is a dispute between two fan portfolio sites, not a SEGA notice. Code search covers only
indexed files, so this isn't proof that no notice exists.

## Bearing on this project

Which clauses touch which activity. Whether any of them binds, or how "except where permitted by law" and local
statutory rights apply, is a lawyer's call, not ours: [NEEDS VERIFICATION] throughout.

- **Reverse engineering for analysis** (ADR 0002): EULA 7(e) and SSA 2.G both prohibit it, both "except where permitted
  by law" or "under applicable law notwithstanding these restrictions". EULA 16 keeps local statutory rights.
- **Modifying the running game** (Reloaded-II hooks in memory): EULA 7(e) ("modify") and possibly 7(h) ("executable
  programs which mimic [...] functionality"). The Modding Terms "encourage" Mods for "certain games", but their licence
  covers SEGA-provided Modding Tools, and none exist for this game. Whether this game permits Mods at all is unknown.
- **Denuvo:** EULA 7(f) and 18 make tampering with Denuvo a material breach. Ground rule 5 and ADR 0002 already forbid
  it. Whether in-memory hooks count as "tamper" isn't addressed by the text.
- **Distributing a mod** (ships no game files): EULA 7(d) and Modding Terms 2 restrict distributing the Product and
  Mods. Modding Terms 1 and 3 require the Mod's elements to be original work, which matches ground rule 4. Whether
  shipping hook code that names or targets the game's functions is a "derivative work" is unknown.
- **Accessibility:** no clause and no official statement mentions accessibility or assistive software.
- **Online and Steam:** SSA 4.B's "Cheats" are tied to multiplayer advantage. Its "mods [...] to modify [...] Steam
  Content and Services" sentence could be read broadly. The project doesn't touch online features or Steam processes.
