import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const SRC = path.resolve(__dirname, '../..');
const ICONS_DIR = path.join(SRC, 'components', 'icons');
const LUCIDE_IMPORT = /(?:from\s+|import\s*\(?\s*)['"]lucide-react(?:\/[^'"]*)?['"]/;

// Files not yet moved to AppIcon. Each migration task removes its files; Phase 04 deletes this list.
const LEGACY: string[] = [];

function walk(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const full = path.join(dir, entry);
    if (statSync(full).isDirectory()) return walk(full);
    return /\.(ts|tsx)$/.test(entry) ? [full] : [];
  });
}

describe('lucide-react imports', () => {
  it('happen only in components/icons (plus the legacy list)', () => {
    const offenders = walk(SRC)
      .filter((file) => !file.startsWith(ICONS_DIR + path.sep))
      .filter((file) => LUCIDE_IMPORT.test(readFileSync(file, 'utf8')))
      .map((file) => path.relative(SRC, file).split(path.sep).join('/'))
      .sort();
    expect(offenders).toEqual([...LEGACY].sort());
  });
});
