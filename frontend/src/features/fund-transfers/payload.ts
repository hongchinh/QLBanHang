import type { ConfirmFlags } from '@/lib/confirm-required';
import { formatMoneyForDisplay } from '@/lib/money-input';
import { fromDateTimeLocalValue, toDateTimeLocalValue } from '@/lib/vn-datetime';
import type { FundTransferFormParsed, FundTransferFormValues } from './schema';
import type { FundTransfer, FundTransferDefaults, UpsertFundTransferRequest } from './types';

// A transfer can only raise the negative-fund confirmation, so that is the one flag sent.
export function toUpsertPayload(values: FundTransferFormParsed, flags: ConfirmFlags = {}): UpsertFundTransferRequest {
  return {
    voucherAt: fromDateTimeLocalValue(values.voucherAt),
    fromFundId: values.fromFundId,
    toFundId: values.toFundId,
    amount: values.amount,
    description: values.description,
    note: values.note,
    version: values.version,
    acknowledgeNegativeFund: flags.acknowledgeNegativeFund ?? false,
  };
}

export function toFormDefaults(transfer?: FundTransfer, defaults?: FundTransferDefaults): FundTransferFormValues {
  if (!transfer) {
    return {
      voucherAt: toDateTimeLocalValue(defaults?.voucherAt ?? new Date().toISOString()),
      fromFundId: '',
      toFundId: '',
      amount: '',
      description: '',
      note: '',
      version: undefined,
    };
  }
  return {
    voucherAt: toDateTimeLocalValue(transfer.voucherAt),
    fromFundId: transfer.fromFundId,
    toFundId: transfer.toFundId,
    amount: formatMoneyForDisplay(transfer.amount),
    description: transfer.description ?? '',
    note: transfer.note ?? '',
    version: transfer.version,
  };
}
