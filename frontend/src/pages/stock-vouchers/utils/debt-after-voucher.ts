import type { DebtEffect } from '@/features/cash-common/types';

export interface VoucherDebtImpact {
  effect: DebtEffect;
  total: number;
  paid: number;
}

export interface DebtAfterVoucher {
  receivableAfter: number;
  payableAfter: number;
  // creditLimit − receivableAfter; null without a limit.
  availableAfter: number | null;
  receivableChanged: boolean;
  payableChanged: boolean;
}

interface Balances {
  receivableBalance: number;
  payableBalance: number;
  creditLimit?: number | null;
}

// The unpaid part moves the debt; the paid part goes through the automatic cash voucher.
function delta({ effect, total, paid }: VoucherDebtImpact) {
  const net = total - paid;
  return {
    receivable: effect === 'ReceivableIncrease' ? net : effect === 'ReceivableDecrease' ? -net : 0,
    payable: effect === 'PayableIncrease' ? net : effect === 'PayableDecrease' ? -net : 0,
  };
}

const touchesReceivable = (e?: DebtEffect) => e === 'ReceivableIncrease' || e === 'ReceivableDecrease';
const touchesPayable = (e?: DebtEffect) => e === 'PayableIncrease' || e === 'PayableDecrease';

// `saved`: the stored impact of the voucher being edited, already inside the server balances.
export function debtAfterVoucher(
  balances: Balances,
  current: VoucherDebtImpact,
  saved?: VoucherDebtImpact,
): DebtAfterVoucher {
  const now = delta(current);
  const before = saved ? delta(saved) : { receivable: 0, payable: 0 };
  const receivableAfter = balances.receivableBalance + now.receivable - before.receivable;
  return {
    receivableAfter,
    payableAfter: balances.payableBalance + now.payable - before.payable,
    availableAfter: balances.creditLimit == null ? null : balances.creditLimit - receivableAfter,
    receivableChanged: touchesReceivable(current.effect) || touchesReceivable(saved?.effect),
    payableChanged: touchesPayable(current.effect) || touchesPayable(saved?.effect),
  };
}

interface SavedVoucher {
  status: string;
  partnerId?: string | null;
  reasonDebtEffect: DebtEffect;
  total: number;
  paidAmount: number;
}

// The impact the server balance already holds: only an Active voucher counts, and only for its own partner.
export function savedImpactOf(
  voucher: SavedVoucher | undefined,
  selectedPartnerId: string | undefined,
): VoucherDebtImpact | undefined {
  if (!voucher || voucher.status !== 'Active' || !selectedPartnerId || voucher.partnerId !== selectedPartnerId) {
    return undefined;
  }
  return { effect: voucher.reasonDebtEffect, total: voucher.total, paid: voucher.paidAmount };
}
