# Fit the objective markers

"Guide me to the objective" (`O`, #652) places each marker from the map's own minimap record, read from the player's
`base.cpk`; no fit is needed. This page covers the one live check, an override for a field whose record turns out
wrong, and a hand-written waypoint for an objective with no marker. The overrides and waypoints are two JSON files under
`data/objectives/`, both empty. The conversion, `x = Ox − s × px`, `z = Oz − s × py`, is in
[the minimap finding](../../findings/minimap-data-converts-map-points-to-world.md); the markers are in
[the icon-points finding](../../findings/objective-map-icon-points-static.md).

## 1. Confirm a marker live

1. Deploy a Debug build and start a session ([run a game session](run-a-game-session.md)); it turns the testing
   setting `objectiveGuidance` on.
2. Load a save on map 1 (`f_0001_001`) whose current objective is row 11, the shared exit marker at point
   `(1045, 295)`. Its record (`18807, 21899, 18`) puts it at world `(−3, 16589)`.
3. Walk onto the marker and press `L`. The message log gets a line like:

   ```text
   objective place map=1 field=f_0001_001 player=(-3,24,16589) row=11 flag=0x1177 conversion=minimap:1(18807,21899,18) waypoints=0 variant1=False points=(1045,295 k=4 i=11) ... world=(1045,295)->(-3,16589)r=4
   ```

   `world=` gives each of the row's markers as `(point)->(world x, z)` and `r=`, its distance to the nearest walkable
   cell (`?` while the walk graph loads, `far` past 32 cells). `conversion=` names the record, `override` for a field in
   `map-to-world.json`, `none` for a map with no record, `loading` while the tables are read. Press `O` too: its
   `objective key` line is the same record.
4. It passes when `player`'s `x` and `z` are within a few metres (about 100 units) of the marker's world point. A map
   whose floors have different records says `floors=<n>differ` after the record: the floor is picked by the player's
   height `[NEEDS VERIFICATION]`, so note the `player` `y` on those (maps 14, 15 and 114).

## 2. Override a field that's wrong

When a field's markers land off by more than a few metres, fit it by hand from two stops on markers of that field, as
far apart as you can, with the markers' `(x, y)` and your `player` `x` and `z`:

- `scaleX = (worldX2 - worldX1) / (x2 - x1)` and `originX = worldX1 - scaleX × x1`;
- `scaleZ = (worldZ2 - worldZ1) / (y2 - y1)` and `originZ = worldZ1 - scaleZ × y1`.

Check a third stop lands within about 100 units, then add the field to `data/objectives/map-to-world.json`, which wins
over the record:

```json
{
  "maps": [
    { "field": "f_0001_001", "originX": 18807, "originZ": 21899, "scaleX": -18, "scaleZ": -18 }
  ]
}
```

That row is map 1's record in this file's terms (`world = origin + scale × point`). A scale can't be 0. If the third
stop is still off, the map is turned or its axes swap, which this file can't say: report it.

## 3. Add a waypoint where there is no marker

For a row with no marker on a field (the icon-points finding lists chapter 1's), stand where the player should go,
press `L`, and add the line's `field`, `row` and `player` `x` and `z` (not `y`) to `data/objectives/waypoints.json`:

```json
{
  "waypoints": [
    { "field": "f_0001_001", "row": 1, "x": -2269.4, "z": 8609.8 }
  ]
}
```

`O` guides to the nearest marker or waypoint for the row. The field tests check both files' schema
(`ObjectiveDataTests`), and the dev log names a file the mod left out.
