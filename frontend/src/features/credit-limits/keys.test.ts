import { describe, expect, it } from 'vitest';
import { creditLimitKeys } from './keys';

describe('creditLimitKeys', () => {
  it('sits under the cash root', () => {
    expect(creditLimitKeys.all).toEqual(['cash', 'credit-limits']);
  });

  it('builds the list key from the params', () => {
    const params = { page: 2 } as never;
    expect(creditLimitKeys.list(params)).toEqual(['cash', 'credit-limits', 'list', params]);
  });
});
