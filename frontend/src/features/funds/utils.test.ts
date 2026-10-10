import { describe, expect, it } from 'vitest';
import { defaultFund, selectableFunds } from './utils';
import type { Fund } from './types';

function fund(overrides: Partial<Fund> & Pick<Fund, 'id' | 'code' | 'kind'>): Fund {
  return {
    branchId: 'b1',
    name: overrides.code,
    bankName: null,
    accountNumber: null,
    isDefault: false,
    openingAmount: 0,
    isActive: true,
    ...overrides,
  };
}

const FUNDS: Fund[] = [
  fund({ id: 'f3', code: 'VCB', kind: 'Bank', isDefault: true }),
  fund({ id: 'f2', code: 'QTM2', kind: 'Cash', isActive: false }),
  fund({ id: 'f1', code: 'QTM', kind: 'Cash', isDefault: true }),
  fund({ id: 'f4', code: 'ACB', kind: 'Bank' }),
  fund({ id: 'f5', code: 'BIDV', kind: 'Bank', isActive: false }),
];

describe('selectableFunds', () => {
  it('selectable funds keep the inactive selection', () => {
    expect(selectableFunds(FUNDS, 'Bank').map((f) => f.code)).toEqual(['ACB', 'VCB']);
    expect(selectableFunds(FUNDS, 'Bank', 'f5').map((f) => f.code)).toEqual(['ACB', 'BIDV', 'VCB']);
    // An inactive selection of another kind is not added.
    expect(selectableFunds(FUNDS, 'Cash', 'f5').map((f) => f.code)).toEqual(['QTM']);
    // Without a kind: every active fund plus the selection, by code.
    expect(selectableFunds(FUNDS, undefined, 'f2').map((f) => f.code)).toEqual(['ACB', 'QTM', 'QTM2', 'VCB']);
    expect(selectableFunds(FUNDS, 'Cash', null).map((f) => f.code)).toEqual(['QTM']);
  });
});

describe('defaultFund', () => {
  it('default fund per kind', () => {
    expect(defaultFund(FUNDS, 'Cash')?.id).toBe('f1');
    expect(defaultFund(FUNDS, 'Bank')?.id).toBe('f3');
    expect(defaultFund(FUNDS, 'None')).toBeUndefined();
    expect(defaultFund([fund({ id: 'x', code: 'X', kind: 'Bank' })], 'Bank')).toBeUndefined();
  });
});
