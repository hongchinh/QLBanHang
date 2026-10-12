import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { describe, expect, it } from 'vitest';

// Vitest runs from frontend/.
const css = readFileSync(resolve(process.cwd(), 'src/index.css'), 'utf8');
const tailwind = readFileSync(resolve(process.cwd(), 'tailwind.config.ts'), 'utf8');
const block = (selector: string) => css.slice(css.indexOf(`${selector} {`), css.indexOf('}', css.indexOf(`${selector} {`)));

const GROUPS = ['sales', 'inventory', 'cash', 'debt', 'reports', 'catalogs', 'system'];
const HELPERS = ['group-bg', 'pin-bg', 'pin-fg', 'pin-border', 'match'];
const CHANNELS = /^\d+(\.\d+)? \d+(\.\d+)?% \d+(\.\d+)?%( \/ \d?\.\d+)?$/;

describe('nav color tokens', () => {
  it('defines group colors in :root and helpers in :root and .dark', () => {
    for (const id of GROUPS) expect(block(':root')).toContain(`--nav-${id}:`);
    for (const id of HELPERS) {
      expect(block(':root')).toContain(`--nav-${id}:`);
      expect(block('.dark')).toContain(`--nav-${id}:`);
    }
  });

  it('writes every value as bare HSL channels', () => {
    const values = [...css.matchAll(/--nav-[\w-]+:\s*([^;]+);/g)].map((m) => m[1].trim());
    expect(values.length).toBe(GROUPS.length + HELPERS.length * 2);
    for (const v of values) expect(v).toMatch(CHANNELS);
  });

  it('maps every token in tailwind.config.ts', () => {
    for (const id of [...GROUPS, ...HELPERS]) expect(tailwind).toContain(`'hsl(var(--nav-${id}))'`);
  });
});
