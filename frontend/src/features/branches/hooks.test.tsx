import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { customerKeys } from '@/features/customers/keys';
import { productGroupKeys } from '@/features/product-groups/keys';
import { lookupKeys, productKeys } from '@/features/products/keys';
import { supplierKeys } from '@/features/suppliers/keys';
import { warehouseKeys } from '@/features/warehouses/keys';
import { branchKeys } from './keys';
import { useDeleteDemoBranch } from './hooks';

vi.mock('./api', () => ({
  branchesApi: {
    removeDemo: vi.fn().mockResolvedValue({}),
  },
}));

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const invalidate = vi.spyOn(qc, 'invalidateQueries');
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  }
  return { wrapper, invalidate };
}

describe('useDeleteDemoBranch', () => {
  // The demo delete hard-deletes the DM- products and partners and the DM product group,
  // so cached catalog lists and pickers must refetch.
  it('invalidates the branch and catalog key roots after a delete', async () => {
    const { wrapper, invalidate } = setup();
    const { result } = renderHook(() => useDeleteDemoBranch(), { wrapper });

    result.current.mutate('b1');

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    const keys = invalidate.mock.calls.map(([filters]) => filters?.queryKey);
    expect(keys).toEqual(
      expect.arrayContaining([
        branchKeys.lists(),
        branchKeys.me(),
        productKeys.all,
        customerKeys.all,
        supplierKeys.all,
        warehouseKeys.all,
        productGroupKeys.all,
        lookupKeys.productGroups(),
      ]),
    );
  });
});
