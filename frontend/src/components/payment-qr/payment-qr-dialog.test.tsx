import { forwardRef } from 'react';
import { AxiosError } from 'axios';
import { MemoryRouter } from 'react-router-dom';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import type { VoucherPaymentQr } from '@/features/voucher-payment-qr/types';
import { ApiCallError } from '@/lib/api-client';
import { PaymentQrDialog } from './payment-qr-dialog';
import { buildQrInfoText } from './qr-actions';

const mocks = vi.hoisted(() => ({
  useVoucherPaymentQr: vi.fn(),
  hasPermission: vi.fn(),
  copyText: vi.fn(),
  shareOrDownload: vi.fn(),
  composeQrImage: vi.fn(),
  canvasToBlob: vi.fn(),
  toast: vi.fn(),
}));

vi.mock('qrcode.react', () => ({
  QRCodeCanvas: forwardRef<
    HTMLCanvasElement,
    { value: string; size: number; level: string; marginSize: number; boostLevel?: boolean }
  >(({ value, size, level, marginSize, boostLevel }, ref) => (
    <canvas
      ref={ref}
      data-testid="qr"
      data-value={value}
      data-level={level}
      data-margin={marginSize}
      data-boost={String(boostLevel)}
      width={size}
    />
  )),
}));
vi.mock('@/features/voucher-payment-qr/hooks', () => ({ useVoucherPaymentQr: mocks.useVoucherPaymentQr }));
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: typeof mocks.hasPermission }) => unknown) =>
    selector({ hasPermission: mocks.hasPermission }),
}));
vi.mock('./qr-actions', async (importOriginal) => {
  const actual: Record<string, unknown> = await importOriginal();
  return { ...actual, copyText: mocks.copyText, shareOrDownload: mocks.shareOrDownload };
});
vi.mock('@/lib/use-toast', () => ({ toast: mocks.toast }));
vi.mock('./qr-image', () => ({ composeQrImage: mocks.composeQrImage, canvasToBlob: mocks.canvasToBlob }));

const qr: VoucherPaymentQr = {
  payload: '00020101021238570010A000000727',
  amount: 1_000_000,
  total: 1_500_000,
  cashPaid: 500_000,
  bankName: 'Ngân hàng TMCP Ngoại thương Việt Nam',
  bankShortName: 'Vietcombank',
  accountNumber: '0123456789',
  accountHolderName: 'CONG TY ABC',
  content: 'PX000318',
  fundCode: 'NH01',
  fundName: 'Tài khoản Vietcombank',
  sourceStockVoucherCode: null,
};

function stubMatchMedia(matches: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
  );
}

function hookResult(overrides: Record<string, unknown> = {}) {
  return { data: qr, isLoading: false, isFetching: false, isError: false, error: null, refetch: vi.fn(), ...overrides };
}

function renderDialog(open = true) {
  return render(
    <MemoryRouter>
      <PaymentQrDialog kind="stock" id="v1" voucherCode="PT000125" open={open} onOpenChange={vi.fn()} />
    </MemoryRouter>,
  );
}

function withError(error: unknown) {
  mocks.useVoucherPaymentQr.mockReturnValue(hookResult({ data: undefined, isError: true, error }));
}

describe('PaymentQrDialog', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    stubMatchMedia(true);
    mocks.hasPermission.mockReturnValue(false);
    mocks.useVoucherPaymentQr.mockReturnValue(hookResult());
  });
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it('shows the qr and the details', () => {
    renderDialog();
    const canvas = screen.getByTestId('qr');
    expect(canvas).toHaveAttribute('data-value', qr.payload);
    expect(canvas).toHaveAttribute('data-level', 'M');
    expect(canvas).toHaveAttribute('data-margin', '4');
    expect(canvas).toHaveAttribute('data-boost', 'false');
    expect(canvas).toHaveAttribute('width', '264');
    expect(screen.getByText('1.000.000')).toBeInTheDocument();
    expect(screen.getByText('QR chuyển khoản')).toBeInTheDocument();
    expect(screen.getByText('PT000125')).toBeInTheDocument();
    const row = (label: string) => screen.getByText(label).parentElement as HTMLElement;
    expect(row('Ngân hàng')).toHaveTextContent('Ngân hàng TMCP Ngoại thương Việt Nam');
    expect(row('Số tài khoản')).toHaveTextContent('0123456789');
    expect(row('Chủ tài khoản')).toHaveTextContent('CONG TY ABC');
    expect(row('Nội dung')).toHaveTextContent('PX000318');
    expect(row('Quỹ nhận')).toHaveTextContent('Tài khoản Vietcombank (NH01)');
  });

  it('shows the breakdown only with cash paid', () => {
    const { unmount } = renderDialog();
    expect(screen.getByText('Tổng phiếu 1.500.000 − đã trả tiền mặt 500.000')).toBeInTheDocument();
    unmount();
    mocks.useVoucherPaymentQr.mockReturnValue(hookResult({ data: { ...qr, cashPaid: 0 } }));
    renderDialog();
    expect(screen.queryByText(/đã trả tiền mặt/)).not.toBeInTheDocument();
  });

  it('shows the automatic receipt line', () => {
    mocks.useVoucherPaymentQr.mockReturnValue(hookResult({ data: { ...qr, sourceStockVoucherCode: 'PX000318' } }));
    const { unmount } = renderDialog();
    expect(screen.getByText('Phiếu thu tự động theo PX000318')).toBeInTheDocument();
    unmount();
    mocks.useVoucherPaymentQr.mockReturnValue(hookResult());
    renderDialog();
    expect(screen.queryByText(/Phiếu thu tự động theo/)).not.toBeInTheDocument();
  });

  it('copy buttons', async () => {
    const user = userEvent.setup();
    renderDialog();
    const rowOf = (label: string) => screen.getByText(label).parentElement as HTMLElement;
    await user.click(within(rowOf('Số tài khoản')).getByRole('button', { name: 'Sao chép số tài khoản' }));
    expect(mocks.copyText).toHaveBeenLastCalledWith('0123456789');
    await user.click(within(rowOf('Nội dung')).getByRole('button', { name: 'Sao chép nội dung' }));
    expect(mocks.copyText).toHaveBeenLastCalledWith('PX000318');
    await user.click(screen.getByRole('button', { name: 'Sao chép thông tin' }));
    expect(mocks.copyText).toHaveBeenLastCalledWith(buildQrInfoText(qr));
  });

  it('download', async () => {
    const blob = new Blob(['x'], { type: 'image/png' });
    const composed = document.createElement('canvas');
    mocks.composeQrImage.mockReturnValue(composed);
    mocks.canvasToBlob.mockResolvedValue(blob);
    const user = userEvent.setup();
    renderDialog();
    await user.click(screen.getByRole('button', { name: 'Tải ảnh QR' }));
    await waitFor(() => expect(mocks.shareOrDownload).toHaveBeenCalledWith(blob, 'QR_PT000125.png'));
    expect(mocks.composeQrImage).toHaveBeenCalledWith(screen.getByTestId('qr'), {
      amount: 1_000_000,
      content: 'PX000318',
      bankLine: 'Vietcombank · 0123456789',
    });
    expect(mocks.canvasToBlob).toHaveBeenCalledWith(composed);
  });

  it('tells the user when the picture cannot be made', async () => {
    mocks.composeQrImage.mockReturnValue(document.createElement('canvas'));
    mocks.canvasToBlob.mockRejectedValue(new Error('no picture'));
    const user = userEvent.setup();
    renderDialog();
    await user.click(screen.getByRole('button', { name: 'Tải ảnh QR' }));
    await waitFor(() =>
      expect(mocks.toast).toHaveBeenCalledWith({ title: 'Không tạo được ảnh QR', variant: 'destructive' }),
    );
    expect(mocks.shareOrDownload).not.toHaveBeenCalled();
  });

  describe('422 message', () => {
    it('reads the message from the real transport shape', () => {
      withError(
        new AxiosError('Request failed', 'ERR_BAD_REQUEST', undefined, undefined, {
          status: 422,
          data: { success: false, error: { code: 'PAYMENT_QR_UNAVAILABLE', message: 'Phiếu đã hủy.' } },
        } as never),
      );
      renderDialog();
      expect(screen.getByText('Phiếu đã hủy.')).toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Thử lại' })).not.toBeInTheDocument();
    });

    it('shows the message without a link', () => {
      withError(new ApiCallError({ code: 'PAYMENT_QR_UNAVAILABLE', message: 'Phiếu đã hủy.' }));
      mocks.hasPermission.mockReturnValue(true);
      renderDialog();
      expect(screen.getByText('Phiếu đã hủy.')).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'Mở màn Quỹ' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Thử lại' })).not.toBeInTheDocument();
      expect(screen.queryByTestId('qr')).not.toBeInTheDocument();
    });

    it.each([
      'Quỹ NH01 chưa khai ngân hàng, số tài khoản hoặc chủ tài khoản.',
      'Chi nhánh chưa có quỹ ngân hàng mặc định đủ thông tin QR.',
    ])('links to the funds screen for "%s" with the permission', (message) => {
      withError(new ApiCallError({ code: 'PAYMENT_QR_UNAVAILABLE', message }));
      mocks.hasPermission.mockImplementation((p: string) => p === 'cash.catalogs.manage');
      const { unmount } = renderDialog();
      expect(screen.getByText(message)).toBeInTheDocument();
      expect(screen.getByRole('link', { name: 'Mở màn Quỹ' })).toHaveAttribute('href', '/funds');
      unmount();

      mocks.hasPermission.mockReturnValue(false);
      renderDialog();
      expect(screen.getByText(message)).toBeInTheDocument();
      expect(screen.queryByRole('link', { name: 'Mở màn Quỹ' })).not.toBeInTheDocument();
    });
  });

  it('other errors', async () => {
    const refetch = vi.fn();
    mocks.useVoucherPaymentQr.mockReturnValue(
      hookResult({ data: undefined, isError: true, error: new Error('Network Error'), refetch }),
    );
    const user = userEvent.setup();
    renderDialog();
    expect(screen.getByText('Network Error')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(refetch).toHaveBeenCalledTimes(1);
  });

  it('loading', () => {
    mocks.useVoucherPaymentQr.mockReturnValue(hookResult({ data: undefined, isLoading: true, isFetching: true }));
    renderDialog();
    expect(document.querySelector('[data-icon="loading"]')).toBeInTheDocument();
    expect(screen.queryByTestId('qr')).not.toBeInTheDocument();
  });

  it('hides the previous answer while the server is asked again', () => {
    mocks.useVoucherPaymentQr.mockReturnValue(hookResult({ isFetching: true }));
    renderDialog();
    expect(document.querySelector('[data-icon="loading"]')).toBeInTheDocument();
    expect(screen.getByRole('img', { name: 'Đang tải' })).toBeInTheDocument();
    expect(screen.queryByTestId('qr')).not.toBeInTheDocument();
    expect(screen.queryByText('1.000.000')).not.toBeInTheDocument();
  });

  it.each([
    ['dialog', true],
    ['sheet', false],
  ])('keeps the action buttons outside the scrolling body (%s)', (_name, wide) => {
    stubMatchMedia(wide);
    renderDialog();
    const scroll = screen.getByTestId('payment-qr-scroll');
    expect(scroll).toContainElement(screen.getByTestId('qr'));
    expect(scroll).toHaveClass('overflow-y-auto', 'min-h-0');
    const actions = screen.getByTestId('payment-qr-actions');
    expect(scroll).not.toContainElement(actions);
    expect(actions).toHaveClass('shrink-0');
    expect(actions).toContainElement(screen.getByRole('button', { name: 'Sao chép thông tin' }));
    expect(actions).toContainElement(screen.getByRole('button', { name: 'Tải ảnh QR' }));
  });

  it('dialog or sheet', () => {
    const { unmount } = renderDialog();
    expect(screen.getByTestId('payment-qr-dialog')).toBeInTheDocument();
    expect(screen.queryByTestId('payment-qr-sheet')).not.toBeInTheDocument();
    unmount();
    stubMatchMedia(false);
    renderDialog();
    expect(screen.getByTestId('payment-qr-sheet')).toBeInTheDocument();
    expect(screen.queryByTestId('payment-qr-dialog')).not.toBeInTheDocument();
    expect(screen.getByTestId('qr')).toBeInTheDocument();
  });

  it('asks the server on every opening', () => {
    renderDialog(true);
    expect(mocks.useVoucherPaymentQr).toHaveBeenLastCalledWith('stock', 'v1', { enabled: true });
    renderDialog(false);
    expect(mocks.useVoucherPaymentQr).toHaveBeenLastCalledWith('stock', 'v1', { enabled: false });
    expect(screen.getAllByTestId('qr')).toHaveLength(1);
  });
});
