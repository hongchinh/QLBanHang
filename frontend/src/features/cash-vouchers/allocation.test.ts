import { describe, expect, it } from 'vitest';
import { allocationTotal, suggestAllocations, type AllocatableItem } from './allocation';

function item(docCode: string, docDate: string, dueDate: string | null, open: number): AllocatableItem {
  return { entryId: `e-${docCode}`, docCode, docDate, dueDate, open };
}

describe('suggestAllocations', () => {
  it('suggests earliest due first', () => {
    const items = [
      item('PX03', '2026-10-01', '2026-10-20', 300),
      item('PX02', '2026-10-15', null, 200),
      item('PX01', '2026-10-05', '2026-10-10', 100),
    ];

    expect(suggestAllocations(items, 1_000)).toEqual([
      { entryId: 'e-PX01', docCode: 'PX01', amount: 100 },
      { entryId: 'e-PX02', docCode: 'PX02', amount: 200 },
      { entryId: 'e-PX03', docCode: 'PX03', amount: 300 },
    ]);
  });

  it('breaks ties on doc date, then doc code', () => {
    const items = [
      item('PX09', '2026-10-02', '2026-10-10', 10),
      item('PX08', '2026-10-01', '2026-10-10', 10),
      item('PX07', '2026-10-02', '2026-10-10', 10),
    ];

    expect(suggestAllocations(items, 30).map((row) => row.docCode)).toEqual(['PX08', 'PX07', 'PX09']);
  });

  it('stops when the amount is used', () => {
    const items = [
      item('PX01', '2026-10-01', '2026-10-10', 1_000),
      item('PX02', '2026-10-01', '2026-10-20', 2_000),
      item('PX03', '2026-10-01', '2026-10-30', 3_000),
    ];

    expect(suggestAllocations(items, 2_500)).toEqual([
      { entryId: 'e-PX01', docCode: 'PX01', amount: 1_000 },
      { entryId: 'e-PX02', docCode: 'PX02', amount: 1_500 },
    ]);
  });

  it('zero amount gives no rows', () => {
    const items = [item('PX01', '2026-10-01', '2026-10-10', 1_000)];

    expect(suggestAllocations(items, 0)).toEqual([]);
    expect(suggestAllocations(items, -5)).toEqual([]);
  });

  it('skips items without an open amount and rounds to 2 decimals', () => {
    const items = [
      item('PX01', '2026-10-01', '2026-10-05', 0),
      item('PX02', '2026-10-01', '2026-10-10', 0.1),
      item('PX03', '2026-10-01', '2026-10-20', 0.2),
    ];

    // 0.3 − 0.1 is 0.19999999999999998 in binary floating point.
    expect(suggestAllocations(items, 0.3)).toEqual([
      { entryId: 'e-PX02', docCode: 'PX02', amount: 0.1 },
      { entryId: 'e-PX03', docCode: 'PX03', amount: 0.2 },
    ]);
  });
});

describe('allocationTotal', () => {
  it('sums the rows', () => {
    expect(allocationTotal([{ amount: 1_000 }, { amount: 1_500 }])).toBe(2_500);
    expect(allocationTotal([])).toBe(0);
    expect(allocationTotal([{ amount: 0.1 }, { amount: 0.2 }])).toBe(0.3);
  });
});
