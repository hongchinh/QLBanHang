import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const SRC = path.resolve(__dirname, '../..');

const BANNED = /[▲▼▾✕]/;
// An arrow at the start or end of a text run, a string label or a line is an icon, not prose.
const EDGE_ARROW = /(^|[>'"}])\s*[←→]|[←→]\s*($|[<'"{])/;

function walk(dir: string): string[] {
  return readdirSync(dir).flatMap((entry) => {
    const full = path.join(dir, entry);
    if (statSync(full).isDirectory()) return walk(full);
    return /\.(ts|tsx)$/.test(entry) ? [full] : [];
  });
}

function isTestFile(file: string): boolean {
  return /\.test\.(ts|tsx)$/.test(file);
}

function stripComment(line: string): string | null {
  const trimmed = line.trim();
  if (trimmed.startsWith('//') || trimmed.startsWith('*') || trimmed.startsWith('/*')) return null;
  const cut = line.indexOf(' // ');
  return cut >= 0 ? line.slice(0, cut) : line;
}

describe('glyph icons', () => {
  it('no source file uses a text glyph where AppIcon belongs', () => {
    const violations: string[] = [];
    for (const file of walk(SRC).filter((f) => !isTestFile(f))) {
      const rel = path.relative(SRC, file).split(path.sep).join('/');
      readFileSync(file, 'utf8')
        .split(/\r?\n/)
        .forEach((raw, index) => {
          const line = stripComment(raw);
          if (line === null) return;
          if (BANNED.test(line) || EDGE_ARROW.test(line)) {
            violations.push(`${rel}:${index + 1}: ${raw.trim()}`);
          }
        });
    }
    expect(violations, violations.join('\n')).toEqual([]);
  });
});
