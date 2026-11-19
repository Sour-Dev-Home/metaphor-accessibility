# metaphor-accessibility

Mods, tools and reverse-engineering notes for Metaphor: ReFantazio on PC.

> **Disclaimer:** this is a non-commercial, fan-made accessibility tool for blind and visually impaired players. It is
> not affiliated with, endorsed by or sponsored by ATLUS or SEGA.

> **Support:** if you'd like to support my time on this project, you can tip on [Ko-fi](https://ko-fi.com/soure).
> Tipping is entirely optional and gets you nothing extra: no early builds, no extra features, no priority requests.
> The mod is and stays free for everyone.

Status: early development, no release yet. Testers can try dev builds that read the game's dialogue window aloud through
a screen reader, with a spoken installer and a setting for voiced lines; menu reading is in progress. [NEEDS
VERIFICATION: what works in the game today.] Start with [the player docs](docs/players/README.md).

## What's here, and what isn't

- **Here:** our own code, and notes on the game's formats and engine, each pinned to the game build it was checked on.
- **Not here:** anything from the game itself (executables, archives, assets, decompiled code). You need your own copy of
  the game.

This project is single-player modding only. It doesn't touch online features, DRM or anti-tamper protection.

Metaphor: ReFantazio and related names are trademarks of their respective owners, used here only to say which game
this tool works with.

To contribute, start with [CONTRIBUTING.md](.github/CONTRIBUTING.md). The checks, supply chain and branch rules are in
the [contributor docs](docs/contributors/README.md).

## License

Code is under [GPL-3.0](LICENSE). Docs (the Markdown files, including everything in `docs/`) are under
[CC BY 4.0](docs/LICENSE), except `docs/raw-sources/`, which holds copies of other people's work under their own terms.

- [CREDITS.md](CREDITS.md) lists the projects whose code, findings or ideas this one uses.
- `src/Metaphor.Hooks/SquirrelRegistrationListener.cs` follows a technique from MF.Toolkit (AGPL-3.0), credited in the
  file. It's a development-only hook that never ships in a release.
- The mod package ships third-party libraries under their own licences, each in `licenses/` with where its source is
  ([ADR 0005](docs/adr/0005-prism-speech-backend.md), [ADR 0006](docs/adr/0006-hooks-through-reloaded-shared-hooks.md)).
