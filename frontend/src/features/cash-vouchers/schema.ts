import { z } from 'zod';
import { optionalString } from '@/lib/zod-helpers';

// Empty select/autocomplete values arrive as '' and mean "not chosen".
const optionalUuid = () =>
  z
    .string()
    .uuid()
    .optional()
    .or(z.literal(''))
    .transform((v) => (v ? v : undefined));

// Same lengths and messages as the backend UpsertCashVoucherRequestValidator.
const cashLineSchema = z.object({
  // Client-only stable row key (see features/quotations/schema.ts); stripped before submitting.
  _uiKey: z.string().optional(),
  id: z.string().uuid().optional(),
  description: z.string().trim().min(1, 'Vui lòng nhập diễn giải').max(500),
  amount: z.coerce.number().gt(0, 'Số tiền phải lớn hơn 0'),
  note: optionalString(1000),
});

// One row per open item shown in the allocation grid; rows at 0 are dropped from the payload.
const cashAllocationSchema = z.object({
  entryId: z.string().uuid(),
  docCode: z.string(),
  amount: z.coerce.number().min(0, 'Số tiền đối trừ không được âm'),
});

export const cashVoucherSchema = z.object({
  type: z.enum(['Receipt', 'Payment']),
  // <input type="datetime-local"> value in local (VN) time.
  voucherAt: z.string().min(1, 'Chọn ngày giờ'),
  fundId: z.string().uuid('Chọn quỹ'),
  reasonId: z.string().uuid('Chọn lý do'),
  partnerId: optionalUuid(),
  partnerName: optionalString(255),
  partnerAddress: optionalString(1000),
  personName: optionalString(255),
  description: optionalString(500),
  attachedDocs: optionalString(255),
  note: optionalString(2000),
  // Concurrency token of the loaded voucher; never taken from a later refetch.
  version: z.number().optional(),
  lines: z.array(cashLineSchema).min(1, 'Phiếu phải có ít nhất 1 dòng'),
  allocations: z.array(cashAllocationSchema),
  // The allocations follow the suggestion until the user edits them.
  allocationsTouched: z.boolean(),
});

export type CashVoucherFormValues = z.input<typeof cashVoucherSchema>;
export type CashVoucherFormParsed = z.output<typeof cashVoucherSchema>;
export type CashLineFormValues = z.input<typeof cashLineSchema>;
export type CashAllocationFormValues = z.input<typeof cashAllocationSchema>;
