import { afterEach, describe, expect, it, vi } from 'vitest';
import { canvasToBlob, composeQrImage } from './qr-image';

function recordingContext() {
  const calls = {
    fillText: [] as string[],
    fonts: [] as string[], // the font in force at each fillText
    drawImage: [] as unknown[][],
    smoothing: [] as boolean[], // imageSmoothingEnabled in force at each drawImage
    fillRect: [] as { rect: number[]; fillStyle: string }[],
  };
  const ctx = {
    font: '',
    fillStyle: '',
    textAlign: '',
    textBaseline: '',
    imageSmoothingEnabled: true,
    fillRect: vi.fn((...a: number[]) => calls.fillRect.push({ rect: a, fillStyle: ctx.fillStyle })),
    fillText: vi.fn((text: string) => {
      calls.fillText.push(text);
      calls.fonts.push(ctx.font);
    }),
    drawImage: vi.fn((...a: unknown[]) => {
      calls.drawImage.push(a);
      calls.smoothing.push(ctx.imageSmoothingEnabled);
    }),
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

  it('paints a white background first', () => {
    const { ctx, calls } = recordingContext();
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
      ctx as unknown as CanvasRenderingContext2D,
    );
    composeQrImage(document.createElement('canvas'), { amount: 1, content: 'a', bankLine: 'b' });
    expect(calls.fillRect[0]).toEqual({ rect: [0, 0, 600, 760], fillStyle: '#ffffff' });
  });

  it('draws the QR without smoothing and the amount in bold 40px', () => {
    const { ctx, calls } = recordingContext();
    vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockReturnValue(
      ctx as unknown as CanvasRenderingContext2D,
    );
    composeQrImage(document.createElement('canvas'), { amount: 1_000_000, content: 'a', bankLine: 'b' });
    expect(calls.smoothing).toEqual([false]);
    expect(calls.fonts[calls.fillText.indexOf('1.000.000 đồng')]).toMatch(/^bold 40px /);
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
