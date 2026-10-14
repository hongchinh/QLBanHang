import type { StockPrintPriceMode } from './types';

const MODES: StockPrintPriceMode[] = ['Voucher', 'Cost', 'None'];
const keyFor = (userId: string) => `qldh:stock-print-price-mode:${userId}`;

// The last price mode the user printed a stock voucher with; Cost needs the inventory.view_cost permission.
export function readPriceMode(userId: string | undefined, canViewCost: boolean): StockPrintPriceMode {
  if (!userId) return 'Voucher';
  try {
    const raw = localStorage.getItem(keyFor(userId));
    const mode = MODES.find((m) => m === raw);
    if (!mode || (mode === 'Cost' && !canViewCost)) return 'Voucher';
    return mode;
  } catch {
    return 'Voucher';
  }
}

export function writePriceMode(userId: string | undefined, mode: StockPrintPriceMode): void {
  if (!userId) return;
  try {
    localStorage.setItem(keyFor(userId), mode);
  } catch {
    // storage unavailable: the choice just isn't remembered
  }
}
