import { useRef, useState } from 'react';
import { useQueryClient } from '@tanstack/react-query';
import { useCreateProduct, useUpdateProduct } from '@/features/products/hooks';
import { lookupsApi } from '@/features/products/api';
import { lookupKeys, productKeys } from '@/features/products/keys';
import type { ProductFormParsed } from '@/features/products/schema';
import type { CreateProductRequest, UpdateProductRequest } from '@/features/products/types';

const UUID_RE = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;

export function useSaveProduct() {
  const qc = useQueryClient();
  const createProduct = useCreateProduct();
  const updateProduct = useUpdateProduct();
  // Covers the whole save (typed-unit step + mutation): the mutations' own isPending is false during the unit step.
  const inFlight = useRef<Promise<unknown> | null>(null);
  const [saving, setSaving] = useState(false);

  // One save at a time: a repeat call while one runs gets the same promise instead of starting another request.
  function runOnce<T>(run: () => Promise<T>): Promise<T> {
    if (inFlight.current) return inFlight.current as Promise<T>;
    setSaving(true);
    const p = run().finally(() => {
      inFlight.current = null;
      setSaving(false);
    });
    inFlight.current = p;
    return p;
  }

  // A unit typed by hand (not a UUID) is created on the fly, as the product page always did.
  async function resolveUnit(parsed: ProductFormParsed): Promise<ProductFormParsed> {
    if (UUID_RE.test(parsed.unitId)) return parsed;
    const unit = await lookupsApi.getOrCreateUnit(parsed.unitId);
    qc.invalidateQueries({ queryKey: lookupKeys.units() });
    return { ...parsed, unitId: String(unit.id) };
  }

  return {
    create: (parsed: ProductFormParsed) =>
      runOnce(async () => {
        const created = await createProduct.mutateAsync(toCreatePayload(await resolveUnit(parsed)));
        // Cached "not found" searches must not hide the new product (search staleTime is 30 s).
        qc.invalidateQueries({ queryKey: productKeys.searches() });
        return created;
      }),
    update: (id: string, parsed: ProductFormParsed) =>
      runOnce(async () => updateProduct.mutateAsync({ id, data: toUpdatePayload(await resolveUnit(parsed)) })),
    isPending: saving || createProduct.isPending || updateProduct.isPending,
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

function toCreatePayload(parsed: ProductFormParsed): CreateProductRequest {
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

function toUpdatePayload(parsed: ProductFormParsed): UpdateProductRequest {
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
