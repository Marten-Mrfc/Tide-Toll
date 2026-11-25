// The Artifact host serves the published page inside its own document
// skeleton: a doctype, a charset, a width=device-width viewport meta and a
// body. The built page has none of these (the page contract forbids them), so
// the browser tests load it the same way: wrapped in that skeleton, written to
// test-results/host.html. Without the viewport meta, mobile emulation lays the
// page out 980 px wide and a "phone" test tests nothing.
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { fileURLToPath, pathToFileURL } from "node:url";
import { dirname, join } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
export const builtFile = join(here, "..", "dist", "index.html");

/** Writes the hosted page and returns its file URL. */
export function hostedUrl() {
  const out = join(here, "..", "test-results");
  mkdirSync(out, { recursive: true });
  const page = readFileSync(builtFile, "utf8");
  const html = `<!doctype html>\n<html lang="en">\n<head>\n<meta charset="utf-8">\n<meta name="viewport" content="width=device-width, initial-scale=1">\n</head>\n<body>\n${page}\n</body>\n</html>\n`;
  const file = join(out, "host.html");
  writeFileSync(file, html);
  return pathToFileURL(file).href;
}
