# n10: the drawing measured here (software Vulkan under xvfb)

How: `tools/perf-matrix.sh` runs `game/tests/perf-draw.tscn` under xvfb with the Mobile renderer on Mesa's llvmpipe
(the CPU plays the graphics card), the heavy-run lock held per group. The town: the bot's end-of-age town (seed 1,
28 villagers) or the sealed second-age town (34 villagers), adopted and held still, at the follow view; per variant
10 warm-up frames, then 30 frames timed by real time; draws, objects and primitives from the rendering server.
**The milliseconds mean nothing by themselves** (a frame here is half a second); the ratios within a group and the
counts are the result. Rows in one group run in one process (the two age-1 1920 x 1080 baselines are two groups).

## Before the wins (Part B)

| town | variant | window | mean ms | p95 ms | vs its baseline | draws | objects | primitives |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| age1 | baseline | 1920x1080 | 644 | 683 | 1.00 | 360 | 807 | 228,514 |
| age1 | noink | 1920x1080 | 650 | 912 | 1.01 | 354 | 780 | 228,082 |
| age1 | nosoft | 1920x1080 | 667 | 877 | 1.04 | 356 | 782 | 228,144 |
| age1 | noshadow | 1920x1080 | 533 | 572 | 0.83 | 255 | 621 | 119,188 |
| age1 | baseline | 1920x1080 | 618 | 656 | 1.00 | 360 | 807 | 228,514 |
| age1 | novillagers | 1920x1080 | 622 | 674 | 1.01 | 337 | 763 | 190,930 |
| age1 | flatground | 1920x1080 | 190 | 201 | 0.31 | 355 | 721 | 227,918 |
| age1 | zoomout | 1920x1080 | 408 | 431 | 0.66 | 389 | 394 | 229,090 |
| age1 | closest | 1920x1080 | 600 | 658 | 0.97 | 327 | 693 | 224,262 |
| age1 | scale50 | 1920x1080 | 227 | 251 | 0.37 | 350 | 716 | 227,876 |
| age1 | baseline | 1280x720 | 324 | 346 | 1.00 | 360 | 807 | 228,514 |
| age1 | baseline | 2560x1440 | 1033 | 1132 | 1.00 | 360 | 807 | 228,514 |
| river | baseline | 1920x1080 | 673 | 709 | 1.00 | 538 | 1366 | 289,238 |
| river | noink | 1920x1080 | 617 | 659 | 0.92 | 536 | 1338 | 289,418 |
| river | noshadow | 1920x1080 | 545 | 598 | 0.81 | 359 | 1183 | 158,301 |
| river | flatground | 1920x1080 | 207 | 223 | 0.31 | 538 | 1340 | 289,422 |

Trees hidden: the first try failed (the node is renamed when the trees are planted again for an adopted town); measured in the after set below.

**What it says.**
1. **The ground shader dominates**: with the ground's material swapped for a flat one the frame takes 0.31 of the time
   (both ages); the shadow pass is the second cost (0.81 to 0.83 without shadows), and the ink pass is within the noise
   (1.01 in the first age, 0.92 in the second; "only the soft ink off" 1.04, so the stencil split shows nothing here).
2. **The cost follows the pixels**: 1280 x 720 (0.44 of the pixels) takes 0.51 of the 1080p time, 2560 x 1440 (1.78)
   takes 1.64, the 3D scale at 50 percent (0.25 of the pixels) takes 0.37, and the whole book in view (less of the
   screen is the heavy ground) 0.66.
3. **The number of draws does not matter** at this size: 360 draws and 229 k primitives in the first age, 538 and
   289 k in the second; hiding the villagers (23 draws, 38 k primitives fewer) changes nothing (1.01), and the second
   age's 50 percent more draws cost 5 to 9 percent more.

Caution: llvmpipe does texture filtering in software, which makes texture reads dearer relative to arithmetic than on
a real card, so the ground's share here is an upper bound. The order (ground, then shadows, then ink) and the
pixel-bound shape are what to carry over; the owner's readout (GPU ms at their window size) is the real number.

## The simulation and the collector

- `ticks --preset river --walkers 40` and `--walkers 100` (20 days, the desire-line world the tool profiles, not the
  town game's `Town.Step`): mean 0.723 ms and 0.745 ms a tick, p99 3.08 and 3.54 ms, worst 8.54 and 9.18 ms (the first
  tick; afterwards 5.7 ms). The ticks with two route searches cost about 2.5 ms; walkers barely change the mean.
- The town game's own step: 0.02 ms a tick at 28 villagers (`game-perf`'s bot run), 0.01 ms a frame in the readout.
- The .NET collector over 60 s of the end-of-age town at 1x (headless, uncapped, 8,696 frames, `perf-draw -- --gc=60`):
  **0 collections** of generation 0, 1 and 2; 4,020 KB allocated (473 bytes a frame); heap 13 MB. No autosave fell
  in the minute (every 2 minutes).

## After the wins (Part C)

| town | variant | window | before: mean ms | p95 | vs baseline | after: mean ms | p95 | vs baseline | draws before / after | primitives before / after |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| age1 | baseline | 1920x1080 | 644 | 683 | 1.00 | 638 | 704 | 1.00 | 360 / 360 | 228,514 / 228,514 |
| age1 | noink | 1920x1080 | 650 | 912 | 1.01 | 567 | 603 | 0.89 | 354 / 354 | 228,082 / 228,082 |
| age1 | nosoft | 1920x1080 | 667 | 877 | 1.04 | 606 | 653 | 0.95 | 356 / 354 | 228,144 / 227,916 |
| age1 | noshadow | 1920x1080 | 533 | 572 | 0.83 | 488 | 524 | 0.77 | 255 / 255 | 119,188 / 119,188 |
| age1 | baseline | 1920x1080 | 618 | 656 | 1.00 | 579 | 654 | 1.00 | 360 / 386 | 228,514 / 235,200 |
| age1 | novillagers | 1920x1080 | 622 | 674 | 1.01 | 558 | 620 | 0.96 | 337 / 360 | 190,930 / 192,808 |
| age1 | flatground | 1920x1080 | 190 | 201 | 0.31 | 196 | 220 | 0.34 | 355 / 381 | 227,918 / 234,946 |
| age1 | zoomout | 1920x1080 | 408 | 431 | 0.66 | 380 | 400 | 0.66 | 389 / 389 | 229,090 / 233,826 |
| age1 | closest | 1920x1080 | 600 | 658 | 0.97 | 592 | 636 | 1.02 | 327 / 350 | 224,262 / 230,920 |
| age1 | scale50 | 1920x1080 | 227 | 251 | 0.37 | 218 | 244 | 0.38 | 350 / 373 | 227,876 / 234,534 |
| age1 | baseline | 1280x720 | 324 | 346 | 1.00 | 313 | 338 | 1.00 | 360 / 386 | 228,514 / 235,200 |
| age1 | baseline | 2560x1440 | 1033 | 1132 | 1.00 | 1008 | 1084 | 1.00 | 360 / 386 | 228,514 / 235,200 |
| river | baseline | 1920x1080 | 673 | 709 | 1.00 | 566 | 611 | 1.00 | 538 / 580 | 289,238 / 297,323 |
| river | noink | 1920x1080 | 617 | 659 | 0.92 | 540 | 571 | 0.95 | 536 / 580 | 289,418 / 297,515 |
| river | noshadow | 1920x1080 | 545 | 598 | 0.81 | 478 | 505 | 0.84 | 359 / 405 | 158,301 / 164,189 |
| river | flatground | 1920x1080 | 207 | 223 | 0.31 | 217 | 234 | 0.38 | 538 / 582 | 289,422 / 297,587 |
| age1 | notrees | 1920x1080 | - | - | - | 593 | 625 | 0.93 | - / 345 | - / 120,336 |

The after set: the first group (the five rows from the top) ran on the branch with Q4 and Q5 before the main branch
was merged; the other groups after the merge, which brought the trades' heads and rings (draws 360 to 373-386 in the
first age, 538 to 580 in the second). So the after rows draw a little more, and are still no slower.

**What it says.** The ground's own cost (baseline minus flat ground, same group): first age 428 ms to 383 (-11
percent, group 2), second age 466 to 349 (-25 percent: most of that view is away from the river and from wear);
the whole frame -6 percent (group 2), -16 percent (river), -3 and -2 percent at 720p and 1440p, and within the noise
(about 5 percent between two runs of the same thing) in the first group. llvmpipe keeps evaluating a branch for a
block of pixels if any of them takes it, so it skips less than a card would, where a whole wave of pixels on plain
grass or far from the river skips the work. Ink and shadows are as before (ink 0.89 to 0.95, shadows 0.77 to 0.84);
trees cost 0.93 (their 108 k triangles drawn twice, colour and shadow).
