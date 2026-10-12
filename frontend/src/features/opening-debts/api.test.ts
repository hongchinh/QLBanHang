import { afterEach, describe, expect, it, vi } from 'vitest';
import api from '@/lib/api-client';
import { openingDebtsApi } from './api';

describe('openingDebtsApi.remove', () => {
  afterEach(() => vi.restoreAllMocks());

  it('sends the confirmed settlement token in the query params', async () => {
    const del = vi.spyOn(api, 'delete').mockResolvedValue({ data: { success: true, timestamp: '' } });

    await openingDebtsApi.remove('o1', 11, true, 't1');

    expect(del).toHaveBeenCalledWith('/opening-debts/o1', {
      params: { version: 11, confirmRemoveSettlements: true, confirmedSettlements: 't1' },
    });
  });
});

describe('openingDebtsApi.owners', () => {
  afterEach(() => vi.restoreAllMocks());

  it('GETs the owner options', async () => {
    const get = vi.spyOn(api, 'get').mockResolvedValue({
      data: { success: true, data: [{ id: 'u1', fullName: 'A' }], timestamp: '' },
    });

    const result = await openingDebtsApi.owners();

    expect(get).toHaveBeenCalledWith('/opening-debts/owners', { params: undefined });
    expect(result).toEqual([{ id: 'u1', fullName: 'A' }]);
  });
});
