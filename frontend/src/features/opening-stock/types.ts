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
  /** VN date `yyyy-MM-dd`; absent when the warehouse has no opening stock yet. */
  openingDate?: string | null;
  lines: OpeningStockLine[];
}

export interface SaveOpeningStockRequest {
  warehouseId: string;
  openingDate: string;
  acknowledgeNegativeStock: boolean;
  lines: { productId: string; quantity: number; amount: number }[];
}
