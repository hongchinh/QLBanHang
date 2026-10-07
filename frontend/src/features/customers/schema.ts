import { z } from 'zod';
import { optionalEmail, optionalString } from '@/lib/zod-helpers';

// One partner catalog with two roles (customer / supplier); only the name message differs.
function createPartnerSchema(nameRequiredMessage: string) {
  return z
    .object({
      code: optionalString(50),
      name: z.string().min(1, nameRequiredMessage).max(255),
      taxCode: optionalString(20),
      companyAddress: optionalString(1000),
      defaultShippingAddress: optionalString(1000),
      contactPerson: optionalString(255),
      phoneNumber: optionalString(30),
      email: optionalEmail(),
      group: z.enum(['Company', 'Agent', 'Retail', 'Project']),
      note: optionalString(2000),
      status: z.enum(['Active', 'Inactive']).optional(),
      isCustomer: z.boolean().default(true),
      isSupplier: z.boolean().default(false),
    })
    .refine((v) => v.isCustomer || v.isSupplier, {
      message: 'Chọn ít nhất một vai trò',
      path: ['isCustomer'],
    });
}

export const customerSchema = createPartnerSchema('Tên khách hàng là bắt buộc');
export const supplierSchema = createPartnerSchema('Tên nhà cung cấp là bắt buộc');

export type CustomerFormValues = z.input<typeof customerSchema>;
export type CustomerFormParsed = z.output<typeof customerSchema>;
