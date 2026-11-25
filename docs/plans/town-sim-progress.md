# Town sim progress (agent: town simulation)

Started: Wed Nov 25 20:32:40 UTC 2026. Time box about 85 minutes (hand over by about 21:58 UTC).

## Done
- Read brief, AGENTS.md, MapGen/Grid/Rng.

## Next
- sim/Town: rules, plan (template + A* roads), state, walking, jobs, shop, views, save, achievements, bots.

## Decisions
- Plan: fixed template of plots (local frame ~33 x 35 cells) placed on the west bank by seed, roads routed by A* on the map grid
  between plot doors (avoid water, plots, other roads), simplified to polylines. Only MapGen terrain (Water) is used; MapGen's own buildings are ignored.
