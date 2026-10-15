import type { VoucherPaymentQr } from '@/features/voucher-payment-qr/types';
import { saveBlob } from '@/lib/blob-download';
import { toast } from '@/lib/use-toast';
import { formatCurrencyVnd } from '@/lib/utils';

export async function copyText(text: string): Promise<void> {
  try {
    await navigator.clipboard.writeText(text);
    toast({ title: 'Đã sao chép', variant: 'success' });
  } catch {
    toast({ title: 'Không sao chép được', variant: 'destructive' });
  }
}

// Opens the share sheet where files can be shared (phones); otherwise, or when sharing fails, saves the file.
export async function shareOrDownload(blob: Blob, fileName: string): Promise<void> {
  const file = new File([blob], fileName, { type: 'image/png' });
  if (typeof navigator.canShare === 'function' && navigator.canShare({ files: [file] })) {
    try {
      await navigator.share({ files: [file], title: fileName.replace(/\.[^.]+$/, '') });
      return;
    } catch (error) {
      // The user closed the share sheet: nothing more to do.
      if ((error as { name?: string } | null)?.name === 'AbortError') return;
    }
  }
  saveBlob(blob, fileName);
}

export function buildQrInfoText(qr: VoucherPaymentQr): string {
  return [
    `Ngân hàng: ${qr.bankName}`,
    `Số tài khoản: ${qr.accountNumber}`,
    `Chủ tài khoản: ${qr.accountHolderName}`,
    `Số tiền: ${formatCurrencyVnd(qr.amount)} đồng`,
    `Nội dung: ${qr.content}`,
  ].join('\n');
}
