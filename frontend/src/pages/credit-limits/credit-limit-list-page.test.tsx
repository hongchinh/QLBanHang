import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CreditLimitListPage } from './credit-limit-list-page';
import type { CreditLimit } from '@/features/credit-limits/types';
import type { DebtPartnerSearchItem } from '@/features/debt/types';

const upsertMock = vi.fn();
const deleteMock = vi.fn();
const useCreditLimitsMock = vi.fn();
const useDebtPartnerSearchMock = vi.fn();

const LIMITS: CreditLimit[] = [
  { customerId: 'c1', customerCode: 'KH001', customerName: 'Công ty An Phát', creditDays: 30, amount: 50000000 },
  { customerId: 'c2', customerCode: 'KH002', customerName: 'Đại lý Bình Minh', amount: 0 },
];

const PARTNERS: DebtPartnerSearchItem[] = [
  { id: 'c3', code: 'KH003', name: 'Cửa hàng Cát Tường', status: 'Active', isCustomer: true, isSupplier: false },
];

vi.mock('@/features/credit-limits/hooks', () => ({
  useCreditLimits: (...args: unknown[]) => useCreditLimitsMock(...args),
  useUpsertCreditLimit: () => ({ mutateAsync: upsertMock, isPending: false }),
  useDeleteCreditLimit: () => ({ mutate: deleteMock, isPending: false }),
}));

vi.mock('@/features/debt/hooks', () => ({
  useDebtPartnerSearch: (...args: unknown[]) => useDebtPartnerSearchMock(...args),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

function renderPage() {
  return render(
    <MemoryRouter>
      <CreditLimitListPage />
    </MemoryRouter>,
  );
}

describe('CreditLimitListPage', () => {
  beforeEach(() => {
    upsertMock.mockReset().mockResolvedValue({});
    deleteMock.mockReset();
    useCreditLimitsMock.mockReset().mockReturnValue({
      data: { items: LIMITS, page: 1, pageSize: 20, totalItems: 2, totalPages: 1, hasNextPage: false, hasPreviousPage: false },
      isLoading: false,
      isError: false,
    });
    useDebtPartnerSearchMock.mockReset().mockReturnValue({ data: PARTNERS, isLoading: false, isError: false });
  });

  it('lists limits', () => {
    renderPage();

    const row = screen.getByText('KH001').closest('tr')!;
    expect(within(row).getByText('Công ty An Phát')).toBeInTheDocument();
    expect(within(row).getByText('30')).toBeInTheDocument();
    expect(within(row).getByText('50.000.000')).toBeInTheDocument();
    expect(screen.getByText('Đại lý Bình Minh')).toBeInTheDocument();
    expect(useCreditLimitsMock).toHaveBeenLastCalledWith({ search: undefined, page: 1, pageSize: 20 });
  });

  it('upserts with the selected customer', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole('button', { name: /Thêm hạn mức/ }));
    const dialog = screen.getByRole('dialog');
    await user.type(within(dialog).getByRole('combobox', { name: 'Khách hàng *' }), 'cat');
    await user.click(await within(dialog).findByRole('option', { name: /KH003/ }));
    expect(useDebtPartnerSearchMock).toHaveBeenLastCalledWith('Receivable', expect.any(String));

    await user.type(within(dialog).getByLabelText('Hạn mức *'), '5.000.000');
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(upsertMock).toHaveBeenCalledWith({ customerId: 'c3', amount: 5000000 }));
  });

  it('deletes after confirmation', async () => {
    const user = userEvent.setup();
    renderPage();

    const row = screen.getByText('KH002').closest('tr')!;
    await user.click(within(row).getByRole('button', { name: 'Xóa hạn mức' }));
    expect(deleteMock).not.toHaveBeenCalled();

    const confirm = screen.getByRole('dialog');
    await user.click(within(confirm).getByRole('button', { name: 'Xóa' }));

    expect(deleteMock).toHaveBeenCalledWith('c2', expect.anything());
  });
});
