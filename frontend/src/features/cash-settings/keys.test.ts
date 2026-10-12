import { describe, expect, it } from 'vitest';
import { cashSettingsKeys } from './keys';

describe('cashSettingsKeys', () => {
  it('is a global root outside the cash root', () => {
    expect(cashSettingsKeys.all).toEqual(['cash-settings']);
  });
});
