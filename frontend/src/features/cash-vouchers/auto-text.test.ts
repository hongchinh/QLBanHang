import { describe, expect, it } from 'vitest';
import { buildAutoDescription, followAuto, followHeaderInLines, isUntouchedLine } from './auto-text';

describe('buildAutoDescription (D2)', () => {
  it('joins the reason and the partner', () =>
    expect(buildAutoDescription('Thu tiền khách hàng', 'Công ty A')).toBe('Thu tiền khách hàng - Công ty A'));
  it('is the reason alone without a partner', () => {
    expect(buildAutoDescription('Thu tiền khách hàng', '')).toBe('Thu tiền khách hàng');
    expect(buildAutoDescription('Thu tiền khách hàng', '   ')).toBe('Thu tiền khách hàng');
    expect(buildAutoDescription('Thu tiền khách hàng', undefined)).toBe('Thu tiền khách hàng');
  });
  it('is empty without a reason', () => {
    expect(buildAutoDescription('', 'Công ty A')).toBe('');
    expect(buildAutoDescription(undefined, 'Công ty A')).toBe('');
    expect(buildAutoDescription(null, null)).toBe('');
  });
  it('trims both parts', () => expect(buildAutoDescription('  Thu  ', ' A ')).toBe('Thu - A'));
});

describe('followAuto (D3)', () => {
  it('fills an empty field', () => {
    expect(followAuto('', 'old', 'new')).toBe('new');
    expect(followAuto('  ', 'old', 'new')).toBe('new');
    expect(followAuto(undefined, 'old', 'new')).toBe('new');
  });
  it('follows a field that still holds the previous auto text', () =>
    expect(followAuto('old', 'old', 'new')).toBe('new'));
  it('keeps a hand-edited field', () => expect(followAuto('typed', 'old', 'new')).toBe('typed'));
});

describe('followHeaderInLines', () => {
  it('updates empty lines and lines equal to the old text, keeps edited ones', () => {
    const lines = [{ description: '' }, { description: 'old' }, { description: 'Hàng lỗi lô 05' }];
    const r = followHeaderInLines(lines, 'old', 'new');
    expect(r.changed).toBe(true);
    expect(r.lines.map((l) => l.description)).toEqual(['new', 'new', 'Hàng lỗi lô 05']);
    expect(lines[0].description).toBe('');   // the input is not mutated
  });
  it('returns the same array when nothing changes', () => {
    const lines = [{ description: 'typed' }];
    const r = followHeaderInLines(lines, 'old', 'new');
    expect(r.changed).toBe(false);
    expect(r.lines).toBe(lines);
  });
  it('does nothing when the text did not change', () => {
    const lines = [{ description: '' }];
    expect(followHeaderInLines(lines, 'same', 'same').changed).toBe(false);
  });
  it('keeps the other fields of a line', () => {
    const r = followHeaderInLines([{ description: '', amount: 5 }], '', 'new');
    expect(r.lines[0]).toEqual({ description: 'new', amount: 5 });
  });
});

describe('isUntouchedLine (section-01 §3.4)', () => {
  it('zero amount with an empty description', () => expect(isUntouchedLine({ description: '', amount: 0 }, 'Thu')).toBe(true));
  it('zero amount with the header text', () => expect(isUntouchedLine({ description: 'Thu', amount: 0 }, 'Thu')).toBe(true));
  it('an amount makes it touched', () => expect(isUntouchedLine({ description: 'Thu', amount: 5 }, 'Thu')).toBe(false));
  it('another description makes it touched', () => expect(isUntouchedLine({ description: 'Khác', amount: 0 }, 'Thu')).toBe(false));
  it('accepts string and missing amounts', () => {
    expect(isUntouchedLine({ description: '', amount: '0' }, 'Thu')).toBe(true);
    expect(isUntouchedLine({ description: '' }, 'Thu')).toBe(true);
    expect(isUntouchedLine({ description: '', amount: '1.000' }, 'Thu')).toBe(false);
  });
});
