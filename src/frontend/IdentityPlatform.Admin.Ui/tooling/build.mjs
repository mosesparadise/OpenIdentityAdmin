import { build } from "esbuild";
import { readFile, writeFile, readdir, mkdir } from "node:fs/promises";
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import path from "node:path";
import os from "node:os";
import { mkdtemp, rm } from "node:fs/promises";

const root = fileURLToPath(new URL("../", import.meta.url));
const check = process.argv.includes("--check");
const generated = path.join(root, "generated");
const scratch = await mkdtemp(path.join(os.tmpdir(), "admin-ui-build-"));
/** Records exact runtime package notices; an unknown license or absent license text blocks shipping. */
async function notices() {
  const tree = JSON.parse(execFileSync("npm", ["ls", "--omit=dev", "--all", "--json"], { cwd: root, encoding: "utf8" }));
  const packages = new Map();
  const visit = node => { for (const [name, item] of Object.entries(node.dependencies ?? {})) { packages.set(name, item.version); visit(item); } };
  visit(tree);
  // Tailwind is a build dependency, but its preflight/theme CSS is distributed in app.css.
  const manifest = JSON.parse(await readFile(path.join(root, "package.json"), "utf8"));
  packages.set("tailwindcss", manifest.devDependencies.tailwindcss);
  let content = "OpenIdentityAdmin browser runtime — third-party notices\n\n";
  for (const [name, version] of [...packages].sort(([a], [b]) => a.localeCompare(b, "en"))) {
    const directory = path.join(root, "node_modules", name);
    const manifest = JSON.parse(await readFile(path.join(directory, "package.json"), "utf8"));
    if (manifest.version !== version || manifest.license !== "MIT") throw new Error("Unreviewed runtime package license");
    const license = (await readdir(directory)).find(file => /^license(?:\.md|\.txt)?$/i.test(file));
    if (!license) throw new Error("Missing runtime license");
    content += `${name}@${version} — ${manifest.license}\n${await readFile(path.join(directory, license), "utf8")}\n\n`;
  }
  return content.replaceAll("\r\n", "\n").trimEnd() + "\n";
}
try {
  const result = await build({ absWorkingDir: root, entryPoints: ["main.tsx"], outfile: "app.js", bundle: true,
    minify: true, platform: "browser", target: ["es2022"], format: "iife", jsx: "automatic", write: false,
    define: { "process.env.NODE_ENV": '"production"' }, legalComments: "external", sourcemap: false });
  execFileSync(process.execPath, [path.join(root, "node_modules/@tailwindcss/cli/dist/index.mjs"),
    "-i", "Features/Shell/app.css", "-o", path.join(scratch, "app.css"), "--minify"], { cwd: root, stdio: "pipe" });
  const files = new Map(result.outputFiles.map(file => [path.basename(file.path), file.contents]));
  files.set("app.css", await readFile(path.join(scratch, "app.css")));
  files.set("index.html", await readFile(path.join(root, "index.html")));
  files.set("appearance.js", await readFile(path.join(root, "../IdentityPlatform.DesignTokens/theme.js")));
  files.set("notices.txt", Buffer.from(await notices()));
  await mkdir(generated, { recursive: true });
  if (check && JSON.stringify((await readdir(generated)).sort()) !== JSON.stringify([...files.keys()].sort()))
    throw new Error("Generated asset inventory differs");
  for (const [name, bytes] of files) {
    if (check) { if (!Buffer.from(bytes).equals(await readFile(path.join(generated, name)))) throw new Error(`Generated asset differs: ${name}`); }
    else await writeFile(path.join(generated, name), bytes);
  }
  console.log(`${check ? "Verified" : "Built"} ${files.size} deterministic local assets`);
} finally { await rm(scratch, { recursive: true, force: true }); }
