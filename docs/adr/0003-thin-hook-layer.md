# Logic lives in plain libraries; only a thin hook layer touches the game

Format logic and accessibility-feature logic live in plain `net9.0` libraries that reference neither Reloaded-II nor
anything Windows-only. One thin `net9.0-windows` mod project wires them into the game, and it is the only project
allowed to use unsafe code or touch raw memory. Features depend on interfaces, never on each other. Architecture tests
fail the build when any of these rules is broken.

We chose this over a single project, the template's default and the layout the community mods mostly use, because it
lets most bugs be caught in seconds by unit tests with fake game state and no game running. It also keeps the riskiest
code, pointers into the game's memory, in one small place for security review. The cost is more projects and an
interface at each seam.
