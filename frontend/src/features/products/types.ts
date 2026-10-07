import type { PagedResult } from '@/features/customers/types';

export type ProductStatus = 'Active' | 'Inactive';

export type PricingMode = 'PerUnit' | 'PerSquareMeter' | 'PerLinearMeter' | 'PerCubicMeter';

export interface LookupItem {
  id: string;
  code: string;
  name: string;
}

export interface Product {
  id: string;
  code: string;
  name: string;
  productGroupId?: string;
  productGroupCode?: string;
  productGroupName?: string;
  unitId?: string;
  unitCode?: string;
  unitName?: string;
  length?: number;
  width?: number;
  thickness?: number;
  density?: number;
  specification?: string;
  defaultPrice?: number;
  costPrice?: number;
  defaultTaxRate?: number;
  note?: string;
  status: ProductStatus;
  pricingMode: PricingMode;
  trackInventory: boolean;
  purchaseDiscountRate: number;
  salesDiscountRate: number;
  priceIncludesVat: boolean;
  /** Ledger rows exist: pricingMode, unitId, trackInventory and priceIncludesVat are locked. */
  hasInventoryActivity: boolean;
  createdAt: string;
}

export interface ProductListItem {
  id: string;
  code: string;
  name: string;
  productGroupName?: string;
  unitName?: string;
  specification?: string;
  defaultPrice?: number;
  costPrice?: number;
  status: ProductStatus;
  pricingMode: PricingMode;
  trackInventory: boolean;
  defaultTaxRate?: number;
  length?: number;
  width?: number;
  thickness?: number;
  purchaseDiscountRate: number;
  salesDiscountRate: number;
  priceIncludesVat: boolean;
}

export interface ProductListParams {
  page?: number;
  pageSize?: number;
  search?: string;
  productGroupId?: string;
  unitId?: string;
  status?: ProductStatus;
  sortBy?: string;
  sortDirection?: 'asc' | 'desc';
}

export interface CreateProductRequest {
  code?: string;
  name: string;
  productGroupId: string;
  unitId: string;
  length?: number;
  width?: number;
  thickness?: number;
  density?: number;
  specification?: string;
  defaultPrice?: number;
  costPrice?: number;
  defaultTaxRate?: number;
  note?: string;
  pricingMode: PricingMode;
  trackInventory: boolean;
  purchaseDiscountRate: number;
  salesDiscountRate: number;
  priceIncludesVat: boolean;
}

export interface UpdateProductRequest extends Omit<CreateProductRequest, 'code'> {
  status: ProductStatus;
}

export interface ProductSuggestion {
  id: string;
  code: string;
  name: string;
  specification?: string;
  unitName?: string;
  pricingMode: PricingMode;
  defaultPrice?: number;
  costPrice?: number;
  // Required (possibly undefined) so the compiler flags every place that builds a suggestion.
  defaultTaxRate: number | undefined;
  length: number | undefined;
  width: number | undefined;
  thickness: number | undefined;
  trackInventory: boolean;
  purchaseDiscountRate: number;
  salesDiscountRate: number;
  priceIncludesVat: boolean;
}

export type { PagedResult };
