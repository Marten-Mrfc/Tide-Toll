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
  gravelCost: 0.8, // a gravel cell's surface cost; worn ground and traffic can still make it cheaper
  gravelSpeedMul: 1.15,
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
