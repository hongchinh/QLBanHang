import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { StockReasonListPage } from './stock-reason-list-page';
import type { StockReason } from '@/features/stock-reasons/types';

const createMock = vi.fn();
const updateMock = vi.fn();
const REASONS: StockReason[] = [
  {
    id: 'r1',
    code: 'NMH',
    name: 'Nhập mua hàng',
    direction: 'In',
    partnerType: 'Supplier',
    debtEffect: 'PayableIncrease',
    isSystem: true,
  },
  {
    id: 'r2',
    code: 'XTL',
    name: 'Xuất thanh lý',
    direction: 'Out',
    partnerType: 'Any',
    debtEffect: 'None',
    isSystem: false,
  },
];

vi.mock('@/features/stock-reasons/hooks', () => ({
  useStockReasons: () => ({ data: REASONS, isLoading: false, isError: false }),
  useCreateStockReason: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateStockReason: () => ({ mutateAsync: updateMock, isPending: false }),
  useDeleteStockReason: () => ({ mutate: vi.fn(), isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const granted = new Set<string>(['inventory.catalogs.manage']);
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean; isInRole: () => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p), isInRole: () => false }),
}));

function renderPage() {
  return render(
    <MemoryRouter>
      <StockReasonListPage />
    </MemoryRouter>,
  );
}

describe('StockReasonListPage', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
    updateMock.mockReset().mockResolvedValue({});
  });

  it('renders reasons and creates one with direction and partner type', async () => {
    const user = userEvent.setup();
    renderPage();

    const row = screen.getByText('Nhập mua hàng').closest('tr')!;
    expect(within(row).getByText('Nhập')).toBeInTheDocument();
    expect(within(row).getByText('Nhà cung cấp')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Thêm lý do/ }));
    await user.type(screen.getByLabelText('Mã lý do *'), 'XHH');
    await user.type(screen.getByLabelText('Tên lý do *'), 'Xuất hàng hỏng');
    await user.click(screen.getByRole('combobox', { name: 'Loại' }));
    await user.click(await screen.findByRole('option', { name: 'Xuất' }));
    await user.click(screen.getByRole('combobox', { name: 'Đối tượng' }));
    await user.click(await screen.findByRole('option', { name: 'Khách hàng' }));
    await user.click(screen.getByRole('button', { name: 'Tạo mới' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({
        code: 'XHH',
        name: 'Xuất hàng hỏng',
        direction: 'Out',
        partnerType: 'Customer',
        debtEffect: 'None',
      }),
    );
  });

  it('system reason disables direction and partner type and hides delete', async () => {
    const user = userEvent.setup();
    renderPage();

    const systemRow = screen.getByText('Nhập mua hàng').closest('tr')!;
    const customRow = screen.getByText('Xuất thanh lý').closest('tr')!;
    expect(within(systemRow).queryByRole('button', { name: 'Xóa lý do' })).not.toBeInTheDocument();
    expect(within(customRow).getByRole('button', { name: 'Xóa lý do' })).toBeInTheDocument();

    await user.click(within(systemRow).getByRole('button', { name: 'Sửa lý do' }));
    expect(screen.getByRole('combobox', { name: 'Loại' })).toBeDisabled();
    expect(screen.getByRole('combobox', { name: 'Đối tượng' })).toBeDisabled();
    expect(screen.getByLabelText('Tên lý do *')).toBeEnabled();
  });

  it('debt effect options follow direction', async () => {
    const user = userEvent.setup();
    renderPage();

    expect(screen.getByRole('columnheader', { name: 'Ảnh hưởng công nợ' })).toBeInTheDocument();
    expect(within(screen.getByText('Nhập mua hàng').closest('tr')!).getByText('Tăng phải trả')).toBeInTheDocument();
    expect(within(screen.getByText('Xuất thanh lý').closest('tr')!).getByText('Không ảnh hưởng')).toBeInTheDocument();

    await user.click(within(screen.getByText('Xuất thanh lý').closest('tr')!).getByRole('button', { name: 'Sửa lý do' }));

    // Stock-out: None / ReceivableIncrease / PayableDecrease.
    const effect = () => screen.getByRole('combobox', { name: 'Ảnh hưởng công nợ' });
    expect(effect()).toHaveTextContent('Không ảnh hưởng');
    await user.click(effect());
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual([
      'Không ảnh hưởng',
      'Tăng phải thu',
      'Giảm phải trả',
    ]);
    await user.click(screen.getByRole('option', { name: 'Tăng phải thu' }));

    // Switching to stock-in drops the effect that is no longer allowed.
    await user.click(screen.getByRole('combobox', { name: 'Loại' }));
    await user.click(await screen.findByRole('option', { name: 'Nhập' }));
    expect(effect()).toHaveTextContent('Không ảnh hưởng');

    // Stock-in: None / PayableIncrease / ReceivableDecrease.
    await user.click(effect());
    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual([
      'Không ảnh hưởng',
      'Tăng phải trả',
      'Giảm phải thu',
    ]);
    await user.click(screen.getByRole('option', { name: 'Giảm phải thu' }));
    await user.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({
        id: 'r2',
        data: { name: 'Xuất thanh lý', direction: 'In', partnerType: 'Any', debtEffect: 'ReceivableDecrease' },
      }),
    );
  });
});
