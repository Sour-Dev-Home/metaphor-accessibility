# Fit the objective markers

Turn on "guide me to the objective" (`O`, #652) for a field by fitting its map-to-world conversion from a marker walk,
or add a hand-written waypoint for an objective with no marker. No code changes: two JSON files under
`data/objectives/`, both empty until someone fits them. Why the conversion is needed is in
[the icon-points finding](../../findings/objective-map-icon-points-static.md).

## 1. Walk the markers

1. Deploy a Debug build and start a session ([run a game session](run-a-game-session.md)); it turns the testing
   setting `objectiveGuidance` on.
2. Load a save on a step whose objective has a marker on the district you're in (the finding's chapter 1 table: rows
   2, 9, 11, 13, 14 or 20 in the royal capital).
3. Open the field map, note where the objective marker is, close it, walk onto the marker and press `L`. The message
   log gets a line like:

   ```text
   objective place map=1 field=f_0001_001 player=(-2269.4,24,8609.8) row=11 flag=0x1177 conversion=False waypoints=0 variant1=False points=(1045,295 k=4 i=11) (1478,1025 k=4 i=9) ...
   ```

   `points=loading` means the field's markers were still being read: press `L` again. Press `O` too: its
   `objective key` line is the same record.
4. Do the same on a second marker of the same field, as far from the first as you can, then a third to check. Each
   district (`f_0001_001`, `_002`, `_003`) is its own field and needs its own two stops.
5. Take a screenshot of the field map at each stop, and note which marker you stood on: its `(x,y k=4 i=<row>)` in
   the line.

## 2. Fit the conversion

For a field, with the markers' `(x, y)` and your `player` `x` and `z` at two stops:

- `scaleX = (worldX2 - worldX1) / (x2 - x1)` and `originX = worldX1 - scaleX × x1`;
- `scaleZ = (worldZ2 - worldZ1) / (y2 - y1)` and `originZ = worldZ1 - scaleZ × y1`.

Check the third stop lands within a few metres (about 100 units). If it doesn't, the map is turned or its axes swap,
which this file can't say: report it, and the conversion gets a new `IMapToWorld` (`src/Metaphor.Features.Field`).
Add the field to `data/objectives/map-to-world.json`:

```json
{
  "maps": [
    { "field": "f_0001_001", "originX": -9000, "originZ": 2000, "scaleX": 8.5, "scaleZ": 8.5 }
  ]
}
```

The numbers above are made up. A scale may be negative; it can't be 0.

## 3. Add a waypoint where there is no marker

For a row with no marker on a field (the finding lists chapter 1's), stand where the player should go, press `L`, and
add the line's `field`, `row` and `player` `x` and `z` (not `y`) to `data/objectives/waypoints.json`:

```json
{
  "waypoints": [
    { "field": "f_0001_001", "row": 1, "x": -2269.4, "z": 8609.8 }
  ]
}
```

`O` guides to the nearest marker or waypoint for the row. The field tests check both files' schema
(`ObjectiveDataTests`), and the dev log names a file the mod left out.
