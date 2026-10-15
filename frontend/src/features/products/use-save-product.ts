import { useQueryClient } from '@tanstack/react-query';
import { useCreateProduct, useUpdateProduct } from '@/features/products/hooks';
import { lookupsApi } from '@/features/products/api';
import { lookupKeys, productKeys } from '@/features/products/keys';
import type { ProductFormParsed } from '@/features/products/schema';
import type { CreateProductRequest, UpdateProductRequest } from '@/features/products/types';

export const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function useSaveProduct() {
  const qc = useQueryClient();
  const createProduct = useCreateProduct();
  const updateProduct = useUpdateProduct();

  // A unit typed by hand (not a UUID) is created on the fly, as the product page always did.
  async function resolveUnit(parsed: ProductFormParsed): Promise<ProductFormParsed> {
    if (UUID_RE.test(parsed.unitId)) return parsed;
    const unit = await lookupsApi.getOrCreateUnit(parsed.unitId);
    qc.invalidateQueries({ queryKey: lookupKeys.units() });
    return { ...parsed, unitId: String(unit.id) };
  }

  return {
    create: async (parsed: ProductFormParsed) => {
      const created = await createProduct.mutateAsync(toCreatePayload(await resolveUnit(parsed)));
      // Cached "not found" searches must not hide the new product (search staleTime is 30 s).
      qc.invalidateQueries({ queryKey: productKeys.searches() });
      return created;
    },
    update: async (id: string, parsed: ProductFormParsed) =>
      updateProduct.mutateAsync({ id, data: toUpdatePayload(await resolveUnit(parsed)) }),
    isPending: createProduct.isPending || updateProduct.isPending,
    error: createProduct.error ?? updateProduct.error,
    isError: createProduct.isError || updateProduct.isError,
    reset: () => {
      createProduct.reset?.();
      updateProduct.reset?.();
    },
  };
}

function toInventoryPayload(parsed: ProductFormParsed) {
  return {
    trackInventory: parsed.trackInventory,
    priceIncludesVat: parsed.priceIncludesVat,
    purchaseDiscountRate: parsed.purchaseDiscountRate ?? 0,
    salesDiscountRate: parsed.salesDiscountRate ?? 0,
  };
}

export function toCreatePayload(parsed: ProductFormParsed): CreateProductRequest {
  return {
    code: parsed.code,
    name: parsed.name,
    productGroupId: parsed.productGroupId,
    unitId: parsed.unitId,
    specification: parsed.specification,
    length: parsed.length,
    width: parsed.width,
    thickness: parsed.thickness,
    density: parsed.density,
    defaultPrice: parsed.defaultPrice,
    costPrice: parsed.costPrice,
    defaultTaxRate: parsed.defaultTaxRate,
    note: parsed.note,
    pricingMode: parsed.pricingMode,
    ...toInventoryPayload(parsed),
  };
}

export function toUpdatePayload(parsed: ProductFormParsed): UpdateProductRequest {
  return {
    name: parsed.name,
    productGroupId: parsed.productGroupId,
    unitId: parsed.unitId,
    specification: parsed.specification,
    length: parsed.length,
    width: parsed.width,
    thickness: parsed.thickness,
    density: parsed.density,
    defaultPrice: parsed.defaultPrice,
    costPrice: parsed.costPrice,
    defaultTaxRate: parsed.defaultTaxRate,
    note: parsed.note,
    status: parsed.status ?? 'Active',
    pricingMode: parsed.pricingMode,
    ...toInventoryPayload(parsed),
  };
}
