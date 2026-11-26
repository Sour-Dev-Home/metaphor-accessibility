# Unseen Desire 0.1.8, a screen-reader mod for Persona 5 Royal: release notes and what we can take

- **Source:** the release announcement for Unseen Desire 0.1.8 by its author, Haru (heavenay.com), posted on Discord
  and relayed by the maintainer. Read as data. Only the announcement text was read: no zip, no code, nothing run or
  decompiled. The post's date is not recorded; it was read on 2026-11-25.
- **Saved:** 2026-11-25
- **Game build:** none. It targets Persona 5 Royal (Steam), not Metaphor: ReFantazio. Only methods and design carry
  over.

Follows `unseen-desire-0.1.7-2026-11-17.md`. Licence and source status as recorded there (GPL-3.0 per its docs, source
on request). Ideas used from it are credited by name in `CREDITS.md`.

## 1. The release notes, verbatim

### New

The map cursor, G (a controller: hold RT and click the right stick), is now a map you walk with north always up: I, J,
K and L move it a meter north, west, south or east, whatever the camera does, and you hear everything from where the
cursor is, as if you stood there. Walls and closed doors stop it, and you hear what it reaches. The scanner works inside
it, with a Player row for you: P plays a beacon along the way from the cursor, P twice takes the cursor there, Shift and
P guide you to the cursor, Backspace walks you there, and M leaves one bookmark. The old map cursor and the map cursor
on the minimap are gone; the game's map, M, stays as it was.

A screen's keys: F2 (a controller: hold RT and click the left stick) says the keys of the screen you are on, as the
game shows them at the bottom, with your own keys, then the mod's keys there. Every menu also says them when it opens,
after its description; General, "Say a screen's keys when it opens", turns that off. In a dialogue, F2 says Auto and
Log.

Tutorials: many of the game's own tutorials now have a page or two more after the game's, telling what you'll hear
there. The Map Cursor tutorial is new, for the new cursor.

Targeting a Shadow: press P twice on a Shadow in the Shadows list (a controller: LT and Y twice). The beacon leads you
to its back, the camera turns along the way, and you hear when it turns, when it is suspicious, when it spots you, and
Behind it when a strike would be an ambush.

O selects the nearest Shadow with its beacon; O twice targets it (a controller: LT and RB).

The Shadows list says when a Shadow is suspicious or chasing you, and its distances are now the walking distance.

When the game gives you only a few seconds, the last five tick, higher each one. Sounds, "Countdown ticks", sets them.

The mod menu has its own sounds: a page turning as you go into a section and back, and a flick on a choice.

A Persona's affinities are read in sentences, like "Weak to fire and ice. Resistant to electric. Null to gun."

Baton Pass: a member's first row says Weak when your pick hits the target's weakness.

### Fixes

The fifth Palace: the places where players got stuck can now be walked through, and every way on is listed.

The second Palace: a part of a room that had moved was still taken for a wall, and the mod said there was no way
through.

Auto walk to some things you examine now ends where the game offers them; before, it could stop a couple of meters
short.

The first press of the scanner in an area no longer lags.

Dialogue: "Auto" is said only when you press the Auto key yourself.

## 2. What we can take

Design ideas only. How any of it is built cannot be seen (no source).

- **Map cursor (#877):** north always up whatever the camera does, one-meter steps, the listener moved to the cursor,
  walls and closed doors stop it, a beacon or auto walk from the player to the cursor, one bookmark. They dropped their
  separate map-screen and minimap cursors for this one.
- **Auto walk (#184):** stop where the game offers the interaction, not at a fixed distance.
- **Field enemies:** a beacon to an enemy's back; cues for suspicious, spotted and "behind it" (an ambush strike);
  walking distance, not straight-line. Compare `docs/findings/field-enemy-sight-zones-static.md`.
- **A screen's keys:** one key says the game's on-screen prompts with the player's own bindings, then the mod's keys;
  menus say them on open, with a setting to turn that off.
- **Tutorials:** extra pages after the game's, saying what the player will hear there.
- **Countdown ticks:** the last five seconds of a timed prompt tick, rising each time, with a setting.
- **Mod menu sounds:** a sound for going into and out of a section, another for a choice.
- **Affinities as sentences**, grouped by kind. Compare #854 ("just the number").
- **Lessons from their fixes:** precomputed navigation data went stale where geometry moves; warm the scanner's cache
  when an area loads; announce a state change only when the player caused it.
