# The second age, The River: design brief

Written by the lead on 25 November 2026. It builds on the first age and on `docs/plans/incremental-design.md`. Where this brief is silent, choose the simplest thing that fits and write the choice down. Our own design: nothing is taken from another game.

## In one paragraph

The charter gives the town the right to trade on the river. Ringing in the next age keeps the whole town as it is and opens the riverside: a landing where boats moor and buy goods, a tide that decides when boats can come and when the ford can be walked, a far bank with sheep and a weaver's trade, and crossings that arrive one after another: the ford, the ferry, the footbridge, and at last the drawbridge that takes a toll from every boat. The age ends with the borough seal.

## What is new

- **The landing.** The first gift of the age: a quay on the near bank with a road to the town. Boats come up the river, moor, and each asks for a cargo of one good. Carrying to the landing is a new, better-paid way to sell Grain and Flour (and later Cloth). Every unit is paid on delivery; a boat that is filled pays a bonus and leaves; a boat that has waited too long leaves with what it has. One berth at first; boats wait on the river for a free berth.
- **The tide.** A slow, regular cycle of a few minutes. At high water boats can arrive; at low water none come, but the ford can be walked. The player sees it in the river and on a small tide dial, with the time to the turn. It gives the age a rhythm: the landing is busy at high water (a good moment for the bell), the far bank is easy to reach at low water.
- **The far bank and two new goods.** Wool from a pasture on the far bank; Cloth made from Wool at a weaver's cottage on the near bank (the same pattern as Grain, the mill and Flour). Cloth is the dearest cargo.
- **Crossings**, each one a clear step up from the last:
  1. The ford: free, slow, and only at low water. People queue on the bank while the water is high.
  2. The ferry (a building offer): crosses at any tide but carries only a few at a time, so there is a visible queue at both banks. Upgrades: a bigger raft, a faster rope.
  3. The footbridge (a gift): always open and quick for walkers, but boats cannot pass it, so the landing must be below it or the boats stop at it (the planner decides which and keeps it consistent).
  4. The drawbridge (the last gift before the seal): walkers cross freely; it lifts for every boat, walkers wait those few seconds, and every boat that passes pays a toll. Upgrades: the toll, a faster winch.
- **New upgrades**: a second berth; a crane (boats take bigger cargoes); better prices from boats; the value of Wool and of Cloth; the ferry's raft and rope; the toll and the winch. Tools, Pace, Load, paving, extending cottages and the bell continue from the first age with further steps on sale.
- **Levels.** About 12 more, each giving one clear thing: the landing; a bank road; more cottages; the ford and a lane on the far bank; the pasture; the weaver; the ferry offer; the second berth offer; cottages on the far bank; the footbridge; the drawbridge; the borough seal, which ends the age.
- **Requests** go on, now also from skippers ("The barge Marit wants 30 Flour before the tide turns").
- **Achievements**: about 16 more.

## Rules that must hold

- Nothing of the first age is lost at the change: villagers, upgrades, coin, paving, achievements. A town saved in the first age loads and can ring in the second.
- The first age's tests keep passing unchanged; the second age has its own.
- Deterministic, allocation-free steps, saved and loaded exactly, as in the first age.
- Boats are few and readable (two or three on screen), not a traffic jam.
- Targets for the bots, as tests: the second age takes 20 to 35 minutes for the three buy-when-you-can players; at least 2 purchases a minute; never more than 60 seconds with nothing affordable; something new on the map at least every 3.5 minutes; every kind of new thing is bought by the saving bot; each crossing, once it exists, carries more people a minute than the one before it; the landing earns at least a third of all coin by the middle of the age.

## Assets that already exist

`landing.glb`, `rowboat.glb`, `barge.glb`, `ferry-raft.glb`, `ferry-post.glb`, `ferry-rope.glb`, `ford-stones.glb`, `footbridge-end.glb`, `footbridge-span.glb`, `drawbridge-end.glb`, `drawbridge-lift.glb`, `drawbridge-span.glb`, `keeper-hut.glb`, `reeds.glb`, `tree-willow.glb`, `villager-cargo-wool.glb`, `villager-cargo-cloth.glb`, and the river sounds (`oar-splash`, `boat-call`, `ferry-rope`, `drawbridge-raise`, `drawbridge-lower`, `water-ford`). A pasture, sheep and a weaver's cottage do not exist yet and are made in code with ledger rows.
