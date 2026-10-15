import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { StockCardPage } from './stock-card-page';
import type { StockCard, StockCardParams } from '@/features/inventory-reports/types';
import type { ExportFormat, Tt133ExportForm } from '@/features/tt133-export/types';
import { periodPreset } from '@/lib/period-presets';
import { useAuthStore } from '@/stores/auth-store';

const exportButtonProps: {
  forms: Tt133ExportForm[];
  disabled?: boolean;
  onSelect?: (form: Tt133ExportForm, format: ExportFormat) => void;
} = { forms: [] };
vi.mock('@/components/tt133-export/tt133-export-button', () => ({
  Tt133ExportButton: (props: typeof exportButtonProps) => {
    exportButtonProps.forms = props.forms;
    exportButtonProps.disabled = props.disabled;
    exportButtonProps.onSelect = props.onSelect;
    return <div data-testid="tt133-export" data-disabled={String(!!props.disabled)} />;
  },
}));

const downloadMock = vi.fn();
vi.mock('@/features/tt133-export/use-tt133-download', () => ({
  useTt133Download: () => ({ download: downloadMock, isPending: false }),
}));

// The real typeahead needs a product search; the stub picks fixed products by button.
vi.mock('@/pages/quotations/components/product-typeahead-cell', () => ({
  ProductTypeaheadCell: (props: {
    inputId?: string;
    value: string;
    onChange: (v: string) => void;
    onSelect: (s: { id: string; code: string; name: string }) => void;
    openOnFocus?: boolean;
  }) => (
    <div>
      <input
        id={props.inputId}
        value={props.value}
        onChange={(e) => props.onChange(e.target.value)}
        data-open-on-focus={String(props.openOnFocus)}
      />
      {[
        { id: 'p2', code: 'THEP02', name: 'Thép' },
        { id: 'p3', code: 'SON03', name: 'Sơn' },
      ].map((p) => (
        <button key={p.id} type="button" onClick={() => props.onSelect(p)}>
          {`pick ${props.inputId} ${p.code}`}
        </button>
      ))}
    </div>
  ),
}));

vi.mock('@/features/warehouses/hooks', () => ({
  useWarehouses: () => ({ data: [{ id: 'w1', code: 'KHO01', name: 'Kho chính', isActive: true }], isSuccess: true }),
}));
vi.mock('@/features/products/hooks', () => ({
  useProductSearch: () => ({ data: [], isLoading: false, isError: false }),
  useProducts: () => ({ data: { items: [], page: 1, pageSize: 20, totalItems: 0, totalPages: 0 }, isLoading: false }),
  useProductGroups: () => ({ data: [] }),
}));

const cardState: { data: StockCard | undefined; error?: Error } = { data: undefined };
const useStockCardMock = vi.fn((params: StockCardParams, options?: { enabled?: boolean }) => {
  void params;
  void options;
  return { data: cardState.data, isLoading: false, isError: !!cardState.error, error: cardState.error };
});
vi.mock('@/features/inventory-reports/hooks', () => ({
  useStockCard: (params: StockCardParams, options?: { enabled?: boolean }) => useStockCardMock(params, options),
}));

const card = (over: Partial<StockCard> = {}): StockCard => ({
  productId: 'p1',
  productCode: 'TON01',
  productName: 'Tôn lạnh',
  unitName: 'm²',
  from: '2026-10-01',
  to: '2026-10-31',
  isProvisional: true,
  canViewCost: true,
  valuesAtScopeOnly: false,
  openingQty: 5,
  openingValue: 500_000,
  inQty: 10,
  outQty: 4,
  inValue: 1_100_000,
  outValue: 440_000,
  closingQty: 11,
  closingValue: 1_160_000,
  rows: [
    {
      postedAt: '2026-10-01T17:00:00Z', sourceType: 'Opening', sourceId: 'w1', sourceCode: 'TDK',
      reasonName: 'Tồn đầu kỳ', partnerName: null, warehouseCode: 'KHO01',
      qtyIn: 0, qtyOut: 0, unitCost: null, inValue: null, costAmount: null, runningQty: 5, runningValue: 500_000,
    },
    {
      postedAt: '2026-10-02T02:00:00Z', sourceType: 'StockIn', sourceId: 'v1', sourceCode: 'PN00001',
      reasonName: 'Nhập mua hàng', partnerName: 'NCC A', warehouseCode: 'KHO01',
      qtyIn: 10, qtyOut: 0, unitCost: 110_000, inValue: 1_100_000, costAmount: null, runningQty: 15, runningValue: 1_600_000,
    },
    {
      postedAt: '2026-10-05T02:00:00Z', sourceType: 'StockOut', sourceId: 'v2', sourceCode: 'PX00001',
      reasonName: 'Xuất bán hàng', partnerName: 'KH B', warehouseCode: 'KHO01',
      qtyIn: 0, qtyOut: 4, unitCost: 110_000, inValue: null, costAmount: 440_000, runningQty: 11, runningValue: 1_160_000,
    },
  ],
  ...over,
});

function SearchDisplay() {
  return <div data-testid="search">{useLocation().search}</div>;
}

function renderWithState(url: string, state?: unknown) {
  const [pathname, search = ''] = url.split('?');
  return render(
    <MemoryRouter initialEntries={[{ pathname, search: search ? `?${search}` : '', state }]}>
      <StockCardPage />
      <SearchDisplay />
    </MemoryRouter>,
  );
}

function renderAt(url: string) {
  return render(
    <MemoryRouter initialEntries={[url]}>
      <StockCardPage />
      <SearchDisplay />
    </MemoryRouter>,
  );
}

describe('StockCardPage', () => {
  beforeEach(() => {
    useStockCardMock.mockClear();
    cardState.error = undefined;
    downloadMock.mockClear();
    useAuthStore.setState({ user: null });
  });

  it('reads product from the URL and renders opening, rows (voucher codes linked by type, opening rows unlinked) and closing', () => {
    cardState.data = card();
    renderAt('/inventory/stock-card?productId=p1&warehouseId=w1');

    expect(useStockCardMock).toHaveBeenLastCalledWith(expect.objectContaining({ productId: 'p1', warehouseId: 'w1' }), { enabled: true });
    expect(screen.getByText('Giá vốn tạm tính')).toBeInTheDocument();

    const summary = screen.getByTestId('stock-card-summary');
    expect(within(summary).getByText('Tồn đầu')).toBeInTheDocument();
    expect(within(summary).getByText('11 m²')).toBeInTheDocument();
    expect(within(summary).getByText('1.160.000')).toBeInTheDocument(); // vi-VN grouping

    expect(screen.getByRole('link', { name: 'PN00001' })).toHaveAttribute('href', '/stock-in/v1');
    expect(screen.getByRole('link', { name: 'PX00001' })).toHaveAttribute('href', '/stock-out/v2');
    expect(screen.queryByRole('link', { name: /TDK|Tồn đầu kỳ/ })).not.toBeInTheDocument();
    expect(screen.getAllByText('Tồn đầu kỳ').length).toBeGreaterThan(0);
    expect(screen.getByRole('columnheader', { name: 'Giá trị tồn' })).toBeInTheDocument();
  });

  it('value columns hidden without view cost', () => {
    cardState.data = card({ canViewCost: false, openingValue: null, closingValue: null, inValue: null, outValue: null });
    renderAt('/inventory/stock-card?productId=p1');

    expect(screen.queryByRole('columnheader', { name: 'Đơn giá' })).not.toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Giá trị tồn' })).not.toBeInTheDocument();
    expect(screen.queryByText('Giá vốn tạm tính')).not.toBeInTheDocument();
  });

  it('values-at-scope-only hides running values and shows the note', () => {
    cardState.data = card({ valuesAtScopeOnly: true, openingValue: null, closingValue: null });
    renderAt('/inventory/stock-card?productId=p1&warehouseId=w1');

    expect(screen.getByRole('columnheader', { name: 'Đơn giá' })).toBeInTheDocument();
    expect(screen.queryByRole('columnheader', { name: 'Giá trị tồn' })).not.toBeInTheDocument();
    expect(screen.getByText('Giá trị tồn tính theo chi nhánh — bỏ lọc kho để xem')).toBeInTheDocument();
  });

  it('asks for a product when none is selected', () => {
    cardState.data = undefined;
    renderAt('/inventory/stock-card');

    expect(useStockCardMock).toHaveBeenLastCalledWith(expect.objectContaining({ productId: '' }), { enabled: true });
    expect(screen.getByText('Chọn hàng hóa để xem thẻ kho')).toBeInTheDocument();
  });

  it('drops a warehouseId that is not a warehouse of the working branch', () => {
    cardState.data = card();
    renderAt('/inventory/stock-card?productId=p1&warehouseId=other-branch-wh');

    expect(screen.getByTestId('search')).toHaveTextContent('?productId=p1');
    expect(screen.getByTestId('search')).not.toHaveTextContent('warehouseId');
    expect(useStockCardMock).toHaveBeenLastCalledWith(
      expect.objectContaining({ productId: 'p1', warehouseId: undefined }),
      { enabled: true },
    );
  });

  it('holds the request and explains an invalid date range', () => {
    cardState.data = undefined;
    renderAt('/inventory/stock-card?productId=p1');

    fireEvent.change(screen.getByLabelText('Từ ngày'), { target: { value: '2026-10-10' } });
    fireEvent.change(screen.getByLabelText('Đến ngày'), { target: { value: '2026-10-01' } });
    expect(screen.getByText('Từ ngày không được sau Đến ngày')).toBeInTheDocument();
    expect(useStockCardMock).toHaveBeenLastCalledWith(expect.anything(), { enabled: false });

    fireEvent.change(screen.getByLabelText('Từ ngày'), { target: { value: '' } });
    expect(screen.getByText('Chọn đủ Từ ngày và Đến ngày')).toBeInTheDocument();
    expect(useStockCardMock).toHaveBeenLastCalledWith(expect.anything(), { enabled: false });
  });

  it('shows the request error', () => {
    cardState.data = undefined;
    cardState.error = new Error('Không tải được thẻ kho');
    renderAt('/inventory/stock-card?productId=p1');

    expect(screen.getByText('Không tải được thẻ kho')).toBeInTheDocument();
  });

  describe('TT133 export', () => {
    const URL = '/inventory/stock-card?productId=p1&warehouseId=w1';

    function setPermissions(permissions: string[]) {
      useAuthStore.setState({
        user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
      });
    }

    function fileBase(code: string) {
      const from = (screen.getByLabelText('Từ ngày') as HTMLInputElement).value;
      const to = (screen.getByLabelText('Đến ngày') as HTMLInputElement).value;
      return `${code}_${from.replace(/-/g, '')}_${to.replace(/-/g, '')}`;
    }

    it('offers S08-DNN and, with inventory.view_cost, S06-DNN with the screen parameters', () => {
      cardState.data = card();
      setPermissions(['reports.inventory', 'inventory.view_cost']);
      renderAt(URL);

      const from = (screen.getByLabelText('Từ ngày') as HTMLInputElement).value;
      const to = (screen.getByLabelText('Đến ngày') as HTMLInputElement).value;
      expect(exportButtonProps.forms).toEqual([
        { code: 'S08-DNN', report: 'stock-card', params: { from, to, warehouseId: 'w1' }, fileBase: fileBase('S08-DNN') },
        { code: 'S06-DNN', report: 'stock-detail', params: { from, to, warehouseId: 'w1' }, fileBase: fileBase('S06-DNN') },
      ]);
    });

    it('offers only S08-DNN without inventory.view_cost, warehouseId omitted for all warehouses', () => {
      cardState.data = card();
      setPermissions(['reports.inventory']);
      renderAt('/inventory/stock-card?productId=p1');

      expect(exportButtonProps.forms.map((f) => f.code)).toEqual(['S08-DNN']);
      expect(exportButtonProps.forms[0].params.warehouseId).toBeUndefined();
    });

    it('opens the dialog with the current product; confirming downloads it by id', async () => {
      const user = userEvent.setup();
      cardState.data = card();
      renderAt(URL);

      act(() => exportButtonProps.onSelect!(exportButtonProps.forms[0], 'pdf'));
      expect(await screen.findByRole('dialog', { name: 'Chọn mặt hàng xuất sổ' })).toBeInTheDocument();
      expect(screen.getByRole('radio', { name: 'Đang xem: TON01' })).toBeChecked();

      await user.click(screen.getByRole('button', { name: 'Xuất' }));
      expect(downloadMock).toHaveBeenCalledWith(exportButtonProps.forms[0], 'pdf', { productIds: ['p1'] });
      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    });

    it('Tất cả có phát sinh trong kỳ downloads all', async () => {
      const user = userEvent.setup();
      cardState.data = card();
      renderAt(URL);

      act(() => exportButtonProps.onSelect!(exportButtonProps.forms[0], 'xlsx'));
      await user.click(await screen.findByRole('radio', { name: 'Tất cả có phát sinh trong kỳ' }));
      await user.click(screen.getByRole('button', { name: 'Xuất' }));

      expect(downloadMock).toHaveBeenCalledWith(exportButtonProps.forms[0], 'xlsx', { all: true });
    });

    it('several picked products send their ids', async () => {
      const user = userEvent.setup();
      cardState.data = card();
      renderAt(URL);

      act(() => exportButtonProps.onSelect!(exportButtonProps.forms[0], 'xlsx'));
      await user.click(await screen.findByRole('radio', { name: 'Chọn nhiều' }));
      await user.click(screen.getByRole('button', { name: 'pick stock-card-export-product THEP02' }));
      await user.click(screen.getByRole('button', { name: 'pick stock-card-export-product SON03' }));
      await user.click(screen.getByRole('button', { name: 'Xuất' }));

      expect(downloadMock).toHaveBeenCalledWith(exportButtonProps.forms[0], 'xlsx', { productIds: ['p2', 'p3'] });
    });

    it('without a product the dialog starts at Chọn nhiều', async () => {
      cardState.data = undefined;
      renderAt('/inventory/stock-card');
      expect(screen.getByTestId('tt133-export')).toHaveAttribute('data-disabled', 'false');

      act(() => exportButtonProps.onSelect!(exportButtonProps.forms[0], 'pdf'));
      expect(await screen.findByRole('radio', { name: 'Chọn nhiều' })).toBeChecked();
      expect(screen.queryByRole('radio', { name: /^Đang xem/ })).not.toBeInTheDocument();
    });

    it('the product boxes do not open their list on focus', async () => {
      cardState.data = undefined;
      renderAt('/inventory/stock-card');
      expect(document.getElementById('stock-card-product')).toHaveAttribute('data-open-on-focus', 'false');

      // the export dialog starts at "Chọn nhiều" when no product is selected, which shows the picker
      act(() => exportButtonProps.onSelect!(exportButtonProps.forms[0], 'pdf'));
      await screen.findByRole('radio', { name: 'Chọn nhiều' });
      expect(document.getElementById('stock-card-export-product')).toHaveAttribute('data-open-on-focus', 'false');
    });

    it('an invalid range disables the button', () => {
      cardState.data = card();
      renderAt(URL);
      fireEvent.change(screen.getByLabelText('Từ ngày'), { target: { value: '2026-10-10' } });
      fireEvent.change(screen.getByLabelText('Đến ngày'), { target: { value: '2026-10-01' } });

      expect(screen.getByTestId('tt133-export')).toHaveAttribute('data-disabled', 'true');
    });

    it('the preset Tháng trước sets the previous month', async () => {
      const user = userEvent.setup();
      cardState.data = card();
      renderAt(URL);

      await user.click(screen.getByRole('combobox', { name: 'Kỳ' }));
      await user.click(screen.getByRole('option', { name: 'Tháng trước' }));

      const last = periodPreset('lastMonth');
      expect(screen.getByLabelText('Từ ngày')).toHaveValue(last.from);
      expect(screen.getByLabelText('Đến ngày')).toHaveValue(last.to);
    });
  });

  describe('back link', () => {
    const CARD_URL = '/inventory/stock-card?productId=p1';

    it('links back to the report it was opened from', () => {
      cardState.data = card();
      renderWithState(CARD_URL, { from: '/parent?x=1' });

      expect(screen.getByRole('link', { name: 'Quay lại báo cáo trước' })).toHaveAttribute('href', '/parent?x=1');
    });

    it('has no back link without state', () => {
      cardState.data = card();
      renderWithState(CARD_URL);

      expect(screen.queryByRole('link', { name: /^Quay lại/ })).not.toBeInTheDocument();
    });

    it('keeps the back link after a filter change', async () => {
      cardState.data = card();
      const user = userEvent.setup();
      renderWithState(CARD_URL, { from: '/parent?x=1' });

      await user.click(screen.getByRole('combobox', { name: 'Kho' }));
      await user.click(await screen.findByRole('option', { name: 'KHO01 — Kho chính' }));

      expect(screen.getByTestId('search')).toHaveTextContent('warehouseId=w1');
      expect(screen.getByRole('link', { name: 'Quay lại báo cáo trước' })).toHaveAttribute('href', '/parent?x=1');
    });
  });
});
