import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { StockSummaryPage } from './stock-summary-page';
import type { StockSummaryParams, StockSummaryReport } from '@/features/inventory-reports/types';
import type { Tt133ExportForm } from '@/features/tt133-export/types';
import { periodPreset } from '@/lib/period-presets';

const exportButtonProps: { forms: Tt133ExportForm[]; disabled?: boolean } = { forms: [] };
vi.mock('@/components/tt133-export/tt133-export-button', () => ({
  Tt133ExportButton: (props: typeof exportButtonProps) => {
    exportButtonProps.forms = props.forms;
    exportButtonProps.disabled = props.disabled;
    return <div data-testid="tt133-export" data-disabled={String(!!props.disabled)} />;
  },
}));

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({ data: [{ id: 'w1', code: 'KHO01', name: 'Kho chính', isActive: true }] }),
}));
vi.mock('@/features/products/hooks', () => ({
  useProductGroups: () => ({ data: [{ id: 'g1', code: 'EPS', name: 'Xốp EPS' }] }),
}));

const reportState: { data: StockSummaryReport | undefined } = { data: undefined };
const useStockSummaryMock = vi.fn((params: StockSummaryParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: reportState.data, isLoading: false, isError: false, error: null };
});
vi.mock('@/features/inventory-reports/hooks', () => ({
  useStockSummary: (params: StockSummaryParams, options?: { enabled?: boolean }) =>
    useStockSummaryMock(params, options),
}));

const report = (over: Partial<StockSummaryReport> = {}): StockSummaryReport => ({
  from: '2026-10-10',
  to: '2026-10-31',
  scope: 'Branch',
  warehouseId: null,
  isProvisional: false,
  canViewCost: true,
  valuesAtScopeOnly: false,
  rows: [
    {
      productId: 'p1',
      productCode: 'SM01',
      productName: 'Hàng SM01',
      unitName: 'Tấm',
      productGroupName: 'Xốp EPS',
      openingQty: 20,
      openingValue: 2_200_000,
      inQty: 10.5,
      inValue: 1_000_000,
      outQty: 5,
      outValue: 600_000,
      closingQty: 25.5,
      closingValue: 2_600_000,
    },
  ],
  totalOpeningValue: 2_200_000,
  totalInValue: 1_000_000,
  totalOutValue: 600_000,
  totalClosingValue: 2_600_000,
  ...over,
});

const quantitiesOnly = (over: Partial<StockSummaryReport> = {}) => {
  const base = report();
  return report({
    rows: base.rows.map((r) => ({ ...r, openingValue: null, inValue: null, outValue: null, closingValue: null })),
    totalOpeningValue: null,
    totalInValue: null,
    totalOutValue: null,
    totalClosingValue: null,
    ...over,
  });
};

function LocationDisplay() {
  const loc = useLocation();
  return <div data-testid="location">{loc.pathname + loc.search}</div>;
}

function renderAt(url = '/inventory/stock-summary?from=2026-10-10&to=2026-10-31') {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <Routes>
        <Route path="/inventory/stock-summary" element={<StockSummaryPage />} />
        <Route path="/inventory/stock-card" element={<div>card</div>} />
      </Routes>
      <LocationDisplay />
    </MemoryRouter>,
  );
}

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

describe('StockSummaryPage', () => {
  beforeEach(() => {
    useStockSummaryMock.mockClear();
    reportState.data = report();
    setPermissions([]);
  });

  it('renders a row with eight numbers and the Cộng footer', () => {
    renderAt();

    const row = screen.getByText('SM01').closest('tr')!;
    const cells = within(row).getAllByRole('cell');
    expect(cells.map((c) => c.textContent)).toEqual([
      'SM01', 'Hàng SM01', 'Tấm',
      '20', '2.200.000', '10.5', '1.000.000', '5', '600.000', '25.5', '2.600.000',
    ]);
    expect(cells[3]).toHaveClass('text-right', 'tabular-nums');
    expect(cells[4]).toHaveClass('text-right', 'tabular-nums');

    for (const name of ['Tồn đầu kỳ', 'Nhập trong kỳ', 'Xuất trong kỳ', 'Tồn cuối kỳ']) {
      expect(screen.getByRole('columnheader', { name })).toBeInTheDocument();
    }
    expect(screen.getAllByRole('columnheader', { name: 'SL' })).toHaveLength(4);
    expect(screen.getAllByRole('columnheader', { name: 'Thành tiền' })).toHaveLength(4);

    const footer = screen.getByText('Cộng').closest('tr')!;
    expect(within(footer).getAllByRole('cell').map((c) => c.textContent)).toEqual([
      'Cộng', '', '2.200.000', '', '1.000.000', '', '600.000', '', '2.600.000',
    ]);
  });

  it('shows no value columns and no footer without canViewCost', () => {
    reportState.data = quantitiesOnly({ canViewCost: false });
    renderAt();

    expect(screen.queryByRole('columnheader', { name: 'Thành tiền' })).not.toBeInTheDocument();
    expect(screen.queryByText('Cộng')).not.toBeInTheDocument();
    const row = screen.getByText('SM01').closest('tr')!;
    expect(within(row).getAllByRole('cell').map((c) => c.textContent)).toEqual([
      'SM01', 'Hàng SM01', 'Tấm', '20', '10.5', '5', '25.5',
    ]);
  });

  it('explains quantities-only when the values are at scope level only', () => {
    reportState.data = quantitiesOnly({ valuesAtScopeOnly: true });
    renderAt();

    expect(screen.getByText('Giá trị tính theo chi nhánh. Bỏ lọc kho để xem giá trị.')).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Thành tiền' })).not.toBeInTheDocument();
    expect(screen.queryByText('Cộng')).not.toBeInTheDocument();
  });

  it('shows the provisional badge with values', () => {
    reportState.data = report({ isProvisional: true });
    renderAt();
    expect(screen.getByText('Giá trị tạm tính')).toBeInTheDocument();
  });

  it('passes the filters to the hook, search debounced', async () => {
    renderAt('/inventory/stock-summary?from=2026-10-10&to=2026-10-31');

    expect(useStockSummaryMock).toHaveBeenLastCalledWith(
      { from: '2026-10-10', to: '2026-10-31', scope: 'Branch', warehouseId: undefined, productGroupId: undefined, search: undefined },
      { enabled: true },
    );

    fireEvent.change(screen.getByLabelText('Tìm kiếm'), { target: { value: 'sm0' } });
    const user = userEvent.setup();
    await user.click(screen.getByRole('combobox', { name: 'Kho' }));
    await user.click(screen.getByRole('option', { name: /KHO01/ }));
    await user.click(screen.getByRole('combobox', { name: 'Nhóm hàng' }));
    await user.click(screen.getByRole('option', { name: 'Xốp EPS' }));

    await waitFor(() =>
      expect(useStockSummaryMock).toHaveBeenLastCalledWith(
        { from: '2026-10-10', to: '2026-10-31', scope: 'Branch', warehouseId: 'w1', productGroupId: 'g1', search: 'sm0' },
        { enabled: true },
      ),
    );
  });

  it('holds the request on an invalid range', () => {
    renderAt('/inventory/stock-summary?from=2026-10-31&to=2026-10-10');

    expect(screen.getByText('Từ ngày không được sau Đến ngày')).toBeInTheDocument();
    expect(useStockSummaryMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ from: '2026-10-31', to: '2026-10-10' }),
      { enabled: false },
    );
    expect(screen.getByTestId('tt133-export')).toHaveAttribute('data-disabled', 'true');
  });

  it('Toàn công ty sends scope Company, clears the warehouse and disables the warehouse select', async () => {
    setPermissions(['branches.access_all']);
    const user = userEvent.setup();
    renderAt();

    await user.click(screen.getByRole('combobox', { name: 'Kho' }));
    await user.click(screen.getByRole('option', { name: /KHO01/ }));
    await waitFor(() =>
      expect(useStockSummaryMock).toHaveBeenLastCalledWith(expect.objectContaining({ warehouseId: 'w1' }), {
        enabled: true,
      }),
    );

    await user.click(screen.getByRole('combobox', { name: 'Phạm vi' }));
    await user.click(screen.getByRole('option', { name: 'Toàn công ty' }));

    await waitFor(() =>
      expect(useStockSummaryMock).toHaveBeenLastCalledWith(
        expect.objectContaining({ scope: 'Company', warehouseId: undefined }),
        { enabled: true },
      ),
    );
    expect(screen.getByRole('combobox', { name: 'Kho' })).toBeDisabled();
    expect(screen.getByRole('combobox', { name: 'Kho' })).toHaveTextContent('Tất cả kho');
  });

  it('the preset Quý trước updates the dates', async () => {
    const user = userEvent.setup();
    renderAt();

    await user.click(screen.getByRole('combobox', { name: 'Kỳ' }));
    await user.click(screen.getByRole('option', { name: 'Quý trước' }));

    const range = periodPreset('lastQuarter');
    expect(screen.getByLabelText('Từ ngày')).toHaveValue(range.from);
    expect(screen.getByLabelText('Đến ngày')).toHaveValue(range.to);
    expect(useStockSummaryMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ from: range.from, to: range.to }),
      { enabled: true },
    );
  });

  it('gives the export button the S07 form with the hook params', () => {
    renderAt('/inventory/stock-summary?from=2026-10-10&to=2026-10-31');

    expect(exportButtonProps.forms).toEqual([
      {
        code: 'S07-DNN',
        report: 'stock-summary',
        params: { from: '2026-10-10', to: '2026-10-31', scope: 'Branch', warehouseId: undefined, productGroupId: undefined, search: undefined },
        fileBase: 'S07-DNN_20261010_20261031',
      },
    ]);
    expect(exportButtonProps.forms[0].params).toEqual(useStockSummaryMock.mock.calls.at(-1)![0]);
  });

  it('shows an empty message when there is no movement', () => {
    reportState.data = report({
      rows: [],
      totalOpeningValue: 0,
      totalInValue: 0,
      totalOutValue: 0,
      totalClosingValue: 0,
    });
    renderAt();
    expect(screen.getByText('Không có phát sinh trong kỳ')).toBeInTheDocument();
  });

  it('a row opens the stock card, with the warehouse when filtered', async () => {
    const user = userEvent.setup();
    renderAt();

    await user.click(screen.getByText('Hàng SM01'));
    expect(screen.getByTestId('location')).toHaveTextContent('/inventory/stock-card?productId=p1');
    expect(screen.getByTestId('location').textContent).not.toContain('warehouseId');
  });

  it('a row opens the stock card of the filtered warehouse', async () => {
    const user = userEvent.setup();
    renderAt();

    await user.click(screen.getByRole('combobox', { name: 'Kho' }));
    await user.click(screen.getByRole('option', { name: /KHO01/ }));
    await user.click(screen.getByText('Hàng SM01'));

    expect(screen.getByTestId('location')).toHaveTextContent('/inventory/stock-card?productId=p1&warehouseId=w1');
  });
});
