import { describe, expect, it } from 'vitest';
import type { AllocatableItem } from '@/features/cash-vouchers/allocation';
import { pairAllocations, suggestMatching } from './matching';

function item(entryId: string, docDate: string, dueDate: string | null, open: number): AllocatableItem {
  return { entryId, docCode: entryId.toUpperCase(), docDate, dueDate, open };
}

describe('pairAllocations', () => {
  it('pairs one decrease across two increases', () => {
    expect(
      pairAllocations(
        [{ entryId: 'D', amount: 3_000 }],
        [
          { entryId: 'I1', amount: 1_000 },
          { entryId: 'I2', amount: 2_000 },
        ],
      ),
    ).toEqual([
      { increaseEntryId: 'I1', decreaseEntryId: 'D', amount: 1_000 },
      { increaseEntryId: 'I2', decreaseEntryId: 'D', amount: 2_000 },
    ]);
  });

  it('pairs two decreases into one increase', () => {
    expect(
      pairAllocations(
        [
          { entryId: 'D1', amount: 400 },
          { entryId: 'D2', amount: 600.5 },
        ],
        [{ entryId: 'I', amount: 1_000.5 }],
      ),
    ).toEqual([
      { increaseEntryId: 'I', decreaseEntryId: 'D1', amount: 400 },
      { increaseEntryId: 'I', decreaseEntryId: 'D2', amount: 600.5 },
    ]);
  });

  it('splits rows on both sides in the given order', () => {
    expect(
      pairAllocations(
        [
          { entryId: 'D1', amount: 1_500 },
          { entryId: 'D2', amount: 1_500 },
        ],
        [
          { entryId: 'I1', amount: 1_000 },
          { entryId: 'I2', amount: 2_000 },
        ],
      ),
    ).toEqual([
      { increaseEntryId: 'I1', decreaseEntryId: 'D1', amount: 1_000 },
      { increaseEntryId: 'I2', decreaseEntryId: 'D1', amount: 500 },
      { increaseEntryId: 'I2', decreaseEntryId: 'D2', amount: 1_500 },
    ]);
  });

  it('skips rows without an amount', () => {
    expect(
      pairAllocations(
        [
          { entryId: 'D0', amount: 0 },
          { entryId: 'D1', amount: 100 },
        ],
        [
          { entryId: 'I0', amount: 0 },
          { entryId: 'I1', amount: 100 },
        ],
      ),
    ).toEqual([{ increaseEntryId: 'I1', decreaseEntryId: 'D1', amount: 100 }]);
  });

  it('keeps cent amounts exact', () => {
    const pairs = pairAllocations(
      [
        { entryId: 'D1', amount: 0.1 },
        { entryId: 'D2', amount: 0.2 },
      ],
      [{ entryId: 'I1', amount: 0.3 }],
    );

    expect(pairs).toEqual([
      { increaseEntryId: 'I1', decreaseEntryId: 'D1', amount: 0.1 },
      { increaseEntryId: 'I1', decreaseEntryId: 'D2', amount: 0.2 },
    ]);
  });

  it('throws when sums differ', () => {
    expect(() =>
      pairAllocations([{ entryId: 'D', amount: 1_000 }], [{ entryId: 'I', amount: 900 }]),
    ).toThrow();
  });
});

describe('suggestMatching', () => {
  it('suggestion fills the smaller side fully, oldest first', () => {
    const decreases = [
      item('d2', '2026-10-08', null, 500),
      item('d1', '2026-10-03', null, 700),
    ];
    const increases = [
      item('i3', '2026-10-01', '2026-10-30', 1_000),
      item('i2', '2026-10-05', null, 400),
      item('i1', '2026-10-02', '2026-10-04', 300),
    ];

    expect(suggestMatching(decreases, increases)).toEqual({
      decreases: [
        { entryId: 'd1', amount: 700 },
        { entryId: 'd2', amount: 500 },
      ],
      increases: [
        { entryId: 'i1', amount: 300 },
        { entryId: 'i2', amount: 400 },
        { entryId: 'i3', amount: 500 },
      ],
    });
  });

  it('breaks ties on doc date, then doc code', () => {
    const increases = [
      item('i9', '2026-10-02', '2026-10-10', 10),
      item('i8', '2026-10-01', '2026-10-10', 10),
      item('i7', '2026-10-02', '2026-10-10', 10),
    ];

    const { increases: rows } = suggestMatching([item('d1', '2026-10-01', null, 30)], increases);

    expect(rows.map((row) => row.entryId)).toEqual(['i8', 'i7', 'i9']);
  });

  it('suggests nothing when one side has no open amount', () => {
    expect(suggestMatching([], [item('i1', '2026-10-01', null, 100)])).toEqual({
      decreases: [],
      increases: [],
    });
  });

  it('suggested rows pair without a remainder', () => {
    const { decreases, increases } = suggestMatching(
      [item('d1', '2026-10-01', null, 1_234.56)],
      [item('i1', '2026-10-01', null, 1_000), item('i2', '2026-10-02', null, 1_000)],
    );

    expect(pairAllocations(decreases, increases)).toEqual([
      { increaseEntryId: 'i1', decreaseEntryId: 'd1', amount: 1_000 },
      { increaseEntryId: 'i2', decreaseEntryId: 'd1', amount: 234.56 },
    ]);
  });
});
