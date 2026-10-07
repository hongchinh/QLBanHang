import { describe, expect, it } from 'vitest';
import { customerSchema } from './schema';

describe('customerSchema', () => {
  it('requires name', () => {
    const result = customerSchema.safeParse({ name: '', group: 'Company' });
    expect(result.success).toBe(false);
  });

  it('rejects invalid email', () => {
    const result = customerSchema.safeParse({
      name: 'ACME',
      group: 'Company',
      email: 'not-an-email',
    });
    expect(result.success).toBe(false);
  });

  it('accepts empty optional strings as undefined', () => {
    const result = customerSchema.safeParse({
      name: 'ACME',
      group: 'Company',
      code: '',
      email: '',
      note: '',
    });
    expect(result.success).toBe(true);
    if (result.success) {
      expect(result.data.code).toBeUndefined();
      expect(result.data.email).toBeUndefined();
      expect(result.data.note).toBeUndefined();
    }
  });

  it('rejects oversized name', () => {
    const result = customerSchema.safeParse({
      name: 'a'.repeat(256),
      group: 'Company',
    });
    expect(result.success).toBe(false);
  });

  it('rejects both roles false', () => {
    const result = customerSchema.safeParse({
      name: 'ACME',
      group: 'Company',
      isCustomer: false,
      isSupplier: false,
    });
    expect(result.success).toBe(false);
    if (!result.success) {
      expect(result.error.issues[0].message).toBe('Chọn ít nhất một vai trò');
    }
  });

  it('rejects unknown group', () => {
    const result = customerSchema.safeParse({
      name: 'ACME',
      group: 'Unknown',
    });
    expect(result.success).toBe(false);
  });
});
