import type { HelpDoc, HelpVideo } from '@/features/help/types';

const base = { sources: ['x/**'], related: [] as string[], updated: '2026-10-15', notes: [] as string[], shortcuts: [] };
export const stockOutDoc: HelpDoc = {
  ...base, key: 'stock-out.form', title: 'Phiếu xuất kho', routes: ['/stock-out/new', '/stock-out/:id'],
  purpose: 'Ghi nhận hàng xuất khỏi kho.',
  steps: [
    { id: 'reason', text: 'Chọn **Lý do xuất**.' },
    { id: 'partner', text: 'Gõ **Mã đối tượng**.' },
    { id: 'save', text: 'Bấm **Lưu tạm** (`Ctrl S`).' },
  ],
  notes: ['Phải lưu trước khi in.'], shortcuts: [{ keys: 'Ctrl S', label: 'Lưu' }], related: ['receipts.form'],
};
export const receiptsDoc: HelpDoc = { ...base, key: 'receipts.form', title: 'Phiếu thu',
  routes: ['/receipts/new', '/receipts/:id'], purpose: 'Thu tiền.', steps: stockOutDoc.steps };
export const productGroupsDoc: HelpDoc = { ...base, key: 'product-groups', title: 'Nhóm hàng hóa',
  routes: ['/product-groups'], purpose: 'Nhóm hàng.', steps: stockOutDoc.steps };
export const fixtureDocs = [stockOutDoc, receiptsDoc, productGroupsDoc];
export const fixtureVideo: HelpVideo = { youtubeId: 'vid123', title: 'Lập phiếu xuất bán hàng', duration: 155,
  recordedAt: '2026-10-20', steps: { reason: { at: 8, hash: '00000000' }, partner: { at: 21, hash: '00000000' } } };
