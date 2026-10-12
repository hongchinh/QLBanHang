import { describe, expect, it } from 'vitest';
import { openingDebtKeys } from './keys';
import type { OpeningDebtListParams } from './types';

describe('openingDebtKeys', () => {
  it('sits under the cash root', () => {
    expect(openingDebtKeys.all).toEqual(['cash', 'opening-debts']);
  });

  it('builds the list and partner keys', () => {
    const params: OpeningDebtListParams = { page: 1, pageSize: 20 };
    expect(openingDebtKeys.list(params)).toEqual(['cash', 'opening-debts', 'list', params]);
    expect(openingDebtKeys.partners('ab')).toEqual(['cash', 'opening-debts', 'partners', 'ab']);
    expect(openingDebtKeys.owners()).toEqual(['cash', 'opening-debts', 'owners']);
  });
});
