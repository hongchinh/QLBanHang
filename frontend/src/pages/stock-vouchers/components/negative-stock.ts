export interface NegativeStockShortage {
  // "{productCode}@{warehouseCode}" (D17).
  key: string;
  messages: string[];
}

export function toShortages(details?: Record<string, string[]>): NegativeStockShortage[] {
  return Object.entries(details ?? {}).map(([key, messages]) => ({ key, messages }));
}
