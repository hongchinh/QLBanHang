import { readFileSync } from 'node:fs';
import path from 'node:path';
import { describe, expect, it } from 'vitest';

const css = readFileSync(path.resolve(__dirname, '../../index.css'), 'utf8');
const TONES = ['create', 'edit', 'danger', 'confirm', 'info', 'export', 'print', 'accent', 'muted', 'warn'];
const block = (selector: string) => css.slice(css.indexOf(`${selector} {`), css.indexOf('}', css.indexOf(`${selector} {`)));

describe('icon tone tokens', () => {
  it.each([':root', '.dark'])('%s defines every --icon-* token', (selector) => {
    for (const tone of TONES) expect(block(selector)).toMatch(new RegExp(`--icon-${tone}:\\s*[\\d.]+ [\\d.]+% [\\d.]+%;`));
  });
});
