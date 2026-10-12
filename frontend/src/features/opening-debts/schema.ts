import { z } from 'zod';
import { optionalString } from '@/lib/zod-helpers';
import { parseMoneyInput } from '@/pages/quotations/utils/money-input';
import type { UpsertOpeningDebtRequest } from './types';

// Same rules and messages as OpeningDebtService.ValidateAsync; the server also checks the partner role
// and that docDate is on or before the branch opening date.
export const openingDebtSchema = z
  .object({
    partnerId: z.string().uuid('Chọn đối tượng'),
    side: z.enum(['Receivable', 'Payable']),
    kind: z.enum(['Debt', 'Prepayment']),
    docNo: z.string().trim().min(1, 'Vui lòng nhập số chứng từ.').max(50, 'Số chứng từ tối đa 50 ký tự.'),
    // yyyy-MM-dd values of <input type="date">; '' means empty.
    docDate: z.string().min(1, 'Chọn ngày chứng từ'),
    dueDate: z.string(),
    // Money text as typed (vi-VN grouping).
    amount: z.string().transform((text, ctx) => {
      const value = parseMoneyInput(text);
      if (value === undefined) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Nhập số tiền hợp lệ' });
        return z.NEVER;
      }
      if (value <= 0) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Số tiền phải lớn hơn 0.' });
        return z.NEVER;
      }
      // numeric(18,2) on the server; the tolerance absorbs binary rounding of value * 100.
      if (Math.abs(Math.round(value * 100) - value * 100) > 1e-6) {
        ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Số tiền tối đa 2 chữ số thập phân' });
        return z.NEVER;
      }
      return value;
    }),
    note: optionalString(500),
  })
  .superRefine((values, ctx) => {
    if (values.kind === 'Prepayment' && values.dueDate)
      ctx.addIssue({
        code: z.ZodIssueCode.custom,
        path: ['dueDate'],
        message: 'Khoản trả trước không có hạn thanh toán.',
      });
  });

export type OpeningDebtFormValues = z.input<typeof openingDebtSchema>;
export type OpeningDebtFormParsed = z.output<typeof openingDebtSchema>;

export function toUpsertOpeningDebtRequest(
  values: OpeningDebtFormParsed,
  extra: {
    ownerUserId?: string | null;
    confirmRemoveSettlements: boolean;
    confirmedSettlements?: string;
    version?: number;
  },
): UpsertOpeningDebtRequest {
  return {
    partnerId: values.partnerId,
    side: values.side,
    kind: values.kind,
    docNo: values.docNo,
    docDate: values.docDate,
    dueDate: values.kind === 'Debt' && values.dueDate ? values.dueDate : null,
    amount: values.amount,
    ...(extra.ownerUserId !== undefined ? { ownerUserId: extra.ownerUserId } : {}),
    note: values.note,
    confirmRemoveSettlements: extra.confirmRemoveSettlements,
    ...(extra.confirmedSettlements != null ? { confirmedSettlements: extra.confirmedSettlements } : {}),
    ...(extra.version !== undefined ? { version: extra.version } : {}),
  };
}
