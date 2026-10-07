// String unions mirror the backend enums (serialized by name).
export type CostingMethod = 'PeriodicAverage' | 'Fifo';
export type CostingPeriod = 'Month' | 'Quarter' | 'Year';
export type CostingScope = 'Branch' | 'Warehouse';
export type NegativeStockPolicy = 'Allow' | 'Warn' | 'Block';
export type DefaultDateMode = 'Now' | 'PreviousVoucher';
export type DocumentType = 'StockIn' | 'StockOut';
export type NumberingResetPolicy = 'None' | 'Monthly' | 'Yearly';

export interface InventorySettings {
  costingMethod: CostingMethod;
  costingPeriod: CostingPeriod;
  costingScope: CostingScope;
  purchaseCostIncludesVat: boolean;
  negativeStockPolicy: NegativeStockPolicy;
  netExcludesVat: boolean;
  defaultDateMode: DefaultDateMode;
  updatedAt?: string;
}

export type UpdateInventorySettingsRequest = Omit<InventorySettings, 'updatedAt'>;

export interface DocumentNumbering {
  docType: DocumentType;
  prefix: string;
  length: number;
  resetPolicy: NumberingResetPolicy;
  pattern: string;
}

export type UpdateNumberingRequest = Omit<DocumentNumbering, 'docType'>;

export interface RecalcCostRequest {
  /** DateOnly `yyyy-MM-dd` — the start of a costing period. */
  fromPeriodStart: string;
  warehouseId?: string;
  productId?: string;
}

export interface RecalcCostResult {
  fromPeriodStart: string;
  /** Number of (product, costing scope) pairs recalculated. */
  scopeCount: number;
}
