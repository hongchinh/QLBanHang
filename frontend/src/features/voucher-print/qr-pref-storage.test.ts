import { beforeEach, afterEach, describe, expect, it, vi } from 'vitest';
import { readQrPref, writeQrPref } from './qr-pref-storage';

const KEY = 'qldh:voucher-print-qr:u1';

describe('qr-pref-storage', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => vi.restoreAllMocks());

  it('defaults to on', () => {
    expect(readQrPref('u1')).toBe(true);
  });

  it('round-trips under the per-user key', () => {
    writeQrPref('u1', false);
    expect(localStorage.getItem(KEY)).toBe('0');
    expect(readQrPref('u1')).toBe(false);
    writeQrPref('u1', true);
    expect(localStorage.getItem(KEY)).toBe('1');
    expect(readQrPref('u1')).toBe(true);
  });

  it('ignores a stored value that is not 0 or 1', () => {
    localStorage.setItem(KEY, 'Foo');
    expect(readQrPref('u1')).toBe(true);
  });

  it('without a user id it is on and nothing is written', () => {
    writeQrPref(undefined, false);
    expect(localStorage.length).toBe(0);
    expect(readQrPref(undefined)).toBe(true);
  });

  it('survives a throwing storage', () => {
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('no');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('no');
    });
    expect(readQrPref('u1')).toBe(true);
    expect(() => writeQrPref('u1', false)).not.toThrow();
  });
});
