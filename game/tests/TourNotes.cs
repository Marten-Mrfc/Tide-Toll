using System.Collections.Generic;

namespace TideToll.Game.Tests;

/// <summary>
/// What each tour picture really shows, and the faults of the game seen in it, from looking at the pictures of whole
/// runs (the run is repeatable: same town, same moments). Written into build/tour/shots.json for made shots; a shot
/// without an entry keeps the plan's line. Data, not checks: look again and update after game changes.
/// </summary>
public static class TourNotes
{
    public static readonly Dictionary<string, string> Shows = new()
    {
        ["a01-first-minute"] = "The start, close in: one cottage, the well, a faint reddish trail between them and one villager with a yoke; the staked plots for level 2 (a cottage, the bell) are labelled.",
        ["a02-first-coin"] = "The first delivery: a small \"+3\" rises over the villager at the cottage door; the coin counter reads 7.",
        ["a03-interface"] = "The whole interface at level 1 in the normal view: coin and level bar with the next gift top left, the shop's four groups on the right, the bell, Back to the town, achievements and pause along the bottom.",
        ["b01-villager"] = "Two villagers on the trail between the cottage and the well, the second just bought by its bookmark.",
        ["b02-pace-and-load"] = "After Pace and the Basket (both bought by bookmark): villagers on the well road close up; the Basket line has turned into the Yoke line.",
        ["b03-shop-lines"] = "The normal view at level 14 with the shop showing lines you can buy, lines short of coin with their bars, a locked line (Flour price, level 15) and a finished one (Louder bell, at its best).",
        ["b04-extend-mode"] = "Extend mode by its bookmark: two cottages lit in blue, the suggested one tagged with its price, the pointed one with \"Extend this cottage: N coin. Rooms 2 to 3.\"",
        ["b05-extended"] = "The same view a level later: the pointed cottage (middle) after two extensions (a timber frame on one side, a lean-to on the other), a cottage with one extension at the top left; no plain cottage is in this view.",
        ["b06-building"] = "The market stall standing on its plot beside the bell green, just bought by its bookmark.",
        ["b07-bell"] = "The bell rung by Space: the bell card says \"Ringing: everyone hurries\"; villagers on the trail.",
        ["b08-hand"] = "Villagers queued along the road at the field; the shop lists \"A third hand at the field\" and a note says it is new to buy.",
        ["c01-level-banner"] = "Level 2: the banner \"A cottage, a lane and the bell green.\"; the new cottage, its lane and the bell green are drawn in.",
        ["c02-staked-plots"] = "The staked plot of the first field with its label \"A field, at level 3\", the town at the top.",
        ["c03-field"] = "The first field (rows of grain) with a villager at its edge and the track to the well.",
        ["c05-request"] = "A request running: the card says who asks, how much of what within how long, how much is delivered with a bar, and the reward.",
        ["c07-mill"] = "The mill with its sails beside the market stall, villagers queued at its door, the second field to its left.",
        ["d01-trail-new"] = "The level-2 lane just given: a faint lighter trail from the new cottage (top left) to the well, beside the worn first road.",
        ["d02-trail-worn"] = "The same view later: the lane is a firm earth track like the others.",
        ["d03-pave-mode"] = "Pave mode by its bookmark: worn roads lit in blue, the suggested one tagged with its price, the pointed lane with \"Gravel for this road: N coin. Click to pave.\"",
        ["d04-gravel"] = "The same view: the lane laid with gravel (pale grey).",
        ["d05-cobbles"] = "The lane cobbled (level 9, by clicks), the road beside it in gravel, earth tracks around them.",
        ["d06-crowded"] = "A crowded earth road (nobody has paved yet): villagers bunched on it, the road drawn redder; the hint says to pave it.",
        ["e02-town"] = "The whole hamlet at the charter, framed clear of the cards: cottages, fields, mill, market, bell green, every road cobbled, the river to the east.",
        ["e03-charter"] = "The charter banner \"The town is granted its charter. Ring in the next age when you are ready.\" over the town; the Ring bookmark appears.",
        ["e04-achievements"] = "The achievements page opened with Tab.",
        ["f01-ring"] = "Ringing in the next age (N): the page wipe halfway, the first-age town on the left, the river age's map coming in on the right; the tide dial is already up.",
        ["f02-age-page"] = "The second age's page \"The second age: The River\" over the town at level 17: boats at high water, the tide dial, wool and cloth, the four crossings, the seal at the end.",
        ["f03-landing"] = "High water: a boat moored at the landing with \"Flour 35 of 174\" over it, villagers queued along the landing road with sacks, the tide dial (high water, falling) and a skipper's request card.",
        ["f04-ford"] = "The ford's stepping stones and the river at its row with the tide dial; who is waiting or wading depends on the moment.",
        ["f05-pasture"] = "Both banks: the landing with two boats and their cargo labels, the weaver's road on the near bank, the pasture with sheep on the far bank, the drawbridge at the bottom and the ferry at the top.",
        ["f06-ferry"] = "The ferry raft at the far bank's post with one rider, its rope across the river, a long queue on the near bank (level 24); the raft is docked, not in mid-river.",
        ["f07-drawbridge"] = "The drawbridge's lifting half raised for a boat passing upriver (\"Garth: leaving with 0 Grain\"), four walkers waiting on the near bank; the ford below.",
        ["f08-river-shop"] = "The shop in the second age with its own group \"The river\" at the bottom.",
        ["f09-seal"] = "The town on both banks when the borough seal is granted.",
        ["f10-seal-page"] = "\"The second age is complete\": the borough's numbers for both ages and that more ages are planned.",
        ["g01-close"] = "The closest view on the middle of the town: tilted, cottages, well, bell and villagers show their sides.",
        ["g02-zoom-1"] = "The normal view of the whole town.",
        ["g03-zoom-2"] = "Zoomed out to the page: the interface is a slim strip (coin, level, pause) at the top.",
        ["g04-book"] = "Fully out, after the seal: the open book, the town's numbers and the first age's achievements on the left page, \"The Town on the River\" map on the right.",
        ["g05-closing"] = "Pausing: the cover swinging shut over the book (held part way).",
        ["g06-cover"] = "The closed cover: Tide & Toll, the town's name, Resume, Settings, New town, Quit.",
        ["g07-new-town"] = "\"Start a new town? This town will be lost.\" with Start a new town and Keep this town (Keep focused).",
        ["h01-sound"] = "Settings, Sound page.",
        ["h02-display"] = "Settings, Display page: window mode, resolution, VSync, frame limit; interface size, coin numbers, follow view.",
        ["h03-keys"] = "Settings, Keys: Pave being rebound to Space, the clash box \"That key is taken\" offering Swap keys or Cancel (cancelled afterwards).",
        ["h04-saving"] = "Settings, Saving page.",
        ["h05-big"] = "The game at interface size 150 % (put back to 100 % afterwards).",
    };

    public static readonly Dictionary<string, string> Wrong = new()
    {
        ["a01-first-minute"] = "The trail is so faint it is almost invisible at this size.",
        ["a02-first-coin"] = "The coin number is small and drawn over the villager's head.",
        ["b02-pace-and-load"] = "The Basket does not change how a load looks: the villagers carry the same yoke and buckets as at the start, so the bigger load cannot be seen; the hint ring sits on the road with no reason shown.",
        ["b03-shop-lines"] = "No line shows what it is worth in coin a minute. The hint line in the ledger card is cut off by the request card (\"Hint: A queue at the field. Better tools\" ends mid-sentence). Coin numbers are drawn twice, a faded copy above each.",
        ["b04-extend-mode"] = "A coin number is drawn on top of the lit cottage, and the price tip overlaps the cottage's roof.",
        ["b05-extended"] = "One extension and two are hard to tell apart; a coin number is drawn on the cottage's door.",
        ["b07-bell"] = "While it rings the bell card says nothing about time left or the rest to come (the rest time shows only after).",
        ["b08-hand"] = "Nothing on the map marks the field where the hand would go; the level banner covers the top of the view.",
        ["c01-level-banner"] = "The hint already says \"A road is crowded. Pave it so more can walk it.\" with two villagers.",
        ["c02-staked-plots"] = "A plot label lies under the shop card at the right edge of other views (\"The market, at\" cut off in b02's run).",
        ["c03-field"] = "Hardly anyone is seen carrying Grain: one villager at the field edge.",
        ["c07-mill"] = "Queued villagers are drawn through the mill's body and sails. A grey see-through rectangle is drawn on the river (bottom right), in the first age.",
        ["d06-crowded"] = "Bunched villagers are drawn through each other; the red tint of the crowded road is weak.",
        ["e02-town"] = "No charter stone is seen on the bell green. Extended cottages read as dark boxes from this height. The grey see-through rectangle on the river again.",
        ["e03-charter"] = "No charter stone stands on the green: the moment is only the banner.",
        ["f06-ferry"] = "The raft was caught docked at the far post although the condition saw it mid-river: the drawing lags the simulation or the picture came late. The level banner and a staked label over the river.",
        ["f07-drawbridge"] = "The boat's label is drawn over the bridge and the keeper's hut; the level banner covers a boat at the top; nobody waits on the far bank.",
        ["d03-pave-mode"] = "The price tip is drawn over villagers; the lit roads are a pale blue that is hard to see on the worn earth.",
        ["f01-ring"] = "The wipe shows the right half as a pale wash where shop cards and labels from the old view still show through.",
        ["f02-age-page"] = "A plot label \"A cottage, at level 18\" is drawn through the edge of the page card and half under it.",
        ["f03-landing"] = "The level banner covers the top of the view; the cargo label and the coin number overlap the queue; the queue is drawn through the landing's crates.",
        ["f04-ford"] = "A grey see-through rectangle is drawn across the river at the ford's row.",
        ["f05-pasture"] = "The boats' cargo labels are drawn over each other and over the landing; the bank roads run as flat yellow bands.",
        ["f08-river-shop"] = "The cards cover the river: the river group is at the bottom right with little room.",
        ["g03-zoom-2"] = "The left page's row names are cut by the screen edge (\"ls\", \"ed\", \"e town\"); the slim strip (coin, level, pause) is drawn over the map's title \"The Town on the River\".",
        ["g04-book"] = "After the borough seal the left page still says \"The first age, level 28 of 28\" and \"The first age is complete. The river comes next.\", counts 7 cottages (the river cottages are left out) and lists only the first age's achievements.",
        ["g05-closing"] = "The closing book shows only the right page and the board; no town summary page.",
        ["h03-keys"] = "The clash box covers four rows of the right page, among them the rows named in the clash.",
        ["h05-big"] = "At 150 % the shop cards overlap the achievements card at the bottom right; group headings sit under the cards (\"Work\", \"Town\" cut).",
    };
}
