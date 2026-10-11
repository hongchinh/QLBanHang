import { z } from 'zod';
import { optionalString } from '@/lib/zod-helpers';
import { parseMoneyInput } from '@/pages/quotations/utils/money-input';

// The amount input holds display text ("1.500.000"); a number is accepted too.
const amountSchema = z.union([z.string(), z.number()]).transform((value, ctx) => {
  const n = typeof value === 'number' ? value : parseMoneyInput(value);
  if (n === undefined) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Nhập số tiền hợp lệ' });
    return z.NEVER;
  }
  if (n <= 0) {
    ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Số tiền phải lớn hơn 0' });
    return z.NEVER;
  }
  return n;
});

export const fundTransferSchema = z
  .object({
    // <input type="datetime-local"> value in local (VN) time.
    voucherAt: z.string().min(1, 'Chọn ngày giờ'),
    fromFundId: z.string().uuid('Chọn quỹ đi'),
    toFundId: z.string().uuid('Chọn quỹ đến'),
    amount: amountSchema,
    description: optionalString(500),
    note: optionalString(2000),
    // Concurrency token of the loaded transfer (D29); never taken from a later refetch.
    version: z.number().optional(),
  })
  .refine((v) => v.fromFundId !== v.toFundId, { message: 'Quỹ đến phải khác quỹ đi', path: ['toFundId'] });

export type FundTransferFormValues = z.input<typeof fundTransferSchema>;
export type FundTransferFormParsed = z.output<typeof fundTransferSchema>;
