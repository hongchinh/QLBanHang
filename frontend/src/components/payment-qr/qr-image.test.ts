import { afterEach, describe, expect, it, vi } from 'vitest';
import { canvasToBlob, composeQrImage } from './qr-image';

function recordingContext() {
  const calls = { fillText: [] as string[], drawImage: [] as unknown[][], fillRect: [] as number[][] };
  const ctx = {
    font: '',
    fillStyle: '',
    textAlign: '',
    textBaseline: '',
    fillRect: vi.fn((...a: number[]) => calls.fillRect.push(a)),
    fillText: vi.fn((text: string) => calls.fillText.push(text)),
    drawImage: vi.fn((...a: unknown[]) => calls.drawImage.push(a)),
  };
  return { ctx, calls };
}

describe('composeQrImage', () => {
  afterEach(() => {
    vi.restoreAllMocks();
  });

  it('draws the QR, the amount, the content and the bank line on a 600 x 760 canvas', () => {
    const { ctx, calls } = recordingContext();
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
      ctx as unknown as CanvasRenderingContext2D,
    );
    const qr = document.createElement('canvas');
    const out = composeQrImage(qr, {
      amount: 1_000_000,
      content: 'PT000125',
      bankLine: 'Vietcombank · 0123456789',
    });
    expect(out.width).toBe(600);
    expect(out.height).toBe(760);
    expect(calls.fillText).toEqual(
      expect.arrayContaining(['1.000.000 đồng', 'PT000125', 'Vietcombank · 0123456789']),
    );
    expect(calls.drawImage).toHaveLength(1);
    expect(calls.drawImage[0][0]).toBe(qr);
  });
});

describe('canvasToBlob', () => {
  it('resolves the blob', async () => {
    const blob = new Blob(['x'], { type: 'image/png' });
    const canvas = { toBlob: (cb: (b: Blob | null) => void) => cb(blob) } as unknown as HTMLCanvasElement;
    await expect(canvasToBlob(canvas)).resolves.toBe(blob);
  });

  it('rejects when there is no blob', async () => {
    const canvas = { toBlob: (cb: (b: Blob | null) => void) => cb(null) } as unknown as HTMLCanvasElement;
    await expect(canvasToBlob(canvas)).rejects.toThrow();
  });
});
