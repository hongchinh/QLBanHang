import { describe, expect, it } from 'vitest';
import { buildPaymentQrHref } from './payment-qr-link';

describe('buildPaymentQrHref', () => {
  it('builds a payment-qr link with the quotation total and code pre-filled', () => {
    expect(buildPaymentQrHref(1250000, 'BG-0001')).toBe('/qr-thanh-toan?amount=1250000&content=BG-0001');
  });

  it('rounds a non-integer total and URL-encodes the code', () => {
    expect(buildPaymentQrHref(1250000.6, 'BG/0002')).toBe('/qr-thanh-toan?amount=1250001&content=BG%2F0002');
  });
});
