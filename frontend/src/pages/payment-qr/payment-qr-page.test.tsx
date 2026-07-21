import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { PaymentQrPage } from './payment-qr-page';
import { banksApi } from '@/features/banks/api';
import { paymentQrApi } from '@/features/payment-qr/api';

vi.mock('@/features/banks/api', () => ({ banksApi: { list: vi.fn() } }));
vi.mock('@/features/payment-qr/api', () => ({ paymentQrApi: { generate: vi.fn() } }));

function renderPage(initialEntries = ['/qr-thanh-toan']) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <MemoryRouter initialEntries={initialEntries}>
        <PaymentQrPage />
      </MemoryRouter>
    </QueryClientProvider>,
  );
}

describe('PaymentQrPage', () => {
  it('pre-fills amount and content from query params', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: 'bank-1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);

    renderPage(['/qr-thanh-toan?amount=150000&content=DH-0001']);

    await waitFor(() => expect(screen.getByLabelText(/số tiền/i)).toHaveValue(150000));
    expect(screen.getByLabelText(/nội dung/i)).toHaveValue('DH-0001');
  });

  it('generates and displays a QR after submitting valid data', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: 'bank-1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);
    vi.mocked(paymentQrApi.generate).mockResolvedValue({ payload: '00020101021...6304ABCD' });

    const user = userEvent.setup();
    renderPage();

    await waitFor(() => expect(banksApi.list).toHaveBeenCalled());
    await user.click(screen.getByRole('combobox', { name: /ngân hàng/i }));
    await user.click(await screen.findByRole('option', { name: 'Vietcombank' }));
    await user.type(screen.getByLabelText(/số tài khoản/i), '0123456789');
    await user.type(screen.getByLabelText(/chủ tài khoản/i), 'Nguyen Van A');
    await user.type(screen.getByLabelText(/số tiền/i), '150000');
    await user.click(screen.getByRole('button', { name: /tạo qr/i }));

    await waitFor(() => expect(paymentQrApi.generate).toHaveBeenCalledWith(
      expect.objectContaining({ bankId: 'bank-1', accountNumber: '0123456789', amount: 150000 }),
    ));
    expect(await screen.findByRole('button', { name: /tải ảnh/i })).toBeInTheDocument();
  });
});
