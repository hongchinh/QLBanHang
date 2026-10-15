import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { describe, expect, it } from 'vitest';

const frontend = join(dirname(fileURLToPath(import.meta.url)), '..');

describe('help:stale', () => {
  it('runs, reads every help file and exits 0', () => {
    const out = execFileSync(process.execPath, ['scripts/help-stale.ts'], { cwd: frontend, encoding: 'utf8' });
    expect(out).toMatch(/Không có cảnh báo\.|\d+ cảnh báo:/);
  }, 60_000);
});
