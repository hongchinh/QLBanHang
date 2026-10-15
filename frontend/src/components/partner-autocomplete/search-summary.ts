// The server returns at most this many partners per search (GET .../partners).
export const PARTNER_SEARCH_LIMIT = 20;

// What the search list calls its results: both roles on the voucher forms, customers on the quotation.
export type PartnerNoun = 'đối tượng' | 'khách hàng';

interface SummaryInput {
  count: number;
  noun: PartnerNoun;
  activeOnly: boolean;
  limit?: number;
}

// Header line of the partner search list (section-01 §5.2).
export function partnerSearchSummary({ count, noun, activeOnly, limit = PARTNER_SEARCH_LIMIT }: SummaryInput): string {
  if (count >= limit) return `Hiển thị ${limit} kết quả đầu — gõ thêm để thu hẹp`;
  const active = activeOnly ? (noun === 'đối tượng' ? ' (KH + NCC) đang dùng' : ' đang dùng') : '';
  return `Tìm thấy ${count} ${noun}${active}`;
}
