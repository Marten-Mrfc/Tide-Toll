# Feel review (night 3, last round part A)

Reviewer: judged from the sheets in `build/n3/feel/` (twelve frames each, play zoom) and `build/n3/feel/retake/` (pave, gift), read at 03:46 to 03:50 UTC. Sounds judged from the tables only. Not read: feel-ford and the fresh feel-landing (not finished). Not shown in any sheet: the bell's own swing, a boat mooring.

The set is light, in the right palette family, and nothing flickers in the single-event strips. The faults are colour (ink edge swallows thin pieces), one reward that is nearly invisible (landing), one that hides its subject (pave), and two that were never actually seen (bell swing, ford/boat).

## MUST FIX (worst first)

1. **The landing ring is almost invisible** (`feel-landing`, frames 3 to 8). A 1 px grey-white arc on the bank, lost between the "Flour 0 of 38" and "Full, leaving" labels; only frame 8 shows a white arc. The owner asked for a ripple at the landing: today nobody would see it. Change: put it on the water beside the landing (as the progress file already says), make it a River/Paper two-ring ripple at least 2 m across with a ring 0.2 m thick, life 0.9 s; check it again in a sheet that also shows a boat mooring.
2. **Pave grit hides the road and the walkers** (`retake/feel-pave`, frames 2 to 10). 24 near-black stones lie along the whole road at once; the pale road reads as a dotted ink line, like litter or ants, and the walker at the left end is covered. Change: 10 to 12 stones, Packed earth / Trail earth tones (not Ink), 0.25 m, a wave that moves from one end to the other with at most 4 stones live at once, not all along the road in the same frames; keep the ink edge off pieces under 0.3 m.
3. **Thin rings and sparks read as ink, not gold** (`feel-flourish` frames 2 to 6; `retake/feel-gift` frames 1 to 7; `feel-request` frames 2 to 6). The ring is a dark circle until it is largest (gift frame 8 is the first frame with any gold), and the sparks are black dots. They look like a drawn circle and spilled pepper. Change: inner radius 0.75 as planned so the fill shows; ring fill Thatch (light tone), start pale gold from frame 1 (not at its widest); sparks 0.25 m or larger, Thatch light, ink edge 1 px only.
4. **Request reward is staged on bare ground** (`feel-request`). The ring and dots appear at the end of the path beside the empty "The market, to buy" plot, where the coin is, not on a building. Either the staging is wrong (the market must exist, as the progress file says it is the asker) or the code places it at the path, not at the building. Retake with the market built and confirm it is at the market's door.
5. **Bell swing and boat mooring were never seen** (`feel-bell`). The strip shows three white rings round the post (readable, frames 8 and 11 bright white, a little loud against the paper) but the swing, the actual owner ask, is not in it. Take a strip with the town really ringing the bell and one with a boat mooring; if the swing is not visible at play zoom, add a 3 degree larger swing for 0.44 s.
6. **Coin is cream-white, not gold, and its hop does not read** (`feel-coin`, frames 1 to 11). A pale 12 px disc that looks like an egg or a pebble on the trail; the hop is a flip in place. Change: Thatch gold (light tone) fill, 1.6 m, more travel up the screen (about 1.5 m for the first 0.3 s), then fall. Also raise the least gap between coins from 0.08 s to 0.2 s at town level (thirty villagers delivering will put up to 12 discs on screen at once).

## SHOULD FIX

- Trail dust (`feel-trail-dust`): 5 to 6 px white specks at the walker's feet in frames 2 to 12; it is light but close to unseen, and it looks like white litter on a brown path. Make the puff Trail earth light, 0.5 m, rising 0.3 m; keep the 0.4 x (1 - wear) chance.
- Extend (`feel-extend`): good, readable, but the puff cloud sits on the walker and the path beside the cottage in frames 4 to 8 and hides him. Spread it away from the path side, or shorten to 0.6 s. The white-plus-dark-dots look is a little like popcorn: use Limewash and drop the ink edge to 1 px.
- Bell rings (`feel-bell` frames 8, 11): pure white, brighter than anything else in the town. Use Paper at 80 percent size or Limewash.
- Gift ring (`retake/feel-gift`): it crowds the plot's own frame; a ring 20 percent smaller than the plot would sit inside it cleanly.
- Sounds: `gift-ring` (0.90 s) has a least gap of 0.4 s, so a level-up with banner, flourish and gift rings will cut its own tail; set the least gap to 0.9 s for gift-ring, or play only one per level. Check that `ripple` at 0.35 s for a wader on a ford does not make a rattle: use 0.6 s there.
- Pieces at pool pressure: the rule "above 140 live, skip frequent ones" is right, but the progress file reports one burst of 200 live in a bot run; confirm in a thirty-villager sheet that coins plus trail dust together stay under about 25 on screen.

## One line per reward

- Delivery coin: seen (12 px), small and short, hides nothing; too pale and the hop too flat. Sound present (coin-hop, 0.3 s gap) fine.
- Purchase flourish: seen as a ring, but dark and with black sparks; size right (0.7 s). Sound gift-ring; tail cut by the gap.
- Cottage extended: seen, well in style, a little too much cloud over the neighbouring path. Sound dust-puff fine.
- Road paved: seen but too much (24 dark stones along the whole road) and it hides the road. Sound grit 0.5 s gap fine.
- Trail dust: barely seen (too little), harmless. Sound at -4 dB, 1.4 s gap, fine for the most frequent reward.
- Landing: not seen (too little). Sound ripple present.
- Ford: not read (strip unfinished). Sound ripple present; gap 0.35 s may rattle.
- Bell: rings seen (a bit loud white); the swing itself not shown, so not judged. Sound bell-creak 0.45 s with 0.6 s gap fits.
- Level gift: seen late (pale only at frame 8), dark ring before; right size. Sound gift-ring.
- Request met: seen at the wrong place (bare ground); same colour fault as the flourish.

## Owner's list against the rewards

Coin pop at delivery: yes. Dust on a new trail: yes (too faint). Ripple at landing: yes in code, not visible. Bell swing: yes in code (old swing), not proven in a picture. No action on the owner's list is without a reward; the faults are in visibility, not coverage.
