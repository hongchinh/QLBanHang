import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { BankAccountsTab } from './bank-accounts-tab';
import { bankAccountsApi } from './api';
import { banksApi } from '@/features/banks/api';

vi.mock('./api', () => ({
  bankAccountsApi: { list: vi.fn(), create: vi.fn(), update: vi.fn(), remove: vi.fn(), setDefault: vi.fn() },
}));
vi.mock('@/features/banks/api', () => ({ banksApi: { list: vi.fn() } }));

function renderTab() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <BankAccountsTab />
    </QueryClientProvider>,
  );
}

describe('BankAccountsTab', () => {
  it('lists saved accounts and marks the default one', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: 'bank-1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);
    vi.mocked(bankAccountsApi.list).mockResolvedValue([
      {
        id: 'acc-1', bankId: 'bank-1', bankCode: 'VCB', bankName: 'Vietcombank', bankBin: '970436',
        accountNumber: '0123456789', accountName: 'Nguyen Van A', isDefault: true,
      },
    ]);

    renderTab();

    expect(await screen.findByText('0123456789')).toBeInTheDocument();
    expect(screen.getByText(/mặc định/i)).toBeInTheDocument();
  });

  it('adds a new account via the form', async () => {
    vi.mocked(banksApi.list).mockResolvedValue([
      { id: 'bank-1', code: 'VCB', name: 'Vietcombank', shortName: 'VCB', bin: '970436' },
    ]);
    vi.mocked(bankAccountsApi.list).mockResolvedValue([]);
    vi.mocked(bankAccountsApi.create).mockResolvedValue({
      id: 'acc-2', bankId: 'bank-1', bankCode: 'VCB', bankName: 'Vietcombank', bankBin: '970436',
      accountNumber: '9998887776', accountName: 'New Account', isDefault: true,
    });

    const user = userEvent.setup();
    renderTab();

    await user.click(await screen.findByRole('button', { name: /thêm tài khoản/i }));
    await user.click(screen.getByRole('combobox', { name: /ngân hàng/i }));
    await user.click(await screen.findByRole('option', { name: 'Vietcombank' }));
    await user.type(screen.getByLabelText(/số tài khoản/i), '9998887776');
    await user.type(screen.getByLabelText(/chủ tài khoản/i), 'New Account');
    await user.click(screen.getByRole('button', { name: /^lưu$/i }));

    await waitFor(() => expect(bankAccountsApi.create).toHaveBeenCalledWith(
      expect.objectContaining({ bankId: 'bank-1', accountNumber: '9998887776', accountName: 'New Account' }),
    ));
  });
});
