import { describe, expect, it } from 'vitest';
import { creditLimitKeys } from './keys';
import type { CreditLimitListParams } from './types';

describe('creditLimitKeys', () => {
  it('sits under the cash root', () => {
    expect(creditLimitKeys.all).toEqual(['cash', 'credit-limits']);
  });

  it('builds the list key from the params', () => {
    const params: CreditLimitListParams = { page: 2, pageSize: 20 };
    expect(creditLimitKeys.list(params)).toEqual(['cash', 'credit-limits', 'list', params]);
  });
});
