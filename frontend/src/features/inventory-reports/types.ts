// Mirrors backend Inventory/Reports/Models/InventoryReportDtos.cs (Phase 06 Tasks 6.4 and 6.5).
export interface StockOnHandParams {
  /** ISO instant; omitted = now. */
  at?: string;
  warehouseId?: string;
  productGroupId?: string;
  search?: string;
}

export interface StockOnHandRow {
  productId: string;
  productCode: string;
  productName: string;
  productGroupName?: string | null;
  unitName: string;
  warehouseId: string;
  warehouseCode: string;
  warehouseName: string;
  quantity: number;
  value?: number | null;
}

export interface StockOnHandReport {
  at: string;
  isProvisional: boolean;
  canViewCost: boolean;
  totalValue?: number | null;
  rows: StockOnHandRow[];
}

export interface StockCardParams {
  productId: string;
  warehouseId?: string;
  /** VN dates `yyyy-MM-dd`. */
  from: string;
  to: string;
}

export type LedgerSourceType = 'Opening' | 'StockIn' | 'StockOut';

export interface StockCardRow {
  postedAt: string;
  sourceType: LedgerSourceType;
  sourceId: string;
  sourceCode: string;
  reasonName?: string | null;
  partnerName?: string | null;
  warehouseCode: string;
  qtyIn: number;
  qtyOut: number;
  unitCost?: number | null;
  inValue?: number | null;
  costAmount?: number | null;
  runningQty: number;
  runningValue?: number | null;
}

export interface StockCard {
  productId: string;
  productCode: string;
  productName: string;
  unitName: string;
  from: string;
  to: string;
  isProvisional: boolean;
  canViewCost: boolean;
  /** D32: warehouse filter under Branch scope — running values exist only per branch. */
  valuesAtScopeOnly: boolean;
  openingQty: number;
  openingValue?: number | null;
  inQty: number;
  outQty: number;
  inValue?: number | null;
  outValue?: number | null;
  closingQty: number;
  closingValue?: number | null;
  rows: StockCardRow[];
}
