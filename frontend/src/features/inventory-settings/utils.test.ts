import { describe, expect, it } from 'vitest';
import { formatDocumentNumber } from './utils';

describe('formatDocumentNumber', () => {
  it('formats the default pattern', () => {
    expect(formatDocumentNumber('{KH}{STT}', 'PN', 5, 12, new Date(2026, 9, 6))).toBe('PN00012');
  });

  it('formats year and month tokens', () => {
    expect(formatDocumentNumber('{KH}{NAM}{THANG}-{STT}', 'PX', 4, 7, new Date(2026, 2, 1))).toBe(
      'PX202603-0007',
    );
  });

  it('does not truncate a counter longer than the length', () => {
    expect(formatDocumentNumber('{KH}{STT}', 'PN', 3, 12345, new Date(2026, 9, 6))).toBe('PN12345');
  });
});
