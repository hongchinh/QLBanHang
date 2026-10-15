import { describe, expect, it } from 'vitest';
import { PARTNER_SEARCH_LIMIT, partnerSearchSummary } from './search-summary';

describe('partnerSearchSummary (section-01 §5.2)', () => {
  it('form: both roles, active only', () =>
    expect(partnerSearchSummary({ count: 3, noun: 'đối tượng', activeOnly: true })).toBe('Tìm thấy 3 đối tượng (KH + NCC) đang dùng'));
  it('quotation: customers, active only', () =>
    expect(partnerSearchSummary({ count: 3, noun: 'khách hàng', activeOnly: true })).toBe('Tìm thấy 3 khách hàng đang dùng'));
  it('filter: no "đang dùng"', () =>
    expect(partnerSearchSummary({ count: 3, noun: 'đối tượng', activeOnly: false })).toBe('Tìm thấy 3 đối tượng'));
  it('zero results', () =>
    expect(partnerSearchSummary({ count: 0, noun: 'đối tượng', activeOnly: true })).toBe('Tìm thấy 0 đối tượng (KH + NCC) đang dùng'));
  it('says so when the server limit is reached', () => {
    expect(PARTNER_SEARCH_LIMIT).toBe(20);
    expect(partnerSearchSummary({ count: 20, noun: 'đối tượng', activeOnly: true })).toBe('Hiển thị 20 kết quả đầu — gõ thêm để thu hẹp');
  });
  it('an explicit limit wins', () =>
    expect(partnerSearchSummary({ count: 5, noun: 'đối tượng', activeOnly: false, limit: 5 })).toBe('Hiển thị 5 kết quả đầu — gõ thêm để thu hẹp'));
});
