import { execFileSync } from 'node:child_process';
import { readFileSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseHelpDoc, stepHash } from '../src/features/help/parse-help.ts';
import { formatStaleReport, staleWarnings, type CommitStamp, type StaleWarning } from '../src/features/help/stale.ts';
import type { HelpVideo } from '../src/features/help/types.ts';

// Lists help that may be out of date (warnings only; exit 1 only for an unreadable help file).
const here = dirname(fileURLToPath(import.meta.url));
const helpDir = join(here, '../src/features/help');
const repoRoot = execFileSync('git', ['rev-parse', '--show-toplevel'], { cwd: here, encoding: 'utf8' }).trim();
const git = (args: string[]) => execFileSync('git', args, { cwd: repoRoot, encoding: 'utf8' }).trim();
// %ct orders commits across time zones; %cs is the committer's local day, compared with `updated`.
const lastCommit = (pathspecs: string[]): CommitStamp | null => {
  const out = git(['log', '-1', '--format=%ct %cs', '--', ...pathspecs]);
  if (!out) return null;
  const [at, day] = out.split(' ');
  return { at: Number(at), day };
};
const videos = JSON.parse(readFileSync(join(helpDir, 'videos.json'), 'utf8')) as Record<string, HelpVideo>;

const warnings: StaleWarning[] = [];
for (const file of readdirSync(join(helpDir, 'content')).filter((f) => f.endsWith('.md')).sort()) {
  const key = file.slice(0, -3);
  const { doc, errors } = parseHelpDoc(readFileSync(join(helpDir, 'content', file), 'utf8'), key);
  if (!doc) { console.error(errors.join('\n')); process.exitCode = 1; continue; }
  warnings.push(...staleWarnings({
    key,
    updated: doc.updated,
    helpFile: lastCommit([`frontend/src/features/help/content/${file}`]),
    sources: lastCommit(doc.sources.map((s) => `:(glob)${s}`)),
    unmatchedSources: doc.sources.filter((s) => git(['ls-files', '--', `:(glob)${s}`]) === ''),
    steps: doc.steps.map((s) => ({ id: s.id, hash: stepHash(s.text) })),
    video: videos[key],
  }));
}
console.log(formatStaleReport(warnings));
