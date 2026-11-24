// The map palette is read from the page's CSS tokens, so the canvas follows
// the active theme (light, dark, or the data-theme override).

export type RGB = [number, number, number];

const KEYS = [
  "paper", "grass", "grass-dark", "trampled", "trail", "track", "hollow", "crop",
  "water", "water-edge", "ford", "ink", "building", "label", "label-halo",
  "walker", "walker-halo", "fence", "bridge", "ghost", "invalid",
] as const;
export type MapColour = (typeof KEYS)[number];

export interface Palette {
  css: Record<MapColour, string>;
  rgb: Record<MapColour, RGB>;
  heat: RGB[];
}

function parse(c: string): RGB {
  c = c.trim();
  if (c.startsWith("#")) {
    const h = c.length === 4 ? c.slice(1).split("").map(x => x + x).join("") : c.slice(1, 7);
    return [parseInt(h.slice(0, 2), 16), parseInt(h.slice(2, 4), 16), parseInt(h.slice(4, 6), 16)];
  }
  const m = c.match(/\d+(\.\d+)?/g);
  return m ? [Number(m[0]), Number(m[1]), Number(m[2])] : [128, 128, 128];
}

export function readPalette(el: Element = document.documentElement): Palette {
  const cs = getComputedStyle(el);
  const css = {} as Record<MapColour, string>, rgb = {} as Record<MapColour, RGB>;
  for (const k of KEYS) {
    const v = cs.getPropertyValue(`--map-${k}`).trim() || "#808080";
    css[k] = v;
    rgb[k] = parse(v);
  }
  const heat = [0, 1, 2, 3, 4].map(i => parse(cs.getPropertyValue(`--heat-${i}`) || "#808080"));
  return { css, rgb, heat };
}

/** Calls `cb` whenever the theme may have changed (system setting or data-theme). */
export function onThemeChange(cb: () => void) {
  const mq = window.matchMedia("(prefers-color-scheme: dark)");
  mq.addEventListener?.("change", cb);
  new MutationObserver(cb).observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme", "class", "style"] });
}
