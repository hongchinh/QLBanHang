import { fromDateTimeLocalValue } from '@/lib/vn-datetime';
import type { CreateDebtOffsetRequest, DebtOffsetAllocation } from './types';

export interface DebtOffsetFormInput {
  // <input type="datetime-local"> value in local (VN) time.
  voucherAt: string;
  partnerId: string;
  note: string;
  receivables: DebtOffsetAllocation[];
  payables: DebtOffsetAllocation[];
}

const withAmount = (rows: DebtOffsetAllocation[]) =>
  rows.filter((row) => row.amount > 0).map((row) => ({ entryId: row.entryId, amount: row.amount }));

// Only rows with an amount are sent; the server computes the offset amount from them.
export function toCreatePayload(input: DebtOffsetFormInput): CreateDebtOffsetRequest {
  const note = input.note.trim();
  return {
    voucherAt: fromDateTimeLocalValue(input.voucherAt),
    partnerId: input.partnerId,
    note: note || undefined,
    receivables: withAmount(input.receivables),
    payables: withAmount(input.payables),
  };
}
