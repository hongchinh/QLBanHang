import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { RecalcCostPage } from './recalc-cost-page';
import type { ProductSuggestion } from '@/features/products/types';
import { toast } from '@/lib/use-toast';

const recalcMock = vi.fn();

vi.mock('@/features/inventory-settings/hooks', () => ({
  useInventorySettings: () => ({ data: { costingPeriod: 'Month' }, isLoading: false }),
  useRecalcCost: () => ({ mutateAsync: recalcMock, isPending: false }),
}));

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({
    data: [{ id: 'w1', code: 'KHO01', name: 'Kho chính', branchId: 'b1', branchCode: 'CN01', branchName: 'CN', isActive: true }],
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
});
