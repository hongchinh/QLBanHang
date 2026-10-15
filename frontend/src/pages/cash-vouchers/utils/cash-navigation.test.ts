import { describe, expect, it } from 'vitest';
import { CASH_HEADER_FIELD_IDS, cashLineEnterTarget } from './cash-navigation';

describe('CASH_HEADER_FIELD_IDS (section-01 §2.1)', () => {
  it('is the ten header fields in Enter and DOM order', () =>
    expect([...CASH_HEADER_FIELD_IDS]).toEqual([
      'cash-voucher-at', 'cash-fund', 'cash-partner', 'cash-partner-name', 'cash-reason',
      'cash-person-name', 'cash-partner-address', 'cash-description', 'cash-attached-docs', 'cash-voucher-note',
    ]));
});

describe('cashLineEnterTarget (section-01 §2.2)', () => {
  const go = (field: 'description' | 'amount' | 'note', index: number, count: number, shift = false) =>
    cashLineEnterTarget(field, index, count, shift);

  it('Enter: description -> amount -> note of the same row', () => {
    expect(go('description', 0, 2)).toEqual({ kind: 'cell', field: 'amount', index: 0 });
    expect(go('amount', 0, 2)).toEqual({ kind: 'cell', field: 'note', index: 0 });
  });
  it('Enter on a note goes to the description of the next row', () =>
    expect(go('note', 0, 2)).toEqual({ kind: 'cell', field: 'description', index: 1 }));
  it('Enter on the note of the last row appends a row', () =>
    expect(go('note', 1, 2)).toEqual({ kind: 'append' }));
  it('Shift+Enter walks back inside a row', () => {
    expect(go('note', 1, 2, true)).toEqual({ kind: 'cell', field: 'amount', index: 1 });
    expect(go('amount', 1, 2, true)).toEqual({ kind: 'cell', field: 'description', index: 1 });
  });
  it('Shift+Enter on a description goes to the note of the row above', () =>
    expect(go('description', 1, 2, true)).toEqual({ kind: 'cell', field: 'note', index: 0 }));
  it('Shift+Enter on the first description goes to the header note', () =>
    expect(go('description', 0, 2, true)).toEqual({ kind: 'header-note' }));
  it('an index out of range gives none', () => {
    expect(go('note', 2, 2)).toEqual({ kind: 'none' });
    expect(go('description', -1, 2, true)).toEqual({ kind: 'none' });
  });
});
