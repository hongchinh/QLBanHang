export function buildPaymentQrHref(total: number, code: string): string {
  return `/qr-thanh-toan?amount=${Math.round(total)}&content=${encodeURIComponent(code)}`;
}
