import { describe, it, expect, vi, beforeEach } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';
import { lookupKeys, productKeys } from '@/features/products/keys';
import type { ProductFormParsed } from '@/features/products/schema';
import { useSaveProduct } from './use-save-product';

const createMutate = vi.fn();
const updateMutate = vi.fn();
const getOrCreateUnit = vi.fn();

vi.mock('@/features/products/hooks', () => ({
  useCreateProduct: () => ({ mutateAsync: createMutate, isPending: false, isError: false, error: null }),
  useUpdateProduct: () => ({ mutateAsync: updateMutate, isPending: false, isError: false, error: null }),
}));
vi.mock('@/features/products/api', () => ({
  lookupsApi: { getOrCreateUnit: (...a: unknown[]) => getOrCreateUnit(...a) },
}));

const UNIT_ID = '11111111-1111-1111-1111-111111111111';

function parsed(over: Partial<ProductFormParsed> = {}): ProductFormParsed {
  return {
    code: 'TL-50',
    name: 'Tem lăn',
    productGroupId: null,
    unitId: UNIT_ID,
    trackInventory: true,
    priceIncludesVat: false,
    purchaseDiscountRate: undefined,
    salesDiscountRate: undefined,
    ...over,
  } as unknown as ProductFormParsed;
}

function setup() {
  const qc = new QueryClient();
  const invalidate = vi.spyOn(qc, 'invalidateQueries');
  const wrapper = ({ children }: { children: ReactNode }) => (
    <QueryClientProvider client={qc}>{children}</QueryClientProvider>
  );
  const { result } = renderHook(() => useSaveProduct(), { wrapper });
  return { result, invalidate };
}

describe('useSaveProduct', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    getOrCreateUnit.mockResolvedValue({ id: 'u-new', code: 'CUON', name: 'Cuộn' });
  });

  it('create with a UUID unit skips get-or-create and returns the product', async () => {
    const product = { id: 'p1', code: 'TL-50' };
    createMutate.mockResolvedValue(product);
    const { result } = setup();
    const out = await result.current.create(parsed());
    expect(getOrCreateUnit).not.toHaveBeenCalled();
    expect(createMutate).toHaveBeenCalledWith(expect.objectContaining({ unitId: UNIT_ID }));
    expect(out).toBe(product);
  });

  it('create with a typed unit name creates the unit and invalidates units', async () => {
    createMutate.mockResolvedValue({ id: 'p1' });
    const { result, invalidate } = setup();
    await result.current.create(parsed({ unitId: 'Cuộn' }));
    expect(getOrCreateUnit).toHaveBeenCalledWith('Cuộn');
    expect(createMutate).toHaveBeenCalledWith(expect.objectContaining({ unitId: 'u-new' }));
    expect(invalidate).toHaveBeenCalledWith({ queryKey: lookupKeys.units() });
  });

  it('update defaults status to Active and sends inventory fields', async () => {
    updateMutate.mockResolvedValue({ id: 'p1' });
    const { result } = setup();
    await result.current.update('p1', parsed({ status: undefined }));
    expect(updateMutate).toHaveBeenCalledWith({
      id: 'p1',
      data: expect.objectContaining({
        status: 'Active',
        trackInventory: true,
        priceIncludesVat: false,
        purchaseDiscountRate: 0,
        salesDiscountRate: 0,
      }),
    });
  });

  it('create rejects with the mutation error', async () => {
    const err = new Error('boom');
    createMutate.mockRejectedValue(err);
    const { result } = setup();
    await expect(result.current.create(parsed())).rejects.toBe(err);
  });

  it('isPending covers the typed-unit step and a second create joins the first save', async () => {
    let resolveUnit!: (u: { id: string }) => void;
    getOrCreateUnit.mockReturnValue(new Promise((r) => (resolveUnit = r)));
    createMutate.mockResolvedValue({ id: 'p1' });
    const { result } = setup();
    expect(result.current.isPending).toBe(false);

    let first!: Promise<unknown>;
    let second!: Promise<unknown>;
    act(() => {
      first = result.current.create(parsed({ unitId: 'Cuộn' }));
      second = result.current.create(parsed({ unitId: 'Cuộn' }));
    });
    expect(result.current.isPending).toBe(true);
    expect(second).toBe(first);

    await act(async () => {
      resolveUnit({ id: 'u-new' });
      await first;
    });
    expect(getOrCreateUnit).toHaveBeenCalledTimes(1);
    expect(createMutate).toHaveBeenCalledTimes(1);
    expect(result.current.isPending).toBe(false);
  });

  it('a failed save clears the guard so the next save runs', async () => {
    createMutate.mockRejectedValueOnce(new Error('boom')).mockResolvedValueOnce({ id: 'p1' });
    const { result } = setup();
    await expect(result.current.create(parsed())).rejects.toThrow('boom');
    await expect(result.current.create(parsed())).resolves.toEqual({ id: 'p1' });
    expect(createMutate).toHaveBeenCalledTimes(2);
  });

  it('a successful create invalidates cached product searches', async () => {
    createMutate.mockResolvedValue({ id: 'p1' });
    const { result, invalidate } = setup();
    await result.current.create(parsed());
    expect(invalidate).toHaveBeenCalledWith({ queryKey: productKeys.searches() });
    expect(productKeys.searches()).toEqual(['products', 'search']);
  });
});
