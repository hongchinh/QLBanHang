import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { RecalcCostPage } from './recalc-cost-page';
import type { ProductSuggestion } from '@/features/products/types';
import { toast } from '@/lib/use-toast';

const recalcMock = vi.fn();
const settingsState: { loading: boolean } = { loading: false };

vi.mock('@/features/inventory-settings/hooks', () => ({
  useInventorySettings: () =>
    settingsState.loading
      ? { data: undefined, isLoading: true, isError: false }
      : { data: { costingPeriod: 'Month' }, isLoading: false, isError: false },
  useRecalcCost: () => ({ mutateAsync: recalcMock, isPending: false }),
}));

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({
    data: [
      { id: 'w1', code: 'KHO01', name: 'Kho chính', branchId: 'b1', branchCode: 'CN01', branchName: 'CN', isActive: true },
      { id: 'w0', code: 'KHO00', name: 'Kho cũ', branchId: 'b1', branchCode: 'CN01', branchName: 'CN', isActive: false },
    ],
  }),
}));

const PRODUCT = { id: 'p1', code: 'SP01', name: 'Tôn lạnh' } as ProductSuggestion;
vi.mock('@/pages/quotations/components/product-typeahead-cell', () => ({
  ProductTypeaheadCell: ({ onSelect, inputId }: { onSelect: (s: ProductSuggestion) => void; inputId?: string }) => (
    <button type="button" id={inputId} onClick={() => onSelect(PRODUCT)}>
      Chọn hàng
    </button>
  ),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean }) => unknown) =>
    selector({ hasPermission: (p) => ['inventory.recalc_cost', 'products.view'].includes(p) }),
}));

describe('RecalcCostPage', () => {
  beforeEach(() => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 7, 10, 0, 0));
    recalcMock.mockReset().mockResolvedValue({ fromPeriodStart: '2026-09-01', scopeCount: 3 });
    settingsState.loading = false;
    vi.mocked(toast).mockClear();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('submits selected period, warehouse and product after confirmation', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <RecalcCostPage />
      </MemoryRouter>,
    );

    await user.click(screen.getByRole('combobox', { name: 'Từ kỳ' }));
    await user.click(screen.getByRole('option', { name: 'Tháng 09/2026' }));
    await user.click(screen.getByRole('combobox', { name: 'Kho' }));
    expect(screen.queryByRole('option', { name: /KHO00/ })).not.toBeInTheDocument();
    await user.click(screen.getByRole('option', { name: 'KHO01 — Kho chính' }));
    await user.click(screen.getByText('Chọn hàng'));
    expect(screen.getByText('SP01 — Tôn lạnh')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Tính lại giá vốn' }));
    expect(recalcMock).not.toHaveBeenCalled();
    await user.click(screen.getByRole('button', { name: 'Tính lại' }));

    await waitFor(() =>
      expect(recalcMock).toHaveBeenCalledWith({
        fromPeriodStart: '2026-09-01',
        warehouseId: 'w1',
        productId: 'p1',
      }),
    );
    expect(toast).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'Đã tính lại giá vốn cho 3 phạm vi hàng hóa' }),
    );
  });

  it('keeps the button disabled while the settings load', () => {
    settingsState.loading = true;
    render(
      <MemoryRouter>
        <RecalcCostPage />
      </MemoryRouter>,
    );

    expect(screen.getByRole('button', { name: 'Tính lại giá vốn' })).toBeDisabled();
  });

  it('a client timeout says the recalculation may still be running', async () => {
    recalcMock.mockReset().mockRejectedValue({ isAxiosError: true, code: 'ECONNABORTED', message: 'timeout of 300000ms exceeded' });
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <RecalcCostPage />
      </MemoryRouter>,
    );

    await user.click(screen.getByRole('button', { name: 'Tính lại giá vốn' }));
    await user.click(screen.getByRole('button', { name: 'Tính lại' }));

    await waitFor(() =>
      expect(toast).toHaveBeenCalledWith(expect.objectContaining({ title: 'Giá vốn có thể vẫn đang được tính lại' })),
    );
    expect(toast).not.toHaveBeenCalledWith(expect.objectContaining({ variant: 'destructive' }));
  });
});
