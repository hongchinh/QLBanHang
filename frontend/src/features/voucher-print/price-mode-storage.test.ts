import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { readPriceMode, writePriceMode } from './price-mode-storage';

const KEY = 'qldh:stock-print-price-mode:u1';

describe('price-mode-storage', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => vi.restoreAllMocks());

  it('defaults to Voucher', () => {
    expect(readPriceMode('u1', true)).toBe('Voucher');
  });

  it('round-trips under the per-user key', () => {
    writePriceMode('u1', 'None');
    expect(localStorage.getItem(KEY)).toBe('None');
    expect(readPriceMode('u1', true)).toBe('None');
  });

  it('falls back from Cost without the permission', () => {
    localStorage.setItem(KEY, 'Cost');
    expect(readPriceMode('u1', true)).toBe('Cost');
    expect(readPriceMode('u1', false)).toBe('Voucher');
  });

  it('ignores a stored value that is not a mode', () => {
    localStorage.setItem(KEY, 'Foo');
    expect(readPriceMode('u1', true)).toBe('Voucher');
  });

  it('survives a throwing storage', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('no');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('no');
    });
    expect(readPriceMode('u1', true)).toBe('Voucher');
    expect(() => writePriceMode('u1', 'Cost')).not.toThrow();
  });
});
