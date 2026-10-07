import { z } from 'zod';
import { computePricingQuantity } from '@/lib/pricing-quantity';
import { roundAwayFromZero } from '@/lib/round';
import { optionalNumber, optionalString } from '@/lib/zod-helpers';

// Empty select/autocomplete values arrive as '' and mean "not chosen".
const optionalUuid = () =>
  z
    .string()
    .uuid()
    .optional()
    .or(z.literal(''))
    .transform((v) => (v ? v : undefined));

const DIMENSION_LABELS = {
  sheetCount: 'Số tấm',
  length: 'Chiều dài',
  width: 'Chiều rộng',
  thickness: 'Chiều dày',
} as const;

const stockLineSchema = z
  .object({
    // Client-only stable row key (see features/quotations/schema.ts); stripped before submitting.
    _uiKey: z.string().optional(),
    id: z.string().uuid().optional(),
    sortOrder: z.number().int().nonnegative(),
    productId: z.string().uuid('Chọn hàng hóa'),
    productCode: z.string(),
    productName: z.string(),
    pricingMode: z.enum(['PerUnit', 'PerSquareMeter', 'PerLinearMeter', 'PerCubicMeter']),
    unitName: z.string(),
    trackInventory: z.boolean(),
    priceIncludesVat: z.boolean(),
    warehouseId: z.string().uuid('Chọn kho'),
    sheetCount: optionalNumber(),
    length: optionalNumber(),
    width: optionalNumber(),
    thickness: optionalNumber(),
    quantity: z.coerce.number(),
    unitPrice: z.coerce.number().min(0, 'Đơn giá không được âm'),
    discountRate: z.coerce.number().min(0, 'Từ 0 đến 100').max(100, 'Từ 0 đến 100'),
    discountAmount: optionalNumber(),
    discountManual: z.boolean(),
    vatRate: z.coerce.number().min(0, 'Từ 0 đến 100').max(100, 'Từ 0 đến 100'),
    note: optionalString(1000),
  })
  .superRefine((line, ctx) => {
    // Same required dimensions and paths as the backend StockVoucherService.
    const required: (keyof typeof DIMENSION_LABELS)[] = [];
    if (line.pricingMode !== 'PerUnit') required.push('sheetCount', 'length');
    if (line.pricingMode === 'PerSquareMeter' || line.pricingMode === 'PerCubicMeter') required.push('width');
    if (line.pricingMode === 'PerCubicMeter') required.push('thickness');

    let dimensionsValid = true;
    for (const field of required) {
      const value = line[field];
      if (value === undefined || value <= 0) {
        dimensionsValid = false;
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: `${DIMENSION_LABELS[field]} phải lớn hơn 0`,
          path: [field],
        });
      }
    }
    if (!dimensionsValid) return;

    const quantity = roundAwayFromZero(computePricingQuantity(line), 6);
    if (quantity <= 0) {
      ctx.addIssue({ code: z.ZodIssueCode.custom, message: 'Số lượng phải lớn hơn 0', path: ['quantity'] });
      return;
    }
    if (line.discountManual && line.discountAmount !== undefined) {
      const amount = roundAwayFromZero(quantity * line.unitPrice, 0);
      if (line.discountAmount < 0 || line.discountAmount > amount) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: 'Tiền CK không vượt quá số tiền',
          path: ['discountAmount'],
        });
      }
    }
  });

export const stockVoucherSchema = z.object({
  // <input type="datetime-local"> value in local (VN) time.
  voucherAt: z.string().min(1, 'Chọn ngày giờ'),
  warehouseId: z.string().uuid('Chọn kho'),
  reasonId: z.string().uuid('Chọn lý do'),
  partnerId: optionalUuid(),
  partnerName: optionalString(255),
  partnerAddress: optionalString(1000),
  partnerTaxCode: optionalString(20),
  handlerName: optionalString(255),
  paymentMethodId: optionalUuid(),
  note: optionalString(2000),
  freight: z.coerce.number().min(0, 'Phí vận chuyển không được âm'),
  orderDiscount: z.coerce.number().min(0, 'Chiết khấu không được âm'),
  paidAmount: optionalNumber({ min: 0 }),
  // paidAmount follows the total until the user edits it.
  paidAmountTouched: z.boolean(),
  // Concurrency token of the loaded voucher (D29); never taken from a later refetch.
  version: z.number().optional(),
  lines: z.array(stockLineSchema).min(1, 'Phiếu phải có ít nhất 1 dòng'),
});

export type StockVoucherFormValues = z.input<typeof stockVoucherSchema>;
export type StockVoucherFormParsed = z.output<typeof stockVoucherSchema>;
export type StockLineFormValues = z.input<typeof stockLineSchema>;
export type StockLineFormParsed = z.output<typeof stockLineSchema>;
