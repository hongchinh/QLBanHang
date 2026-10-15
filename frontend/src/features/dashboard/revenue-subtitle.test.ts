import { describe, expect, it } from 'vitest';
import { revenueSubtitle } from './revenue-subtitle';

const range = { from: '2026-10-01', to: '2026-10-31' };

describe('revenueSubtitle', () => {
  it('names the branch and the period', () => {
    expect(revenueSubtitle({ branchName: 'Hà Nội', ownOnly: false, ...range })).toBe(
      'Doanh thu xuất bán của chi nhánh Hà Nội, từ 01/10/2026 đến 31/10/2026.',
    );
  });

  it('says "phiếu của tôi" for a user who sees only their own vouchers', () => {
    expect(revenueSubtitle({ branchName: 'Hà Nội', ownOnly: true, ...range })).toBe(
      'Doanh thu xuất bán (phiếu của tôi) của chi nhánh Hà Nội, từ 01/10/2026 đến 31/10/2026.',
    );
  });

  it('leaves the period out before the data is loaded', () => {
    expect(revenueSubtitle({ branchName: 'Hà Nội', ownOnly: false })).toBe('Doanh thu xuất bán của chi nhánh Hà Nội.');
  });

  it('leaves the branch out when it is not known', () => {
    expect(revenueSubtitle({ branchName: null, ownOnly: false, ...range })).toBe(
      'Doanh thu xuất bán, từ 01/10/2026 đến 31/10/2026.',
    );
    expect(revenueSubtitle({ branchName: null, ownOnly: true })).toBe('Doanh thu xuất bán (phiếu của tôi).');
  });
});
