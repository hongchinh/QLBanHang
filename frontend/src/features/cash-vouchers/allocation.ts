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

export interface AllocationCheck {
  // Voucher total − the voucher's other settlements.
  capacity: number;
  allocated: number;
  // capacity − allocated; negative when the rows use more than the voucher amount.
  remaining: number;
  overAllocated: boolean;
  // Entries whose allocation is above their open amount.
  overOpenEntryIds: Set<string>;
  // True when the server would reject the rows (a row above its open amount, or the sum above the capacity).
  invalid: boolean;
}

/** The checks the server repeats on save; the grid shows them and the form blocks saving on them. */
export function checkAllocations(
  items: { entryId: string; open: number }[],
  rows: { entryId: string; amount: number }[],
  total: number,
  otherSettled: number,
): AllocationCheck {
  const allocated = allocationTotal(rows);
  const capacity = round2(total - otherSettled);
  const remaining = round2(capacity - allocated);
  const openOf = new Map(items.map((item) => [item.entryId, item.open]));
  const overOpenEntryIds = new Set(
    rows.filter((row) => row.amount > (openOf.get(row.entryId) ?? Infinity)).map((row) => row.entryId),
  );
  const overAllocated = remaining < 0;
  return { capacity, allocated, remaining, overAllocated, overOpenEntryIds, invalid: overAllocated || overOpenEntryIds.size > 0 };
}
