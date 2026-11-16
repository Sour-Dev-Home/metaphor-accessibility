// Prints a Markdown table of the surviving mutants in a Stryker JSON report, for the slow suite's run page.
//   node tools/slow/survivors.mjs <mutation-report.json> [repository root]
// The report follows mutation-testing-report-schema: files.<path>.mutants[] with mutatorName, status,
// location.start.line and an optional replacement (docs/raw-sources/slow-suite-tools-2026-11-15.md). Stryker records
// absolute paths; with a root they're shown relative to it, so no machine's folders reach the page.
import { readFileSync } from "node:fs";

const report = JSON.parse(readFileSync(process.argv[2], "utf8"));
const slashes = (path) => path.replaceAll("\\", "/");
const root = process.argv[3] ? slashes(process.argv[3]).replace(/\/$/, "") + "/" : "";
const relative = (file) =>
  root && slashes(file).toLowerCase().startsWith(root.toLowerCase()) ? slashes(file).slice(root.length) : slashes(file);
const cell = (text) => String(text).replaceAll("|", "\\|").replaceAll("\n", " ");

const survivors = Object.entries(report.files)
  .flatMap(([file, { mutants }]) =>
    mutants.filter((m) => m.status === "Survived").map((m) => ({ file: relative(file), line: m.location.start.line, m })))
  .sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line);

console.log(`## Surviving mutants (${survivors.length})\n`);
if (survivors.length === 0) {
  console.log("None.");
} else {
  console.log("| File | Line | Mutator | Replacement |\n|---|---|---|---|");
  for (const { file, line, m } of survivors) {
    const replacement = m.replacement === undefined ? "" : `\`${cell(m.replacement)}\``;
    console.log(`| ${cell(file)} | ${line} | ${cell(m.mutatorName)} | ${replacement} |`);
  }
}
