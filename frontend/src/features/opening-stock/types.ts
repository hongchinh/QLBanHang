// Mirrors backend OpeningStocks/Models/OpeningStockDtos.cs (Phase 06 Task 6.1).
export interface OpeningStockLine {
  productId: string;
  productCode: string;
  productName: string;
  unitName: string;
  quantity: number;
  amount: number;
}

export interface OpeningStockGrid {
  warehouseId: string;
  /** The branch opening date (P8), VN date `yyyy-MM-dd`; null until it is set in Settings → Chi nhánh. */
  openingDate?: string | null;
  lines: OpeningStockLine[];
}

export interface SaveOpeningStockRequest {
  warehouseId: string;
  acknowledgeNegativeStock: boolean;
  lines: { productId: string; quantity: number; amount: number }[];
}
