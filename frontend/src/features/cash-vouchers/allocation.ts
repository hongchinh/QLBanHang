import { roundAwayFromZero } from '@/lib/round';

export interface AllocatableItem {
  entryId: string;
  docCode: string;
  // yyyy-MM-dd (VN dates)
  docDate: string;
  dueDate: string | null;
  open: number;
}

export interface SuggestedAllocation {
  entryId: string;
  docCode: string;
  amount: number;
}

// Settlement amounts are numeric(18,2) on the server.
const round2 = (value: number) => roundAwayFromZero(value, 2);

function compareItems(a: AllocatableItem, b: AllocatableItem): number {
  // yyyy-MM-dd strings compare in date order.
  const dueA = a.dueDate ?? a.docDate;
  const dueB = b.dueDate ?? b.docDate;
  if (dueA !== dueB) return dueA < dueB ? -1 : 1;
  if (a.docDate !== b.docDate) return a.docDate < b.docDate ? -1 : 1;
  return a.docCode < b.docCode ? -1 : a.docCode > b.docCode ? 1 : 0;
}

/** Earliest (dueDate ?? docDate), then docDate, then docCode; fills until amount is used. Rounded with roundAwayFromZero. */
export function suggestAllocations(items: AllocatableItem[], amount: number): SuggestedAllocation[] {
  const rows: SuggestedAllocation[] = [];
  let remaining = round2(amount);
  for (const item of [...items].sort(compareItems)) {
    if (remaining <= 0) break;
    const take = round2(Math.min(item.open, remaining));
    if (take <= 0) continue;
    rows.push({ entryId: item.entryId, docCode: item.docCode, amount: take });
    remaining = round2(remaining - take);
  }
  return rows;
}

export function allocationTotal(rows: { amount: number }[]): number {
  return round2(rows.reduce((sum, row) => sum + row.amount, 0));
}
