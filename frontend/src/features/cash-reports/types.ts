import type { DebtSide, DebtSourceType, FundKind } from '@/features/cash-common/types';

// Phase 09 report DTOs (Application/CashDebt/Reports/Models/CashDebtReportDtos.cs). Dates are VN yyyy-MM-dd.

export type FundKindFilter = 'All' | 'Cash' | 'Bank';

export type CashBookSourceType = 'Receipt' | 'Payment' | 'TransferIn' | 'TransferOut';

// GET /api/reports/debt-balance (reports.debt)
export interface DebtBalanceParams {
  side: DebtSide;
  from: string;
  to: string;
  partnerId?: string;
  // Matches the partner code or name.
  search?: string;
}

// Opening and closing are split into Dư Nợ / Dư Có by the server (P24).
export interface DebtBalanceRow {
  partnerId?: string | null;
  partnerCode?: string | null;
  partnerName?: string | null;
  openingDebit: number;
  openingCredit: number;
  increase: number;
  decrease: number;
  closingDebit: number;
  closingCredit: number;
}

export interface DebtBalanceReport {
  side: DebtSide;
  from: string;
  to: string;
  // One row per partner with any non-zero value, ordered by partner code.
  rows: DebtBalanceRow[];
  // Sum of each column; the partner fields are null.
  totals: DebtBalanceRow;
}

// GET /api/reports/debt-ledger (reports.debt)
export interface DebtLedgerParams {
  side: DebtSide;
  partnerId: string;
  from: string;
  to: string;
}

export interface DebtLedgerRow {
  entryId: string;
  postedAt: string;
  docDate: string;
  docCode: string;
  sourceType: DebtSourceType;
  sourceId: string;
  description?: string | null;
  increase: number;
  decrease: number;
  // Opening balance plus Σ Increase − Σ Decrease up to and including this row.
  runningBalance: number;
}

// Opening and closing are split into Dư Nợ / Dư Có by the server (P24).
export interface DebtLedgerReport {
  side: DebtSide;
  partnerId: string;
  partnerCode: string;
  partnerName: string;
  from: string;
  to: string;
  openingBalance: number;
  openingDebit: number;
  openingCredit: number;
  rows: DebtLedgerRow[];
  totalIncrease: number;
  totalDecrease: number;
  closingBalance: number;
  closingDebit: number;
  closingCredit: number;
}

// GET /api/reports/cash-book (reports.cash). fundId set → that fund only (it must match kind unless kind is All).
export interface CashBookParams {
  kind: FundKindFilter;
  fundId?: string;
  from: string;
  to: string;
}

export interface CashBookRow {
  at: string;
  sourceType: CashBookSourceType;
  sourceId: string;
  receiptCode?: string | null;
  paymentCode?: string | null;
  transferCode?: string | null;
  // Partner name snapshot, else the person name.
  partnerOrPerson?: string | null;
  description?: string | null;
  in: number;
  out: number;
  // Group opening plus Σ In − Σ Out up to and including this row.
  running: number;
}

export interface CashBookGroup {
  fundId: string;
  fundCode: string;
  fundName: string;
  fundKind: FundKind;
  bankName?: string | null;
  accountNumber?: string | null;
  opening: number;
  rows: CashBookRow[];
  totalIn: number;
  totalOut: number;
  closing: number;
}

// grandIn / grandOut leave out transfers between two selected funds (P25).
export interface CashBookReport {
  kind: FundKindFilter;
  from: string;
  to: string;
  groups: CashBookGroup[];
  grandOpening: number;
  grandIn: number;
  grandOut: number;
  grandClosing: number;
}
