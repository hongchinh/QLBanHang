const keyFor = (userId: string) => `qldh:voucher-print-qr:${userId}`;

// Whether a receipt / stock-out prints with the transfer QR; on until the user turns it off.
export function readQrPref(userId: string | undefined): boolean {
  if (!userId) return true;
  try {
    return localStorage.getItem(keyFor(userId)) !== '0';
  } catch {
    return true;
  }
}

export function writeQrPref(userId: string | undefined, on: boolean): void {
  if (!userId) return;
  try {
    localStorage.setItem(keyFor(userId), on ? '1' : '0');
  } catch {
    // storage unavailable: the choice just isn't remembered
  }
}
