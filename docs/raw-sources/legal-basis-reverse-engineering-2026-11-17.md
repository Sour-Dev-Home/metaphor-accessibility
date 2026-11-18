# Legal texts on reverse engineering for interoperability

- **Source:** the following, each downloaded with `curl` on 2026-11-17 and read as text:
  - Metaphor: ReFantazio EULA (SEGA of America, "Effective Date: May 2, 2024"):
    <https://store.steampowered.com/eula/2679460_eula_0> (79,755 bytes, SHA-256 prefix `e792bb78ea8b5eeb`; the same
    size as the 2026-11-16 copy in `eula-and-mod-stance-2026-11-16.md`, a different hash, so the page carries some
    per-request bytes. The clauses quoted below match that note word for word).
  - Steam Subscriber Agreement ("last updated on September 10, 2026"): <https://store.steampowered.com/subscriber_agreement/>
  - Directive 2009/24/EC on the legal protection of computer programs, EUR-Lex:
    <https://eur-lex.europa.eu/legal-content/EN/TXT/HTML/?uri=CELEX:32009L0024>
  - 17 U.S.C. § 1201, Cornell LII: <https://www.law.cornell.edu/uscode/text/17/1201>
  - Sega Enterprises Ltd. v. Accolade, Inc., 977 F.2d 1510 (9th Cir. 1992), Public.Resource.Org reporter copy:
    <https://law.resource.org/pub/us/case/reporter/F2/977/977.F2d.1510.92-15655.html>
  - Sony Computer Entertainment, Inc. v. Connectix Corp., 203 F.3d 596 (9th Cir. 2000), same reporter:
    <https://law.resource.org/pub/us/case/reporter/F3/203/203.F3d.596.99-15852.html>
- **Not reached:** Justia, OpenJurist, Casetext and CourtListener copies of both cases (403 or 410), the Ninth Circuit's
  own PDF of Connectix (404). No court-hosted copy was read; the reporter copies above are the source. SEGA's general
  terms (`www.sega.com/terms-use`, `/eula`: HTTP 429), `games.sega.com/EULA` (404) and `atlus.com/terms-of-use/` (404):
  no separate SEGA or Atlus terms beyond the Steam EULA were read, and none was found by search.
- **Saved:** 2026-11-17.
- **Game build:** none; the texts aren't tied to a build. The EULA is the one the Steam store links for the release the
  project's notes pin to executable SHA-256 `548ddc95…`.

This is a record of what the texts say, not legal advice. Whether a clause binds a given player, and how the statutes
apply in their country, is a lawyer's call: [NEEDS VERIFICATION].

## Game EULA, section 7

"You SHALL NOT: [...] (e) reverse engineer, derive source code, modify, decompile, disassemble, copy, or create
derivative works of the Product, in whole or in part, except where permitted by law;"

The Denuvo clause (section 18), section 16 (statutory rights unaffected), the Modding Terms and the governing law are
quoted in `eula-and-mod-stance-2026-11-16.md`.

## Steam Subscriber Agreement, section 2.G

"Except as otherwise permitted under this Agreement (including any Subscription Terms or Rules of Use), or under
applicable law notwithstanding these restrictions, you may not, in whole or in part, [...] reverse engineer, derive
source code from, modify, disassemble, decompile, create derivative works based on, [...] the Content and Services or
any software accessed via Steam without the prior consent, in writing, of Valve."

## EU: Directive 2009/24/EC

**Article 5(3):** "The person having a right to use a copy of a computer program shall be entitled, without the
authorisation of the rightholder, to observe, study or test the functioning of the program in order to determine the
ideas and principles which underlie any element of the program if he does so while performing any of the acts of
loading, displaying, running, transmitting or storing the program which he is entitled to do."

**Article 6(1):** "The authorisation of the rightholder shall not be required where reproduction of the code and
translation of its form [...] are indispensable to obtain the information necessary to achieve the interoperability of
an independently created computer program with other programs", provided "(a) those acts are performed by the licensee
or by another person having a right to use a copy of a program [...]; (b) the information necessary to achieve
interoperability has not previously been readily available [...]; and (c) those acts are confined to the parts of the
original program which are necessary in order to achieve interoperability."

**Article 6(2):** the information may not be "(a) [...] used for goals other than to achieve the interoperability of the
independently created computer program; (b) [...] given to others, except when necessary for the interoperability of
the independently created computer program; or (c) [...] used for the development, production or marketing of a
computer program substantially similar in its expression, or for any other act which infringes copyright."

**Article 6(3):** the Article may not be applied in a way that "unreasonably prejudices the rightholder's legitimate
interests or conflicts with a normal exploitation of the computer program."

**Article 8:** "Any contractual provisions contrary to Article 6 or to the exceptions provided for in Article 5(2) and
(3) shall be null and void."

How each Member State transposed these, and whether they reach a mod that adds output to a running game, wasn't read:
[NEEDS VERIFICATION].

## US: 17 U.S.C. § 1201(f)

**(f)(1):** "a person who has lawfully obtained the right to use a copy of a computer program may circumvent a
technological measure that effectively controls access to a particular portion of that program for the sole purpose of
identifying and analyzing those elements of the program that are necessary to achieve interoperability of an
independently created computer program with other programs, and that have not previously been readily available to the
person [...], to the extent any such acts of identification and analysis do not constitute infringement under this
title."

**(f)(3):** the information "may be made available to others if [...] solely for the purpose of enabling
interoperability of an independently created computer program with other programs, and to the extent that doing so does
not constitute infringement under this title or violate applicable law other than this section."

**(f)(4):** "the term “interoperability” means the ability of computer programs to exchange information, and of such
programs mutually to use the information which has been exchanged."

Section 1201 is an anti-circumvention exception. It doesn't address contract terms, and the project circumvents nothing
(ground rule 5).

## US case law

**Sega v. Accolade**, 977 F.2d 1510: "We conclude that where disassembly is the only way to gain access to the ideas
and functional elements embodied in a copyrighted computer program and where there is a legitimate reason for seeking
such access, disassembly is a fair use of the copyrighted work, as a matter of law."

**Sony v. Connectix**, 203 F.3d 596: "The intermediate copies made and used by Connectix during the course of its
reverse engineering of the Sony BIOS were protected fair use, necessary to permit Connectix to make its non-infringing
Virtual Game Station function with PlayStation games."

Both are copyright holdings. Neither decides whether a licence clause forbidding reverse engineering is enforceable as a
contract; no US case on that question was read: [NEEDS VERIFICATION].
