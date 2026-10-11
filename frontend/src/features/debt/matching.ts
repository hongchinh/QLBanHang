import { suggestAllocations, type AllocatableItem } from '@/features/cash-vouchers/allocation';
import { roundAwayFromZero } from '@/lib/round';
import type { SettlementItemRequest } from './types';

export interface MatchRow {
  entryId: string;
  amount: number;
}

// Settlement amounts are numeric(18,2): pair in whole cents so splits never leave a float remainder.
const toCents = (value: number) => Math.round(roundAwayFromZero(value, 2) * 100);

function centRows(rows: MatchRow[], label: string): { entryId: string; cents: number }[] {
  return rows.map((row) => {
    const cents = toCents(row.amount);
    if (cents < 0) throw new Error(`Số tiền ${label} không được âm.`);
    return { entryId: row.entryId, cents };
  });
}

/** Pairs decrease rows with increase rows in the given order (greedy), splitting amounts; Σ must be equal. */
export function pairAllocations(decreases: MatchRow[], increases: MatchRow[]): SettlementItemRequest[] {
  const left = centRows(decreases, 'chứng từ giảm');
  const right = centRows(increases, 'chứng từ tăng');
  const sum = (rows: { cents: number }[]) => rows.reduce((total, row) => total + row.cents, 0);
  if (sum(left) !== sum(right)) {
    throw new Error('Tổng số tiền đối trừ của hai bên phải bằng nhau.');
  }

  const pairs: SettlementItemRequest[] = [];
  let i = 0;
  let j = 0;
  let leftRemaining = left[0]?.cents ?? 0;
  let rightRemaining = right[0]?.cents ?? 0;
  while (i < left.length && j < right.length) {
    if (leftRemaining === 0) {
      leftRemaining = left[++i]?.cents ?? 0;
      continue;
    }
    if (rightRemaining === 0) {
      rightRemaining = right[++j]?.cents ?? 0;
      continue;
    }
    const take = Math.min(leftRemaining, rightRemaining);
    pairs.push({ increaseEntryId: right[j].entryId, decreaseEntryId: left[i].entryId, amount: take / 100 });
    leftRemaining -= take;
    rightRemaining -= take;
  }
  return pairs;
}

/** Oldest first on both sides: (dueDate ?? docDate), docDate, docCode; fills both sides up to min(Σ open left, Σ open right). */
export function suggestMatching(
  decreases: AllocatableItem[],
  increases: AllocatableItem[],
): { decreases: MatchRow[]; increases: MatchRow[] } {
  const openTotal = (items: AllocatableItem[]) =>
    items.reduce((total, item) => total + Math.max(toCents(item.open), 0), 0);
  const amount = Math.min(openTotal(decreases), openTotal(increases)) / 100;
  const toRows = (items: AllocatableItem[]): MatchRow[] =>
    amount > 0
      ? suggestAllocations(items, amount).map((row) => ({ entryId: row.entryId, amount: row.amount }))
      : [];
  return { decreases: toRows(decreases), increases: toRows(increases) };
}
