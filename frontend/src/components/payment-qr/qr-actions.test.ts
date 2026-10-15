import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { VoucherPaymentQr } from '@/features/voucher-payment-qr/types';
import { buildQrInfoText, copyText, shareOrDownload } from './qr-actions';

const toast = vi.hoisted(() => vi.fn());
const saveBlob = vi.hoisted(() => vi.fn());
vi.mock('@/lib/use-toast', () => ({ toast }));
vi.mock('@/lib/blob-download', () => ({ saveBlob }));

function setNavigator(props: Record<string, unknown>) {
  for (const [key, value] of Object.entries(props)) {
    Object.defineProperty(navigator, key, { value, configurable: true, writable: true });
  }
}

// A touch-primary device (phone, tablet) reports a coarse pointer; a desktop reports a fine one.
function setPointer(pointer: 'coarse' | 'fine' | 'none') {
  if (pointer === 'none') {
    vi.stubGlobal('matchMedia', undefined);
    return;
  }
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({ matches: pointer === 'coarse' && query === '(pointer: coarse)' })),
  );
}

describe('qr-actions', () => {
  beforeEach(() => {
    toast.mockReset();
    saveBlob.mockReset();
    setPointer('coarse');
  });
  afterEach(() => {
    vi.unstubAllGlobals();
    setNavigator({ clipboard: undefined, share: undefined, canShare: undefined });
  });

  describe('copyText', () => {
    it('copies and confirms', async () => {
      const writeText = vi.fn().mockResolvedValue(undefined);
      setNavigator({ clipboard: { writeText } });
      await copyText('x');
      expect(writeText).toHaveBeenCalledWith('x');
      expect(toast).toHaveBeenCalledWith(expect.objectContaining({ title: 'Đã sao chép' }));
    });

    it('reports a rejected write without throwing', async () => {
      setNavigator({ clipboard: { writeText: vi.fn().mockRejectedValue(new Error('denied')) } });
      await expect(copyText('x')).resolves.toBeUndefined();
      expect(toast).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'Không sao chép được', variant: 'destructive' }),
      );
    });

    it('reports a missing clipboard without throwing', async () => {
      setNavigator({ clipboard: undefined });
      await expect(copyText('x')).resolves.toBeUndefined();
      expect(toast).toHaveBeenCalledWith(
        expect.objectContaining({ title: 'Không sao chép được', variant: 'destructive' }),
      );
    });
  });

  describe('shareOrDownload', () => {
    const blob = new Blob(['x'], { type: 'image/png' });

    it('shares the picture on a touch device that can share files', async () => {
      const share = vi.fn().mockResolvedValue(undefined);
      setNavigator({ canShare: vi.fn().mockReturnValue(true), share });
      await shareOrDownload(blob, 'QR_PT1.png');
      expect(share).toHaveBeenCalledTimes(1);
      const arg = share.mock.calls[0][0] as { files: File[]; title: string };
      expect(arg.title).toBe('QR_PT1');
      expect(arg.files).toHaveLength(1);
      expect(arg.files[0].name).toBe('QR_PT1.png');
      expect(arg.files[0].type).toBe('image/png');
      expect(saveBlob).not.toHaveBeenCalled();
    });

    it('does nothing more when the share sheet is dismissed', async () => {
      const abort = Object.assign(new Error('cancelled'), { name: 'AbortError' });
      setNavigator({ canShare: vi.fn().mockReturnValue(true), share: vi.fn().mockRejectedValue(abort) });
      await shareOrDownload(blob, 'QR_PT1.png');
      expect(saveBlob).not.toHaveBeenCalled();
    });

    it('downloads when sharing fails for another reason', async () => {
      setNavigator({
        canShare: vi.fn().mockReturnValue(true),
        share: vi.fn().mockRejectedValue(new Error('boom')),
      });
      await shareOrDownload(blob, 'QR_PT1.png');
      expect(saveBlob).toHaveBeenCalledWith(blob, 'QR_PT1.png');
    });

    it('downloads on a desktop (fine pointer) even when the browser can share files', async () => {
      setPointer('fine');
      const share = vi.fn().mockResolvedValue(undefined);
      setNavigator({ canShare: vi.fn().mockReturnValue(true), share });
      await shareOrDownload(blob, 'QR_PT1.png');
      expect(share).not.toHaveBeenCalled();
      expect(saveBlob).toHaveBeenCalledWith(blob, 'QR_PT1.png');
    });

    it('downloads when matchMedia is missing', async () => {
      setPointer('none');
      const share = vi.fn().mockResolvedValue(undefined);
      setNavigator({ canShare: vi.fn().mockReturnValue(true), share });
      await shareOrDownload(blob, 'QR_PT1.png');
      expect(share).not.toHaveBeenCalled();
      expect(saveBlob).toHaveBeenCalledWith(blob, 'QR_PT1.png');
    });

    it('downloads without canShare', async () => {
      setNavigator({ canShare: undefined, share: undefined });
      await shareOrDownload(blob, 'QR_PT1.png');
      expect(saveBlob).toHaveBeenCalledWith(blob, 'QR_PT1.png');
    });

    it('downloads when the files cannot be shared', async () => {
      const share = vi.fn();
      setNavigator({ canShare: vi.fn().mockReturnValue(false), share });
      await shareOrDownload(blob, 'QR_PT1.png');
      expect(share).not.toHaveBeenCalled();
      expect(saveBlob).toHaveBeenCalledWith(blob, 'QR_PT1.png');
    });
  });

  describe('buildQrInfoText', () => {
    it('lists the five transfer details', () => {
      const qr: VoucherPaymentQr = {
        payload: '000201',
        amount: 1_000_000,
        total: 1_000_000,
        cashPaid: 0,
        bankName: 'Vietcombank',
        bankShortName: 'VCB',
        accountNumber: '0123456789',
        accountHolderName: 'CONG TY A',
        content: 'PT000125',
        fundCode: 'NH01',
        fundName: 'Tài khoản VCB',
      };
      expect(buildQrInfoText(qr)).toBe(
        [
          'Ngân hàng: Vietcombank',
          'Số tài khoản: 0123456789',
          'Chủ tài khoản: CONG TY A',
          'Số tiền: 1.000.000 đồng',
          'Nội dung: PT000125',
        ].join('\n'),
      );
    });
  });
});
