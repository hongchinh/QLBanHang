import type { DebtEffect } from '@/features/cash-common/types';
import type { ConfirmFlags } from '@/lib/confirm-required';
import { fromDateTimeLocalValue, toDateTimeLocalValue } from '@/lib/vn-datetime';
import type { CashLineFormValues, CashVoucherFormParsed, CashVoucherFormValues } from './schema';
import type {
  CashVoucher,
  CashVoucherAllocation,
  CashVoucherDefaults,
  CashVoucherType,
  UpsertCashVoucherRequest,
} from './types';

export function toUpsertPayload(
  parsed: CashVoucherFormParsed,
  debtEffect: DebtEffect,
  flags: ConfirmFlags = {},
): UpsertCashVoucherRequest {
  return {
    type: parsed.type,
    voucherAt: fromDateTimeLocalValue(parsed.voucherAt),
    fundId: parsed.fundId,
    reasonId: parsed.reasonId,
    partnerId: parsed.partnerId,
    partnerName: parsed.partnerName,
    partnerAddress: parsed.partnerAddress,
    partnerTaxCode: parsed.partnerTaxCode,
    personName: parsed.personName,
    description: parsed.description,
    attachedDocs: parsed.attachedDocs,
    note: parsed.note,
    version: parsed.version,
    lines: parsed.lines.map((line) => ({
      id: line.id,
      description: line.description,
      amount: line.amount,
      note: line.note,
    })),
    // Always an array: a reason without debt effect clears the stored AtVoucher settlements.
    allocations:
      debtEffect === 'None'
        ? []
        : parsed.allocations
            .filter((row) => row.amount > 0)
            .map((row) => ({ entryId: row.entryId, amount: row.amount })),
    ...flags,
  };
}

// The only settlements the voucher form edits: the AtVoucher ones this voucher made. Manual, Offset and Auto
// settlements, and another voucher's AtVoucher settlement against this one, are managed elsewhere.
export function isOwnAllocation(row: CashVoucherAllocation): boolean {
  return row.origin === 'AtVoucher' && row.isOwn;
}

export function createEmptyCashLine(): CashLineFormValues {
  return { _uiKey: crypto.randomUUID(), description: '', amount: 0, note: '' };
}

// `type` is used for a new voucher only; an edit keeps the stored type.
export function toFormDefaults(
  voucher: CashVoucher | undefined,
  defaults: CashVoucherDefaults | undefined,
  type: CashVoucherType,
): CashVoucherFormValues {
  if (!voucher) {
    return {
      type,
      voucherAt: toDateTimeLocalValue(defaults?.voucherAt ?? new Date().toISOString()),
      fundId: defaults?.fundId ?? '',
      reasonId: defaults?.reasonId ?? '',
      partnerId: '',
      partnerName: '',
      partnerAddress: '',
      partnerTaxCode: '',
      personName: '',
      description: '',
      attachedDocs: '',
      note: '',
      version: undefined,
      lines: [createEmptyCashLine()],
      allocations: [],
      allocationsTouched: false,
    };
  }

  return {
    type: voucher.type,
    voucherAt: toDateTimeLocalValue(voucher.voucherAt),
    fundId: voucher.fundId,
    reasonId: voucher.reasonId,
    partnerId: voucher.partnerId ?? '',
    partnerName: voucher.partnerName ?? '',
    partnerAddress: voucher.partnerAddress ?? '',
    partnerTaxCode: voucher.partnerTaxCode ?? '',
    personName: voucher.personName ?? '',
    description: voucher.description ?? '',
    attachedDocs: voucher.attachedDocs ?? '',
    note: voucher.note ?? '',
    version: voucher.version,
    lines: voucher.lines.map((line) => ({
      _uiKey: line.id,
      id: line.id,
      description: line.description,
      amount: line.amount,
      note: line.note ?? '',
    })),
    allocations: voucher.allocations
      .filter(isOwnAllocation)
      .map((row) => ({ entryId: row.entryId, docCode: row.docCode, amount: row.amount })),
    // A stored voucher keeps its allocations until the user asks for a new suggestion.
    allocationsTouched: true,
  };
}
