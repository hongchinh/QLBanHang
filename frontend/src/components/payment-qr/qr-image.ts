import { formatCurrencyVnd } from '@/lib/utils';

export interface QrImageText {
  amount: number;
  content: string;
  bankLine: string;
}

const WIDTH = 600;
const HEIGHT = 760;
const MARGIN = 24;
const FONT_FAMILY = 'system-ui, -apple-system, "Segoe UI", Roboto, Arial, sans-serif';

// The picture that is shared or saved: the QR on white with the amount, the transfer content and the bank under it.
export function composeQrImage(qrCanvas: HTMLCanvasElement, text: QrImageText): HTMLCanvasElement {
  const canvas = document.createElement('canvas');
  canvas.width = WIDTH;
  canvas.height = HEIGHT;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Canvas is not supported');

  ctx.fillStyle = '#ffffff';
  ctx.fillRect(0, 0, WIDTH, HEIGHT);

  const qrSize = WIDTH - MARGIN * 2;
  ctx.drawImage(qrCanvas, MARGIN, MARGIN, qrSize, qrSize);

  ctx.fillStyle = '#111827';
  ctx.textAlign = 'center';
  ctx.textBaseline = 'alphabetic';
  ctx.font = `bold 40px ${FONT_FAMILY}`;
  ctx.fillText(`${formatCurrencyVnd(text.amount)} đồng`, WIDTH / 2, 640);
  ctx.font = `24px ${FONT_FAMILY}`;
  ctx.fillText(text.content, WIDTH / 2, 690);
  ctx.fillText(text.bankLine, WIDTH / 2, 730);
  return canvas;
}

export function canvasToBlob(canvas: HTMLCanvasElement): Promise<Blob> {
  return new Promise((resolve, reject) => {
    canvas.toBlob((blob) => (blob ? resolve(blob) : reject(new Error('Cannot create the picture'))), 'image/png');
  });
}
