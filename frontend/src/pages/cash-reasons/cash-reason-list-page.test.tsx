import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CashReasonListPage } from './cash-reason-list-page';
import type { CashReason, CashReasonListParams } from '@/features/cash-reasons/types';

const createMock = vi.fn();
const listParams: (CashReasonListParams | undefined)[] = [];
const REASONS: CashReason[] = [
  {
    id: 'r1',
    code: 'TKH',
    name: 'Thu tiền khách hàng',
    type: 'Receipt',
    partnerType: 'Customer',
    debtEffect: 'ReceivableDecrease',
    isSystem: true,
    isAuto: false,
  },
  {
    id: 'r2',
    code: 'CVP',
    name: 'Chi văn phòng phẩm',
    type: 'Payment',
    partnerType: 'Any',
    debtEffect: 'None',
    isSystem: false,
    isAuto: false,
  },
  {
    id: 'r3',
    code: 'TPX',
    name: 'Thu tiền theo phiếu xuất',
    type: 'Receipt',
    partnerType: 'Any',
    debtEffect: 'None',
    isSystem: true,
    isAuto: true,
  },
];

vi.mock('@/features/cash-reasons/hooks', () => ({
  useCashReasons: (params?: CashReasonListParams) => {
    listParams.push(params);
    return { data: REASONS, isLoading: false, isError: false };
  },
  useCreateCashReason: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateCashReason: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteCashReason: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const granted = new Set<string>(['cash.catalogs.manage']);
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <CashReasonListPage />
    </MemoryRouter>,
  );
}

async function effectOptions(user: ReturnType<typeof userEvent.setup>) {
  await user.click(screen.getByRole('combobox', { name: 'Ảnh hưởng công nợ' }));
  const names = (await screen.findAllByRole('option')).map((o) => o.textContent);
  await user.keyboard('{Escape}');
  return names;
}

describe('CashReasonListPage', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
    listParams.length = 0;
  });

  it('lists reasons with labels', async () => {
    const user = userEvent.setup();
    renderPage();

    for (const header of ['Mã', 'Tên', 'Loại phiếu', 'Đối tượng', 'Ảnh hưởng công nợ', 'Hệ thống']) {
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument();
    }
    const row = screen.getByText('Thu tiền khách hàng').closest('tr')!;
    expect(within(row).getByText('TKH')).toBeInTheDocument();
    expect(within(row).getByText('Phiếu thu')).toBeInTheDocument();
    expect(within(row).getByText('Khách hàng')).toBeInTheDocument();
    expect(within(row).getByText('Giảm phải thu')).toBeInTheDocument();
    expect(within(row).getByText('Hệ thống')).toBeInTheDocument();

    const custom = screen.getByText('Chi văn phòng phẩm').closest('tr')!;
    expect(within(custom).getByText('Phiếu chi')).toBeInTheDocument();
    expect(within(custom).getByText('Bất kỳ')).toBeInTheDocument();
    expect(within(custom).getByText('Không ảnh hưởng')).toBeInTheDocument();

    // The type filter narrows the list on the server; automatic reasons are always included.
    expect(listParams.at(-1)).toEqual({ type: undefined, includeAuto: true });
    await user.click(screen.getByRole('combobox', { name: 'Lọc loại phiếu' }));
    await user.click(await screen.findByRole('option', { name: 'Phiếu chi' }));
    await waitFor(() => expect(listParams.at(-1)).toEqual({ type: 'Payment', includeAuto: true }));
  });

  it('effect options follow the voucher type', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole('button', { name: /Thêm lý do/ }));
    expect(await effectOptions(user)).toEqual(['Không ảnh hưởng', 'Giảm phải thu', 'Tăng phải trả']);

    await user.click(screen.getByRole('combobox', { name: 'Loại phiếu' }));
    await user.click(await screen.findByRole('option', { name: 'Phiếu chi' }));
    expect(await effectOptions(user)).toEqual(['Không ảnh hưởng', 'Giảm phải trả', 'Tăng phải thu']);

    await user.type(screen.getByLabelText('Mã lý do *'), 'CHKH2');
    await user.type(screen.getByLabelText('Tên lý do *'), 'Chi hoàn khách');
    await user.click(screen.getByRole('combobox', { name: 'Đối tượng' }));
    await user.click(await screen.findByRole('option', { name: 'Khách hàng' }));
    await user.click(screen.getByRole('combobox', { name: 'Ảnh hưởng công nợ' }));
    await user.click(await screen.findByRole('option', { name: 'Tăng phải thu' }));
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({
        code: 'CHKH2',
        name: 'Chi hoàn khách',
        type: 'Payment',
        partnerType: 'Customer',
        debtEffect: 'ReceivableIncrease',
      }),
    );
  });

  it('auto reasons are read-only', async () => {
    const user = userEvent.setup();
    renderPage();

    const autoRow = screen.getByText('Thu tiền theo phiếu xuất').closest('tr')!;
    expect(within(autoRow).getByText('Tự động')).toBeInTheDocument();
    expect(within(autoRow).queryByRole('button', { name: 'Sửa lý do' })).not.toBeInTheDocument();
    expect(within(autoRow).queryByRole('button', { name: 'Xóa lý do' })).not.toBeInTheDocument();

    const systemRow = screen.getByText('Thu tiền khách hàng').closest('tr')!;
    expect(within(systemRow).queryByRole('button', { name: 'Xóa lý do' })).not.toBeInTheDocument();
    const customRow = screen.getByText('Chi văn phòng phẩm').closest('tr')!;
    expect(within(customRow).getByRole('button', { name: 'Xóa lý do' })).toBeInTheDocument();

    // A system reason keeps its voucher type and partner type.
    await user.click(within(systemRow).getByRole('button', { name: 'Sửa lý do' }));
    expect(screen.getByRole('combobox', { name: 'Loại phiếu' })).toBeDisabled();
    expect(screen.getByRole('combobox', { name: 'Đối tượng' })).toBeDisabled();
    expect(screen.getByLabelText('Tên lý do *')).toBeEnabled();
  });
});
