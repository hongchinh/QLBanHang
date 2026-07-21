import { describe, expect, it } from 'vitest';
import { paymentQrSchema } from './schema';

describe('paymentQrSchema', () => {
  it('accepts a valid payload', () => {
    const result = paymentQrSchema.safeParse({
      bankId: 'bank-1',
      accountNumber: '0123456789',
      accountName: 'Nguyen Van A',
      amount: 150000,
      content: 'TT DH-0001',
    });
    expect(result.success).toBe(true);
  });

  it('rejects a non-numeric account number', () => {
    const result = paymentQrSchema.safeParse({
      bankId: 'bank-1',
      accountNumber: 'abc',
      accountName: 'Nguyen Van A',
      amount: 150000,
    });
    expect(result.success).toBe(false);
  });

  it('rejects a zero amount', () => {
    const result = paymentQrSchema.safeParse({
      bankId: 'bank-1',
      accountNumber: '0123456789',
      accountName: 'Nguyen Van A',
      amount: 0,
    });
    expect(result.success).toBe(false);
  });
});
