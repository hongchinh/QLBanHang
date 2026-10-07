import { z } from 'zod';
import { optionalNumber, optionalString } from '@/lib/zod-helpers';

export const productSchema = z.object({
  code: optionalString(50),
  name: z.string().min(1, 'Tên hàng hóa là bắt buộc').max(255),
  productGroupId: z.string().min(1, 'Chọn nhóm hàng hóa').uuid('Nhóm hàng hóa không hợp lệ'),
  unitId: z.string().min(1, 'Nhập đơn vị tính'),
  specification: optionalString(500),
  length: optionalNumber({ min: 0 }),
  width: optionalNumber({ min: 0 }),
  thickness: optionalNumber({ min: 0 }),
  density: optionalNumber({ min: 0 }),
  defaultPrice: optionalNumber({ min: 0 }),
  costPrice: optionalNumber({ min: 0 }),
  defaultTaxRate: optionalNumber({ min: 0, max: 100 }),
  note: optionalString(2000),
  status: z.enum(['Active', 'Inactive']).optional(),
  pricingMode: z.enum(['PerUnit', 'PerSquareMeter', 'PerLinearMeter', 'PerCubicMeter']),
  trackInventory: z.boolean().default(true),
  priceIncludesVat: z.boolean().default(false),
  purchaseDiscountRate: optionalNumber({ min: 0, max: 100 }),
  salesDiscountRate: optionalNumber({ min: 0, max: 100 }),
});

export type ProductFormValues = z.input<typeof productSchema>;
export type ProductFormParsed = z.output<typeof productSchema>;
