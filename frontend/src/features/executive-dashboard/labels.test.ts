import { describe, expect, it } from 'vitest';
import { codeName, formatAmount, withBranch } from './labels';

describe('codeName', () => {
  it('joins the code and the name with an en dash', () => {
    expect(codeName('KH001', 'CT Xây dựng An Phát')).toBe('KH001 – CT Xây dựng An Phát');
  });

  it('prints the name alone when the code is null', () => {
    expect(codeName(null, 'CT Xây dựng An Phát')).toBe('CT Xây dựng An Phát');
  });

  it('prints the code alone when the name is null', () => {
    expect(codeName('KH001', null)).toBe('KH001');
  });
});

describe('withBranch', () => {
  it('puts the branch code in front', () => {
    expect(withBranch('CN01', 'KHO01 – Kho chính')).toBe('CN01 · KHO01 – Kho chính');
  });

  it('leaves the label alone without a branch code', () => {
    expect(withBranch(null, 'KHO01 – Kho chính')).toBe('KHO01 – Kho chính');
    expect(withBranch(undefined, 'KHO01')).toBe('KHO01');
  });
});

describe('formatAmount', () => {
  it('prints the full number', () => {
    expect(formatAmount(412_000_000)).toBe('412.000.000');
    expect(formatAmount(0)).toBe('0');
    expect(formatAmount(-1_500)).toBe('-1.500');
  });

  it('prints "—" for null', () => {
    expect(formatAmount(null)).toBe('—');
  });
});
