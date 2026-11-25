// Every tunable of the prototype lives here. Values follow the design spec
// (Gameplay Systems tab, "The wear model"), with the trail-tuning research's
// changes where the spec's numbers did not form trails at prototype scale.

export const PARAMS = {
  // Map and time
  gridSize: 128, // cells per side, 2 m each
  tickHz: 10, // simulation ticks per second of sim time
  daySimSec: 15, // one in-game day at 1x
  seasonSimSec: 60, // one season = 4 days

  // Walkers. Spec speed is 1.34 m/s (0.67 cells/s); at that speed a 200-person
  // town crosses too few cells per day for any trail to form, so time is
  // compressed (research: 3 cells/s and a 6 s dwell give about one round trip
  // per walker per day).
  defaultWalkers: 200,
  minWalkers: 50,
  maxWalkers: 500,
  walkSpeed: 3.0, // cells per sim second on grass
  dwellSimSec: 6, // time spent at home or at a destination
  dwellJitter: [0.5, 1.5] as const, // multiplier range on dwell
  // Trip mix: share of trips that go to each destination kind.
  tripMix: { well: 0.35, field: 0.3, market: 0.35 },
  stuckSimSec: 5, // a walker with no progress for this long is sent home
  retrySimSec: 5, // a walker with no reachable destination waits this long

  // Wear. W in 0..100; gain W += I * (1 - W/100) on entering a cell.
  // Round 3 tuning (seeds 1 to 20, 101 to 120 and 201 to 220): with the flow
  // discount below, modest wear forms narrow trails that merge into shared trunks.
  wearPerStep: 2.2, // spec 0.75
  sideShare: 0.4, // spec 0.15; share of I given to the two side neighbours (wider soft edges, not wider trails)
  // Ground-state thresholds: grass < 10 <= trampled < 25 <= trail < 50 <= track.
  // Hollow way needs W >= 85 held for hollowHoldSimSec.
  stateThresholds: [10, 25, 50, 85] as const,
  hollowHoldSimSec: 2400, // spec: 10 in-game years
  // Index by state: grass, trampled, trail, track, hollow way.
  surfaceCost: [1.0, 0.95, 0.85, 0.75, 0.72] as const,
  stateSpeedMul: [1.0, 1.0, 1.1, 1.2, 1.2] as const,
  // Routing cost of worn ground. Instead of the four-step surfaceCost table,
  // a smooth convex discount: cost x (1 - trailDiscount * min(1, W/discScale)^discPow).
  // A lane worn by one house gets almost no discount; only lanes worn by
  // several houses do, which is what makes house paths merge. 0 restores the table.
  trailDiscount: 0.3,
  discScale: 80,
  discPow: 3,
  // Flow discount: a second discount from recent traffic (a footstep count per
  // cell that decays with flowHalfLife): cost x (1 - flowDiscount * (1 - exp(-flow/flowScale))).
  // Walkers join the lane others are using now, not just the oldest one.
  flowDiscount: 0.5,
  flowScale: 40,
  flowHalfLife: 20, // sim seconds
  // Floor on the combined multiplier. Without it a busy track costs 0.35 of
  // grass. Round 3 lowered it from 0.6 to 0.45 for trunk-share headroom; the
  // frugal walkers below keep new footbridges and fence gaps winning traffic.
  minSurface: 0.45,
  // Frugal walkers: a second view of the ground for walkers who weigh distance
  // more than company. Their cost is 1 + (surface - 1) * frugalDisc (0 ignores
  // worn ground, 1 is the ordinary walker). A trip is frugal if the walker is
  // an explorer (a fixed share of walkers, by a hash of the walker id), or if
  // the ordinary route from where it stands is longer than detourCap times the
  // straight distance (0 turns the cap off). The cap is what keeps a new
  // footbridge winning on maps where busy trunks are cheap.
  explorerShare: 0.1,
  frugalDisc: 0.3,
  detourCap: 1.25,
  bridgeFlow: 1, // 1: a bridge deck is made cheaper by recent traffic like any lane (not by wear); 0: fixed bridgeCost
  // Half-life of wear by band, in sim seconds. Spec: 1, 2, 4 seasons (60/120/240 s).
  halfLifeLow: 60, // W < 25
  halfLifeMid: 120, // 25 <= W < 50
  halfLifeHigh: 240, // W >= 50
  halfLifeHollow: 2400, // spec: 40 seasons
  decayStepSimSec: 1,

  // Route cost
  tasteNoise: 0.08, // +/- 8% on the trail discount, per walker
  stepNoise: 0.06, // per (walker, cell) noise on a step's cost; spreads lanes
  staticNoise: 0.04, // per-cell fixed cost noise; breaks grid ties into bends
  cropPenalty: 0.5, // growing crops add this to a field cell's cost
  lookahead: 20, // cells of field descent a walker looks ahead to aim a straight line
  shortcutSlack: 1.0, // a straight line may cost this much times the field's route
  fordCostMul: 4.0, // round 3 (was 3); the ford's 0.4 speed alone would suggest about 2.5
  fordSpeedMul: 0.4, // spec: ford walker speed 0.4
  bridgeCost: 0.8,
  bridgeSpeedMul: 1.2,
  // Steering tools added in round 3 (spec "Tool table" and "Ground states").
  gravelCost: 0.6, // round 4 (spec 0.8): the toy's worn ground is cheaper than the spec's (floor 0.45 against 0.72), so 0.8 could not pull a trail more than a cell or two; traffic can make gravel cheaper still
  gravelSpeedMul: 1.15,
  gravelFloor: 0.45, // floor on a busy gravel cell's cost (minSurface for worn ground)
  gateCost: 1.5, // added to a gate cell's step cost: the spec's 1.5 s delay, one cost unit per grass cell
  gateSpeedMul: 0.4, // crossing a gate cell takes about 0.5 sim s longer, the same 1.5 cells of walking
  fieldRefreshSimSec: 5, // each cost-to-go field is rebuilt this often
  urgentFieldsPerTick: 1, // after an edit, fields rebuilt per tick until all are fresh (spread, destinations first)

  // Crowding (Weidmann): v = v0 (1 - exp(-g (1/rho - 1/rhoMax))), rho in people per m^2.
  crowdStrength: 1.0,
  crowdAvgSimSec: 10, // density is a moving average over this window
  crowdMaxCostMul: 3,
  weidmannGamma: 1.913,
  weidmannRhoMax: 5.4,
  cellAreaM2: 4,

  // Rain and mud (round 4; spec "Weather and mud", "Ground states", "Seasons and
  // weather"). A weather stream of its own (seeded from the map seed, never the
  // walkers' RNG) rolls once per in-game day for rain. While it rains, every
  // unpaved land cell at wear 25 or more turns to mud; the mud lasts for that
  // day plus one more (plus two in autumn), drying out over the last part of it.
  rainOn: true, // the player's default; the old gate sets run with it off
  rainChance: [0.3, 0.15, 0.4, 0] as const, // per day by season; winter: snow only in the spec (left out)
  mudDays: [2, 2, 3, 2] as const, // days of mud counting the rain day (autumn "two more"; see README for the conflict)
  rainRampSimSec: 2, // rain fades in and out over this long (drawing and sound only)
  wetUpSimSec: 3, // the ground goes from dry to fully wet over this long once rain starts
  drySimSec: 7.5, // mud dries out over the last half day of its run (the spec gives no drying rate)
  // Muddy cost: the spec's multipliers on the state's base cost (trail 0.85 x 1.6
  // = 1.36), blended with the toy's own worn-ground cost by mudReplace
  // (1 = the base cost alone, 0 = multiply the toy's discounted cost), then
  // blended with the dry cost by wetness. Busy mud is no easier: no flow discount.
  mudCostMul: [1, 1, 1.6, 1.7, 1.8] as const, // by ground state; grass and trampled never get muddy
  mudReplace: 1,
  mudSpeedMul: 0.8, // walking speed on mud at full wetness (not in the spec)

  // House growth (round 4; spec "Where houses appear", "Growth rate and density",
  // "Frontage"). A Hamlet cottage (3 x 3 with its garden, 3 villagers) appears
  // every houseEverySimSec on the best valid plot by the frontage rules, while
  // the population is below hamletTarget. Walkers slider = population now.
  growthOn: true, // the player's default; the old gate sets run with it off
  houseEverySimSec: 30, // Hamlet: one household per 2 days
  houseFirstSimSec: 30, // the first one comes after 2 days
  hamletTarget: 250, // people; no new household would take the town above this
  householdSize: 3,
  frontageWear: 50, // the plot must touch a cell this worn (track)
  plotClearWear: 25, // and overlap no cell this worn (trail)
  frontageFloor: 50, // a fronted cell does not decay below this while its house stands
  plotNeighbourRadius: 6, // +1 score per house within this many cells, up to 3

  // Flow preview: trip counts per (house, destination) decay with this time
  // constant, in days. Longer is steadier, shorter follows changes faster.
  tripRateDays: 3,

  // Metrics
  metricWindowDays: 2,
  metricBuckets: 4,
  firstTrailCells: 10, // a connected run of trail cells this long counts as "a trail"
  trunkTopShare: 0.1,
};

export type Params = typeof PARAMS;

export enum Ground {
  Grass = 0,
  Trampled = 1,
  Trail = 2,
  Track = 3,
  Hollow = 4,
}

export function groundOf(w: number, hollow: boolean): Ground {
  const t = PARAMS.stateThresholds;
  if (w < t[0]) return Ground.Grass;
  if (w < t[1]) return Ground.Trampled;
  if (w < t[2]) return Ground.Trail;
  if (hollow) return Ground.Hollow;
  return Ground.Track;
}
