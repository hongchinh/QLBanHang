import { describe, expect, it } from 'vitest';
import { buildPaymentQrHref } from './quotation-form-page';

// quotation-form-page.tsx's full render tree (auth store, router, quotation hooks, line-items
// grid) is already covered by quotation-form-page.draft.test.tsx, which mocks isEdit=false /
// initial=undefined for its draft-restore scenarios. Standing up an isEdit=true + loaded-`initial`
// render here would mean re-deriving that entire mock surface for one link, so this test targets
// only the pure URL-building logic behind the "Tạo QR thanh toán" link.
describe('buildPaymentQrHref', () => {
  it('builds a payment-qr link with the quotation total and code pre-filled', () => {
    expect(buildPaymentQrHref(1250000, 'BG-0001')).toBe('/qr-thanh-toan?amount=1250000&content=BG-0001');
  });

  it('rounds a non-integer total and URL-encodes the code', () => {
    expect(buildPaymentQrHref(1250000.6, 'BG/0002')).toBe('/qr-thanh-toan?amount=1250001&content=BG%2F0002');
  });
});
