import { z } from 'zod';

export const paymentQrSchema = z.object({
  bankId: z.string().min(1, 'Chọn ngân hàng'),
  accountNumber: z
    .string()
    .regex(/^[0-9]{6,19}$/, 'Số tài khoản chỉ gồm 6-19 chữ số'),
  accountName: z.string().min(1, 'Nhập tên chủ tài khoản').max(255),
  amount: z.coerce.number().positive('Số tiền phải lớn hơn 0').max(999_999_999_999),
  content: z.string().max(255).optional(),
});

export type PaymentQrFormValues = z.input<typeof paymentQrSchema>;
export type PaymentQrFormParsed = z.output<typeof paymentQrSchema>;
