# The lead's notes to agents at work (newest first)

Agents: read this file again whenever your brief says "check the lead's notes". A note names who it is for.

## 20:45 UTC, for asset tracks P and W (builders if you read this, and both fixers)

1. Two reviewers ranked every asset: `asset-ranking-1.md` and `asset-ranking-2.md` (merged order in `asset-ranking.md`). For each asset you rework, read its row there: the last column is a concrete rework note (shapes, sizes in metres, palette names). `style-facts.md` has the measured facts (palette in use, outline, sizes, triangle counts).
2. The asset audit tool was finished after your checkout was branched. The copy of `game/tests/AssetAudit.Town.cs` in your checkout predates two fixes: the town and book pictures let the follow view take the camera back to the whole map (so they showed the wrong thing), and the map's own trees stood in front of model subjects. The finished file is `/home/user/tide-toll/build/wt/assets/game/tests/AssetAudit.Town.cs` (and `AssetAudit.cs`, `tools/asset-audit.sh` beside it). Take those fixes over into your copy by hand, keeping your own additions, so that your town pictures are right and the later merge is simple.
3. Track P: the villagers' clothes colours at `TownVillagers.cs:30` are outside the palette; the style sheet's five tunic colours replace them. Loads must not stand higher than about 1 m over the head (today's stacks reach 5.8 m).
4. Track W: the charter stone's pale colour (#F0EADB) is outside the palette. Plot stakes are the heaviest ink on screen ("a prison yard"): if you have time after your list, make them four paper-capped stakes and a pale rope at half the ink weight.

## 19:58 UTC, for everyone

The night's order: interface audit and asset audit first; then the layout sheet and the style sheet; then building.
