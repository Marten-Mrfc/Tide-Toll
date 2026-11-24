// Bundles src/ui/main.ts into one IIFE and inlines it into src/page.html.
// The output is content-only HTML (no doctype/html/head/body): the Artifact
// publisher adds its own skeleton around it.
import * as esbuild from "esbuild";
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const result = await esbuild.build({
  entryPoints: [join(here, "src/ui/main.ts")],
  bundle: true,
  format: "iife",
  minify: false,
  target: "es2020",
  platform: "browser",
  write: false,
  legalComments: "none",
});
const js = result.outputFiles[0].text;
if (/<\/script/i.test(js)) throw new Error("bundle contains a closing script tag");
const template = readFileSync(join(here, "src/page.html"), "utf8");
const marker = "/*BUNDLE*/";
if (!template.includes(marker)) throw new Error("page.html has no bundle marker");
const html = template.replace(marker, () => js);
if (/<!doctype|<(html|head|body)[\s>]/i.test(html)) throw new Error("page must not hold doctype/html/head/body tags");
mkdirSync(join(here, "dist"), { recursive: true });
writeFileSync(join(here, "dist/index.html"), html);
console.log(`wrote dist/index.html (${(html.length / 1024).toFixed(1)} KB)`);
