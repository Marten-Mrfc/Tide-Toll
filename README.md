# Tide & Toll

A town-builder where you never draw a road. Villagers wear their own paths into the grass, boats compete with them for the river, and every era inherits the streets of the one before. The town is a living map drawn on the pages of an open book.

## What is in this repository

| Folder | What it holds |
| --- | --- |
| `sim/` | `TideToll.Sim`: the engine-free C# simulation library (seeded, deterministic, no Godot) |
| `sim.tests/` | xUnit tests for the simulation, run without the game |
| `game/` | the Godot 4.7 .NET project (rendering, camera, UI, audio) |
| `prototypes/desire-lines/` | Prototype 1: a browser toy that tests whether steering worn paths is fun |
| `assets/` | art and sound, each file listed in `assets/LEDGER.csv` with its source and licence |
| `tools/` | setup, the one-command check, ledger check, asset sync and Windows export |
| `docs/` | short notes: asset pipeline, Windows setup, CI template, the overnight log |

## What the game does now

The game is an open book with two chapters, and opens on the first. The right-hand page is a living map of the town; the left is the chapter page (how to play, the village facts, path health with its bands, the legend, a status line); paper bookmarks out of the map page hold the tools and controls, each with a key. Turn the page with the brick-red chapter ribbon at the top of the bookmarks or the C key: a leaf turns over the book and the other chapter opens on a fresh town from the same seed. Run it with `godot --path game` (`-- --chapter 2` to open on the river), or the Windows build (see below).

- **Chapter I, "The Hamlet"** is Prototype 1 as the finished browser toy plays it: the same town as the toy for the same seed, villagers wearing trails into the page, and the player steering them with fences, gates (at any angle), gravel, the footbridge and the eraser, with a flow preview of the routes that would change ("Reroutes about 35 walkers a day (+47% longer)"). Rain turns worn trails to mud for a day or two, and new families build cottages beside busy trails. No boats, no coin.
- **Chapter II, "The River"** is the river prototype: the same path tools (free), plus rowboats and masted barges that come up the river to the landing to buy the grain villagers carry from the field. The player starts with a small purse and buys crossings from the bookmarks, each with its price and what the purse can do ("40c · can buy", "200c · save 150c"): a low footbridge (quickest for walkers, turns barges back), a ferry (every boat passes, walkers wait for the raft) or a drawbridge, raised by hand until the town has learnt the boats (ten raises) and a keeper can be hired. The free ford costs trade: grain carried through it gets wet (the sack is drawn wet over the carrier's head) and sells for less ("wet grain: -3c" at the landing, the ledger's "The ford" line). A waiting boat shows its patience running down, a boat that gives up turns back, coins float up by the landing, and the chapter page keeps a ledger and margin notes. The seasons reach the river: a spring flood over the band beside it (its reach marked by a faint dotted line from the first day) closes the ford, and winter ice lets villagers cross anywhere.

Sound: the river, footsteps by the crowd on screen, rain, a bell at dawn, tool knocks, the boats and bridges, and the theme now and then.

## How to run the checks

On Ubuntu 24.04, install the toolchain once (needs root; about 2 GB with export templates):

```
sudo tools/setup-linux.sh
```

Then, from the repository root:

```
./check                 # sim build + tests, Godot build, headless smoke, tools, river, interface and perf tests,
                        # screenshots at 1280x720 and 1920x1080, ledger
./check --export        # also build the Windows x86_64 export into build/windows/ and check the pack holds every asset
./check --gate          # also the long statistical runs (gate tables, toy comparison, ~6 min)
./check --bench         # also the simulation tick benchmark
./check --with-toy      # also run the browser toy's own checks
./check --no-screenshot # skip the rendered screenshots (no xvfb)
```

It prints a summary table and exits non-zero if anything fails. Logs go to `build/check-logs/`, screenshots to `build/screenshots/` (`book`, `map`, `wear`, `preview` and the river's `river-*` views at both window sizes, plus close-ups).

| Step | What it proves |
| --- | --- |
| `sim-build`, `sim-test` | the simulation builds and its fast tests pass (parity with the toy, no allocation per tick) |
| `game-build`, `game-smoke` | the game builds; the scene is complete, the frame loop ticks, picking, camera limits, edits rebuild |
| `game-tools` | fence, footbridge and erase through real mouse events; nothing placed through the toolbar; the flow preview never changes a run (same wear hash with and without it) |
| `game-river` | the river through real input: ferry and drawbridge placed (and refused off the river or for coin, with the reason), the drawbridge raised and lowered by click, L, its badge's lever and Ctrl+1, the keeper hired, both erased; four in-game days of boats fill the chronicle, the ledger and the coins; the bridge badge clear of its bridge and the place tags at 720p and 1080p; a flood and a freeze driven through the simulation's hooks, with their marks, tags, notes, ledger lines, tool reason and sound coming and going |
| `game-river-rules` | the river's third-pass rules as the player sees them: the starting purse on the price tabs, the flood band's dotted edge, the Ford tag's hint and its giving way to boats, wet sacks and the "wet grain" note, the ledger's ford line and its "today" right after a fast-forward, the keeper earned by ten hand raises and hired, queued barges never overlapping, merged turn-back notes |
| `game-chapter`, `game-hamlet` | the two chapters and the page turn through real input; Chapter I's growing town (a cottage arriving) and its rain, mud and drying |
| `game-ui` | every control exists, has a tooltip and label, and toggles by click; every shortcut; keyboard focus; text sizes at 720p and 1080p |
| `game-perf` | with the river on (a ferry, a drawbridge and its keeper), CPU per frame with 500 walkers under 6 ms (1x) and 10 ms (8x) mean, under 64 bytes allocated per frame, also at 8x in a flood and on the ice; the preview's frames under 25 ms (bounded preview slices); the stand-in for "60 fps with 500 walkers" until a real graphics card measures it |
| `screenshot`, `screenshot-1080` | rendered views with the interface, the wear overlay and a fence drag with the preview, and the river (a barge waiting at the lowered drawbridge, the bridge raised, the ferry, a barge held and turning back at a footbridge, all crossings, a rowboat moored close, the play view, the ledger, a flood, the frozen river with its ice paths, and with the rules' own purse the first morning, wet sacks at the ford, the wet-grain note and the keeper locked and earned), at both sizes; `screenshot-hamlet`, `screenshot-hamlet-1080`: Chapter I (day 1, the page turn, gates and gravel, day 30 with grown houses, rain and mud) |
| `ledger` | every asset has a ledger row |

On Windows, the quickest way to play is the build GitHub makes on every push: open the [Windows build runs](https://github.com/Marten-Mrfc/Tide-Toll/actions/workflows/windows-build.yml), pick the newest green one and download the `TideToll-windows-x64` artifact. See [docs/setup-windows.md](docs/setup-windows.md) for that, for opening the game in Godot instead, and for what to check on a real PC. Adding art or sound: [docs/asset-pipeline.md](docs/asset-pipeline.md).
