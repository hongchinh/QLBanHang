import { describe, expect, it, vi } from 'vitest';
import { QueryClient } from '@tanstack/react-query';
import { supplierKeys } from '@/features/suppliers/keys';
import { invalidatePartnerQueries } from './hooks';
import { customerKeys } from './keys';

describe('invalidatePartnerQueries', () => {
  it('refreshes both catalogs and the cash and stock voucher partner searches', () => {
    const qc = new QueryClient();
    const invalidate = vi.spyOn(qc, 'invalidateQueries');

    invalidatePartnerQueries(qc);

    const keys = invalidate.mock.calls.map((c) => c[0]);
    expect(keys).toContainEqual({ queryKey: ['cash', 'cash-vouchers', 'partners'] });
    expect(keys).toContainEqual({ queryKey: ['inventory', 'stock-vouchers', 'partners'] });
    expect(keys).toContainEqual({ queryKey: customerKeys.all });
    expect(keys).toContainEqual({ queryKey: supplierKeys.all });
  });
});
