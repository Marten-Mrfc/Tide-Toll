// The flow preview's readout for the page. It is the simulation's own wording
// (describePreview), except for a footbridge that would save under about 5%
// of the walk: such a bridge takes only some of the crossings (round 3 review:
// 24% to 45% on maps where it saves under 5%, against at least half when it
// saves 8% or more), so the readout says so instead of promising a shorter route.
import { FlowPreview, describePreview } from "../sim/preview";

const SMALL_SAVING = 0.05;

export function readout(p: FlowPreview, name: (b: number) => string, tool: string): string {
  const text = describePreview(p, name);
  if (tool !== "bridge" || p.blocked.length || p.reroutedPerDay < 0.5) return text;
  const saving = 1 - p.meanCostRatio;
  if (saving >= SMALL_SAVING) return text;
  return "Saves under 5% of the walk, so many will keep to their old way";
}
