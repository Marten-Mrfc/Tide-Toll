Written by the lead at about 05:55 Amsterdam time on 26 November 2026.

**Start here:** the review page with 49 numbered screenshots, each with a yes-or-no question: [Tide & Toll Night Review](https://claude.ai/artifact/EhiZ8zXKSgpu1umUkZ2dd3). The newest Windows build: [GitHub run 45757608394](https://github.com/Marten-Mrfc/Tide-Toll/actions/runs/45757608394), artifact `TideToll-windows-x64` (commit `9864b79`). The night's log, round by round: `docs/incremental-log.md` in the repository.

#### What exists now

| Part | State |
| --- | --- |
| Core loop | One cottage, one villager, one trail. Every trip earns coin, coin buys upgrades, upgrades add renown, each level hands over the next piece of town. Roads are given, wear in visibly and can be paved by clicking them |
| First age, The Hamlet | 16 levels, about 33 minutes by a whole-age play through the interface. Villagers, pace, load, tools, the value of each good, cottages extended by clicking, the bell, building offers, four choices of two boons, requests, the charter drive, small touches, 24 achievements |
| Second age, The River | 12 levels to the borough seal: a landing where boats buy goods, a tide, wool and cloth, ford, ferry, footbridge, drawbridge with a toll, 16 achievements. Played by bots (22 to 34 minutes) and by the screenshot tour only |
| The book | Zoomed in most of the time, with a tilted view; one continuous zoom out to the page and the open book with the town's summary; Esc closes the book and the cover is the pause menu |
| Settings | Pages of the book: Sound, Display, Keys (rebinding with clash handling), Saving. The game autosaves and continues |
| Music and sound | Five new pieces beside your theme (three for the rotation, one for the age change, one for the paused book) and about 70 new sound effects, all made in code, none heard by anyone |
| Checks | One command, 18 steps: 258 simulation tests, nine game test scenes driven by real input (a whole first age among them), screenshots, the asset ledger, the Windows export. GitHub builds the exe for every finished round |

#### How it plays, honestly

- First age by real input only: 7.0, 6.5 and 4.1 purchases a minute in minutes 0 to 10, 10 to 20 and 20 to 30; 3.8, 3.2 and 1.7 decisions a minute by a simple count of moments with two or more kinds of thing to buy; the charter at about minute 33 with 201 purchases.
- The first twenty minutes are the best part. Minutes 20 to 30 are thin. Apart from the four boons everyone ends up buying the same things.
- The second age has not been played through with real input by anyone. Its pacing holds on the ten towns the tests use and is thin elsewhere.
- Round 1 was a tap-fest (80 percent of purchases were one cheap speed upgrade); rules were rewritten three times in the night to get here.

#### What is not right yet

- Faults a reviewer sees in the final pictures are listed on the review page under "What still looks wrong".
- Villagers housed by the river still walk home to a first-age cottage.
- The ford matters on about half of the towns.
- Godot crashed twice during screenshot runs on the cloud machine's software renderer; cause unknown.
- Nothing has run on Windows or on a real graphics card.

#### Needs you

1. Play it on a real Windows machine and say whether it runs, how it sounds and whether it crashes.
2. Listen to the six music pieces (players are on the review page).
3. Answer the yes-or-no questions on the review page and say continue.
4. If you want free assets searched before making our own: the asset sites are blocked from this machine and need allowing.

#### Decisions made for you

Thirty, each with its reason, on the review page and in the log. The ones you are most likely to want to overrule: the tilted view when zoomed in; choices of two boons; the cheap "Regulars" purchase; small touches that appear by themselves; the tide and the order of crossings in the second age; the bell as the one active input.
