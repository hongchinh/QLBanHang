import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { FundListPage } from './fund-list-page';
import type { Fund, FundListParams } from '@/features/funds/types';
import { toast } from '@/lib/use-toast';

const FUNDS: Fund[] = [
  {
    id: 'f1',
    branchId: 'b1',
    code: 'QTM',
    name: 'Quỹ tiền mặt',
    kind: 'Cash',
    bankName: null,
    accountNumber: null,
    isDefault: true,
    openingAmount: 1500000,
    isActive: true,
  },
  {
    id: 'f2',
    branchId: 'b1',
    code: 'VCB',
    name: 'TK Vietcombank',
    kind: 'Bank',
    bankName: 'Vietcombank',
    accountNumber: '0011223344',
    isDefault: false,
    openingAmount: 0,
    isActive: false,
  },
];

const fundsParams = vi.fn<(params?: FundListParams) => void>();
const openingMock = vi.fn();
const createMock = vi.fn();

vi.mock('@/features/funds/hooks', () => ({
  useFunds: (params?: FundListParams) => {
    fundsParams(params);
    return { data: FUNDS, isLoading: false, isError: false };
  },
  useCreateFund: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateFund: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteFund: () => ({ mutate: vi.fn(), isPending: false }),
  useSetFundOpeningBalance: () => ({ mutateAsync: openingMock, isPending: false }),
}));

vi.mock('@/features/banks/hooks', () => ({ useBanks: () => ({ data: [] }) }));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

function apiError(status: number, code: string, message: string, details?: Record<string, string[]>) {
  const config = { headers: new AxiosHeaders(), url: '/funds/f1/opening-balance' } as InternalAxiosRequestConfig;
  return new AxiosError('Request failed', 'ERR_BAD_RESPONSE', config, null, {
    status,
    statusText: '',
    headers: {},
    config,
    data: { success: false, error: { code, message, details }, timestamp: '' },
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/funds']}>
      <Routes>
        <Route path="/funds" element={<FundListPage />} />
        <Route path="/settings/branches" element={<div>Trang chi nhánh</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('FundListPage', () => {
  beforeEach(() => {
    granted = new Set(['cash.catalogs.manage', 'cash.opening_balance']);
    fundsParams.mockReset();
    openingMock.mockReset();
    createMock.mockReset().mockResolvedValue({});
    vi.mocked(toast).mockReset();
  });

  it('lists funds per kind', async () => {
    const user = userEvent.setup();
    renderPage();

    const cashRow = screen.getByText('Quỹ tiền mặt').closest('tr')!;
    expect(within(cashRow).getByText('QTM')).toBeInTheDocument();
    expect(within(cashRow).getByText('Tiền mặt')).toBeInTheDocument();
    expect(within(cashRow).getByText('Mặc định')).toBeInTheDocument();
    expect(within(cashRow).getByText('1.500.000')).toBeInTheDocument();
    const bankRow = screen.getByText('TK Vietcombank').closest('tr')!;
    expect(within(bankRow).getByText('Ngân hàng')).toBeInTheDocument();
    expect(within(bankRow).getByText('Vietcombank')).toBeInTheDocument();
    expect(within(bankRow).getByText('0011223344')).toBeInTheDocument();
    expect(within(bankRow).getByText('Ngừng')).toBeInTheDocument();
    expect(fundsParams).toHaveBeenLastCalledWith({ kind: undefined });

    await user.click(screen.getByRole('combobox', { name: 'Lọc loại quỹ' }));
    await user.click(await screen.findByRole('option', { name: 'Ngân hàng' }));
    expect(fundsParams).toHaveBeenLastCalledWith({ kind: 'Bank' });
  });

  it('creates a bank fund with its account', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole('button', { name: /Thêm quỹ/ }));
    await user.type(screen.getByLabelText('Mã quỹ *'), 'ACB');
    await user.type(screen.getByLabelText('Tên quỹ *'), 'TK ACB');
    await user.click(screen.getByRole('combobox', { name: 'Loại quỹ' }));
    await user.click(await screen.findByRole('option', { name: 'Ngân hàng' }));
    await user.type(screen.getByLabelText('Ngân hàng'), 'ACB');
    await user.type(screen.getByLabelText('Số tài khoản'), '9988');
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({
        code: 'ACB',
        name: 'TK ACB',
        kind: 'Bank',
        bankName: 'ACB',
        accountNumber: '9988',
        bankId: null,
        accountHolderName: null,
        isDefault: false,
        isActive: true,
      }),
    );
  });

  it('opening balance warning resends with acknowledgement', async () => {
    const user = userEvent.setup();
    openingMock
      .mockRejectedValueOnce(
        apiError(422, 'NEGATIVE_FUND_WARNING', 'Quỹ sẽ bị âm.', { QTM: ['Âm 500.000 ngày 05/10/2026'] }),
      )
      .mockResolvedValueOnce({});
    renderPage();

    const cashRow = screen.getByText('Quỹ tiền mặt').closest('tr')!;
    await user.click(within(cashRow).getByRole('button', { name: 'Số dư đầu kỳ' }));
    const amount = screen.getByLabelText('Số dư đầu kỳ *');
    expect(amount).toHaveValue('1.500.000');
    await user.clear(amount);
    await user.type(amount, '200.000');
    await user.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(openingMock).toHaveBeenCalledWith({
        id: 'f1',
        data: { amount: 200000, acknowledgeNegativeFund: false },
      }),
    );
    expect(await screen.findByText('Quỹ bị âm')).toBeInTheDocument();
    expect(screen.getByText('Âm 500.000 ngày 05/10/2026')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Vẫn tiếp tục' }));
    await waitFor(() => expect(openingMock).toHaveBeenCalledTimes(2));
    expect(openingMock).toHaveBeenLastCalledWith({
      id: 'f1',
      data: { amount: 200000, acknowledgeNegativeFund: true },
    });
    await waitFor(() => expect(screen.queryByLabelText('Số dư đầu kỳ *')).not.toBeInTheDocument());
  });

  it('opening date not set links to branch settings', async () => {
    const user = userEvent.setup();
    openingMock.mockRejectedValueOnce(apiError(400, 'OPENING_DATE_NOT_SET', 'Chưa đặt ngày đầu kỳ của chi nhánh.'));
    renderPage();

    const cashRow = screen.getByText('Quỹ tiền mặt').closest('tr')!;
    await user.click(within(cashRow).getByRole('button', { name: 'Số dư đầu kỳ' }));
    await user.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(toast).toHaveBeenCalledWith(
        expect.objectContaining({
          variant: 'destructive',
          description: 'Chưa đặt ngày đầu kỳ của chi nhánh.',
          action: expect.objectContaining({ label: 'Mở Cài đặt → Chi nhánh' }),
        }),
      ),
    );
    const { action } = vi.mocked(toast).mock.calls[0][0];
    action!.onClick();
    expect(await screen.findByText('Trang chi nhánh')).toBeInTheDocument();
  });

  it('actions follow permissions', () => {
    granted = new Set(['cash.opening_balance']);
    const { unmount } = renderPage();
    let cashRow = screen.getByText('Quỹ tiền mặt').closest('tr')!;
    expect(screen.queryByRole('button', { name: /Thêm quỹ/ })).not.toBeInTheDocument();
    expect(within(cashRow).queryByRole('button', { name: 'Sửa quỹ' })).not.toBeInTheDocument();
    expect(within(cashRow).queryByRole('button', { name: 'Xóa quỹ' })).not.toBeInTheDocument();
    expect(within(cashRow).getByRole('button', { name: 'Số dư đầu kỳ' })).toBeInTheDocument();
    unmount();

    granted = new Set(['cash.catalogs.manage']);
    renderPage();
    cashRow = screen.getByText('Quỹ tiền mặt').closest('tr')!;
    expect(screen.getByRole('button', { name: /Thêm quỹ/ })).toBeInTheDocument();
    expect(within(cashRow).getByRole('button', { name: 'Sửa quỹ' })).toBeInTheDocument();
    expect(within(cashRow).getByRole('button', { name: 'Xóa quỹ' })).toBeInTheDocument();
    expect(within(cashRow).queryByRole('button', { name: 'Số dư đầu kỳ' })).not.toBeInTheDocument();
  });

  it('shows the opening-balance icon in the confirm tone', () => {
    granted = new Set(['cash.opening_balance']);
    renderPage();
    const cashRow = screen.getByText('Quỹ tiền mặt').closest('tr')!;
    const icon = within(cashRow).getByRole('button', { name: 'Số dư đầu kỳ' }).querySelector('[data-icon="openingBalance"]');
    expect(icon).toHaveClass('text-icon-confirm');
  });
});
