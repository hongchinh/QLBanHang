import { describe, expect, it } from 'vitest';
import { openingDebtKeys } from './keys';

describe('openingDebtKeys', () => {
  it('sits under the cash root', () => {
    expect(openingDebtKeys.all).toEqual(['cash', 'opening-debts']);
  });

  it('builds the list and partner keys', () => {
    const params = { page: 1 } as never;
    expect(openingDebtKeys.list(params)).toEqual(['cash', 'opening-debts', 'list', params]);
    expect(openingDebtKeys.partners('ab')).toEqual(['cash', 'opening-debts', 'partners', 'ab']);
  });
});
