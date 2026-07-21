import { z } from 'zod';

export const bankAccountFormSchema = z.object({
  bankId: z.string().min(1, 'Chọn ngân hàng'),
  accountNumber: z.string().regex(/^[0-9]{6,19}$/, 'Số tài khoản chỉ gồm 6-19 chữ số'),
  accountName: z.string().min(1, 'Nhập tên chủ tài khoản').max(255),
});

export type BankAccountFormValues = z.infer<typeof bankAccountFormSchema>;
