import { parseHelpDoc } from './parse-help';
import type { HelpDoc } from './types';

// path -> raw text; the key is the file name without .md. Invalid files are logged and skipped
// (help-guard.test.ts fails on them first).
export function parseHelpFiles(files: Record<string, string>): HelpDoc[] {
  const docs: HelpDoc[] = [];
  for (const [path, raw] of Object.entries(files)) {
    const key = path.replace(/^.*\//, '').replace(/\.md$/, '');
    const { doc, errors } = parseHelpDoc(raw, key);
    if (doc) docs.push(doc);
    else console.error(`Help file ${key} is invalid:\n${errors.join('\n')}`);
  }
  return docs;
}

export const helpDocs: HelpDoc[] = parseHelpFiles(
  import.meta.glob<string>('./content/*.md', { query: '?raw', import: 'default', eager: true }),
);
