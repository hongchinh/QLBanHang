import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import type * as ReactRouterDom from 'react-router-dom';
import { StockVoucherFormPage } from './stock-voucher-form-page';
import { toDateTimeLocalValue } from '@/lib/vn-datetime';
import { toFormDefaults } from '@/features/stock-vouchers/payload';
import { useBranchStore } from '@/stores/branch-store';
import type { ProductSuggestion } from '@/features/products/types';
import type { StockReason } from '@/features/stock-reasons/types';
import type { Warehouse } from '@/features/warehouses/types';
import type { PartnerSearchItem, StockVoucher, StockVoucherDefaults } from '@/features/stock-vouchers/types';

const routeParams: { id: string } = { id: 'new' };
const navigateMock = vi.fn();

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual<typeof ReactRouterDom>('react-router-dom');
  return {
    ...actual,
    useParams: () => routeParams,
    useNavigate: () => navigateMock,
    Link: ({ children, to, ...rest }: { children: React.ReactNode; to: string }) =>
      React.createElement('a', { href: String(to), ...rest }, children),
    Navigate: ({ to, replace }: { to: string; replace?: boolean }) => {
      navigateMock(to, { replace });
      return null;
    },
  };
});

vi.mock('@/stores/auth-store', () => ({
  useAuthStore: Object.assign(
    (selector: (s: { user: { id: string }; hasPermission: () => boolean }) => unknown) =>
      selector({ user: { id: 'user-test' }, hasPermission: () => true }),
    { getState: () => ({ user: { id: 'user-test' }, hasPermission: () => true }) },
  ),
}));

const toastMock = vi.fn();
vi.mock('@/lib/use-toast', () => ({ toast: (...args: unknown[]) => toastMock(...args) }));

const createMock = vi.fn();
const updateMock = vi.fn();
const cancelMock = vi.fn();
const restoreMock = vi.fn();
const deleteMock = vi.fn();
const refetchMock = vi.fn();
const useStockAtMock = vi.fn();
const useStockReasonsMock = vi.fn();
const defaultsMock = vi.fn();
const partnerResults: { data: PartnerSearchItem[] } = { data: [] };
const paymentMethodsState: { data: { id: string; code: string; name: string; isCash: boolean }[] } = { data: [] };
const voucherState: { data: StockVoucher | undefined } = { data: undefined };
const activitiesState: { data: unknown[] } = { data: [] };

const mutation = (fn: ReturnType<typeof vi.fn>) => ({ mutateAsync: fn, isPending: false });

vi.mock('@/features/stock-vouchers/hooks', () => ({
  useStockVoucher: (id: string | undefined) => ({
    data: id ? voucherState.data : undefined,
    isLoading: false,
    refetch: refetchMock,
  }),
  useStockVoucherDefaults: (...args: unknown[]) => defaultsMock(...args),
  useStockAt: (body: unknown) => {
    useStockAtMock(body);
    return { data: [] };
  },
  useStockVoucherActivities: () => ({
    data: activitiesState.data,
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  }),
  usePartnerSearch: (_type: string, keyword: string) => ({
    data: keyword ? partnerResults.data : [],
    isLoading: false,
    isError: false,
  }),
  useCreateStockVoucher: () => mutation(createMock),
  useUpdateStockVoucher: () => mutation(updateMock),
  useCancelStockVoucher: () => mutation(cancelMock),
  useRestoreStockVoucher: () => mutation(restoreMock),
  useDeleteStockVoucher: () => mutation(deleteMock),
}));

const WH1 = '11111111-1111-4111-8111-111111111111';
const R_IN = '22222222-2222-4222-8222-222222222222';
const R_IN2 = '22222222-2222-4222-8222-222222222223';
const R_OUT = '22222222-2222-4222-8222-222222222224';
const R_IN_CUSTOMER = '22222222-2222-4222-8222-222222222225';
const PM1 = '33333333-3333-4333-8333-333333333333';
const P1 = '44444444-4444-4444-8444-444444444444';
const V1 = '55555555-5555-4555-8555-555555555555';
const L1 = '66666666-6666-4666-8666-666666666666';

const warehouses: Warehouse[] = [
  { id: WH1, code: 'KHO01', name: 'Kho chính', branchId: 'b1', branchCode: 'CN01', branchName: 'CN 1', isActive: true },
];

const reasons: StockReason[] = [
  { id: R_IN, code: 'NMH', name: 'Nhập mua hàng', direction: 'In', partnerType: 'Supplier', isSystem: true },
  { id: R_IN2, code: 'NKH', name: 'Nhập khác', direction: 'In', partnerType: 'None', isSystem: true },
  { id: R_IN_CUSTOMER, code: 'NTL', name: 'Nhập hàng trả lại', direction: 'In', partnerType: 'Customer', isSystem: true },
  { id: R_OUT, code: 'XBH', name: 'Xuất bán hàng', direction: 'Out', partnerType: 'Customer', isSystem: true },
];

vi.mock('@/features/warehouses/hooks', () => ({ useWarehouses: () => ({ data: warehouses }) }));
vi.mock('@/features/stock-reasons/hooks', () => ({
  useStockReasons: (params: unknown) => {
    useStockReasonsMock(params);
    return { data: reasons };
  },
}));
vi.mock('@/features/payment-methods/hooks', () => ({
  usePaymentMethods: () => ({ data: paymentMethodsState.data }),
}));
vi.mock('@/features/inventory-settings/hooks', () => ({
  useInventorySettings: () => ({ data: { netExcludesVat: false } }),
}));

const product: ProductSuggestion = {
  id: P1,
  code: 'SP01',
  name: 'Bu lông',
  unitName: 'Cái',
  pricingMode: 'PerUnit',
  defaultPrice: 70_000,
  costPrice: 50_000,
  defaultTaxRate: 10,
  length: undefined,
  width: undefined,
  thickness: undefined,
  trackInventory: true,
  purchaseDiscountRate: 0,
  salesDiscountRate: 0,
  priceIncludesVat: false,
};

vi.mock('@/features/products/hooks', () => ({
  useProductSearch: () => ({ data: [product], isLoading: false, isError: false }),
  useProducts: () => ({ data: { items: [], totalPages: 1 }, isLoading: false }),
  useProductGroups: () => ({ data: [] }),
  useProduct: () => ({ data: undefined, isLoading: false }),
}));

const DEFAULT_AT = '2026-10-07T01:30:00.000Z';
const PAYMENT_METHODS = [{ id: PM1, code: 'TM', name: 'Tiền mặt', isCash: true }];

const supplier: PartnerSearchItem = {
  id: '77777777-7777-4777-8777-777777777777',
  code: 'NCC01',
  name: 'Công ty Thép Việt',
  status: 'Active',
  isCustomer: false,
  isSupplier: true,
};

// Server "now" defaults, fetched after mount; a dated lookup answers for that date.
function defaultsResult(type: 'In' | 'Out', voucherAt?: string) {
  const data = voucherAt ? { ...defaultsFor(type), nextCode: `${type === 'In' ? 'PN' : 'PX'}-AT-${voucherAt}` } : defaultsFor(type);
  return { data, isLoading: false, isPending: false, isFetchedAfterMount: true };
}

function defaultsFor(type: 'In' | 'Out'): StockVoucherDefaults {
  return {
    nextCode: type === 'In' ? 'PN00001' : 'PX00001',
    voucherAt: DEFAULT_AT,
    warehouseId: WH1,
    reasonId: type === 'In' ? R_IN : R_OUT,
    paymentMethodId: PM1,
  };
}

function voucher(overrides: Partial<StockVoucher> = {}): StockVoucher {
  return {
    id: V1,
    type: 'In',
    code: 'PN00007',
    voucherAt: DEFAULT_AT,
    branchId: 'b1',
    warehouseId: WH1,
    reasonId: R_IN,
    paymentMethodId: PM1,
    note: 'Ghi chú cũ',
    freight: 0,
    orderDiscount: 0,
    goodsAmount: 100_000,
    lineDiscountTotal: 0,
    discountTotal: 0,
    vatTotal: 10_000,
    total: 110_000,
    paidAmount: 110_000,
    status: 'Active',
    ownerUserId: 'user-test',
    createdAt: DEFAULT_AT,
    version: 7,
    canEdit: true,
    canCancel: true,
    canDelete: true,
    lines: [
      {
        id: L1,
        sortOrder: 0,
        productId: P1,
        productCode: 'SP01',
        productName: 'Bu lông',
        warehouseId: WH1,
        trackInventory: true,
        pricingMode: 'PerUnit',
        unitName: 'Cái',
        priceIncludesVat: false,
        quantity: 2,
        unitPrice: 50_000,
        amount: 100_000,
        discountRate: 0,
        discountAmount: 0,
        discountManual: false,
        orderDiscountAllocated: 0,
        freightAllocated: 0,
        vatRate: 10,
        vatAmount: 10_000,
        netAmount: 100_000,
      },
    ],
    ...overrides,
  };
}

function apiError(status: number, code: string, details?: Record<string, string[]>) {
  return {
    isAxiosError: true,
    message: code,
    response: { status, data: { success: false, error: { code, message: code, details } } },
  };
}

const NEGATIVE_DETAILS = { 'SP01@KHO01': ['Tồn 1, cần 3 — thiếu 2'] };

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

async function fillPerUnitLine(quantity = 3) {
  fireEvent.change(byId('stock-line-product-code-0'), { target: { value: 'SP01' } });
  const listbox = await screen.findByRole('listbox');
  fireEvent.mouseDown(within(listbox).getByText('SP01'));
  await waitFor(() => expect(byId('stock-line-quantity-0').disabled).toBe(false));
  fireEvent.change(byId('stock-line-quantity-0'), { target: { value: String(quantity) } });
}

function renderPage(type: 'In' | 'Out' = 'In') {
  return render(<StockVoucherFormPage type={type} />);
}

function dialog() {
  return within(screen.getByRole('dialog'));
}

describe('StockVoucherFormPage', () => {
  beforeEach(() => {
    localStorage.clear();
    routeParams.id = 'new';
    voucherState.data = undefined;
    activitiesState.data = [];
    navigateMock.mockReset();
    toastMock.mockReset();
    createMock.mockReset();
    updateMock.mockReset();
    cancelMock.mockReset();
    restoreMock.mockReset();
    deleteMock.mockReset();
    refetchMock.mockReset();
    useStockAtMock.mockReset();
    useStockReasonsMock.mockReset();
    defaultsMock.mockReset();
    defaultsMock.mockImplementation((type: 'In' | 'Out', voucherAt?: string) => defaultsResult(type, voucherAt));
    partnerResults.data = [];
    paymentMethodsState.data = PAYMENT_METHODS;
    useBranchStore.setState({ workingBranchId: 'b1' });
  });

  it('new stock-in shows title, expected code and seeded defaults', () => {
    renderPage('In');

    expect(screen.getByRole('heading', { name: 'Thêm phiếu nhập kho' })).toBeInTheDocument();
    expect(screen.getByText('Số phiếu dự kiến: PN00001')).toBeInTheDocument();
    expect(byId('stock-voucher-at').value).toBe(toDateTimeLocalValue(DEFAULT_AT));
    expect(byId('stock-warehouse').value).toBe(WH1);
    expect(byId('stock-reason').value).toBe(R_IN);
    expect(byId('stock-payment-method').value).toBe(PM1);
  });

  it('reason list contains only reasons of the voucher direction', () => {
    renderPage('In');

    expect(useStockReasonsMock).toHaveBeenCalledWith({ direction: 'In' });
    const options = within(byId('stock-reason')).getAllByRole('option').map((o) => o.textContent);
    expect(options).toContain('Nhập mua hàng');
    expect(options).toContain('Nhập khác');
    expect(options).not.toContain('Xuất bán hàng');
  });

  it('saving sends the mapped payload through create', async () => {
    createMock.mockResolvedValue(voucher({ id: V1 }));
    renderPage('In');
    await fillPerUnitLine(3);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    const payload = createMock.mock.calls[0][0];
    expect(payload).toMatchObject({
      type: 'In',
      voucherAt: DEFAULT_AT,
      warehouseId: WH1,
      reasonId: R_IN,
      paymentMethodId: PM1,
      acknowledgeNegativeStock: false,
    });
    expect(payload.lines).toHaveLength(1);
    expect(payload.lines[0]).toMatchObject({
      sortOrder: 0,
      productId: P1,
      warehouseId: WH1,
      quantity: 3,
      unitPrice: 50_000,
      vatRate: 10,
      discountManual: false,
    });
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(`/stock-in/${V1}`, { replace: true }));
  });

  it('negative stock warning asks for confirmation and resends with acknowledgement', async () => {
    createMock
      .mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_WARNING', NEGATIVE_DETAILS))
      .mockResolvedValueOnce(voucher());
    renderPage('In');
    await fillPerUnitLine(3);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    expect(await screen.findByText('Cảnh báo âm kho')).toBeInTheDocument();
    expect(screen.getByText('SP01 @ KHO01')).toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Vẫn lưu' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(2));
    const [first, second] = createMock.mock.calls.map((c) => c[0]);
    expect(first.acknowledgeNegativeStock).toBe(false);
    expect(second).toEqual({ ...first, acknowledgeNegativeStock: true });
  });

  it('blocked negative stock shows the blocked dialog without resend', async () => {
    createMock.mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_BLOCKED', NEGATIVE_DETAILS));
    renderPage('In');
    await fillPerUnitLine(3);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    expect(await screen.findByText('Không đủ tồn kho')).toBeInTheDocument();
    expect(dialog().queryByRole('button', { name: 'Vẫn lưu' })).not.toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Đóng' }));
    await waitFor(() => expect(screen.queryByText('Không đủ tồn kho')).not.toBeInTheDocument());
    expect(createMock).toHaveBeenCalledTimes(1);
  });

  it('cancel warning resends cancel with acknowledgement', async () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    cancelMock
      .mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_WARNING', NEGATIVE_DETAILS))
      .mockResolvedValueOnce(voucher({ status: 'Cancelled', version: 8 }));
    renderPage('In');

    fireEvent.click(screen.getByRole('button', { name: 'Hủy phiếu' }));
    fireEvent.click(dialog().getByRole('button', { name: 'Hủy phiếu' }));

    fireEvent.click(await screen.findByRole('button', { name: 'Vẫn hủy phiếu' }));

    await waitFor(() => expect(cancelMock).toHaveBeenCalledTimes(2));
    expect(cancelMock.mock.calls[0][0]).toEqual({ id: V1, body: { version: 7, acknowledgeNegativeStock: false } });
    expect(cancelMock.mock.calls[1][0]).toEqual({ id: V1, body: { version: 7, acknowledgeNegativeStock: true } });
  });

  it('delete warning resends DELETE with acknowledgeNegativeStock=true', async () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    deleteMock
      .mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_WARNING', NEGATIVE_DETAILS))
      .mockResolvedValueOnce(undefined);
    renderPage('In');

    fireEvent.click(screen.getByRole('button', { name: 'Xóa' }));
    fireEvent.click(dialog().getByRole('button', { name: 'Xóa' }));

    fireEvent.click(await screen.findByRole('button', { name: 'Vẫn xóa' }));

    await waitFor(() => expect(deleteMock).toHaveBeenCalledTimes(2));
    expect(deleteMock.mock.calls[1][0]).toEqual({
      id: V1,
      type: 'In',
      body: { version: 7, acknowledgeNegativeStock: true },
    });
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/stock-in'));
  });

  it('concurrency conflict resets the form to the server data and does not resend stale values', async () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    updateMock.mockRejectedValueOnce(apiError(409, 'CONCURRENCY'));
    refetchMock.mockResolvedValueOnce({ status: 'success', data: voucher({ version: 8, note: 'Ghi chú máy chủ' }) });
    renderPage('In');

    fireEvent.change(byId('stock-voucher-note'), { target: { value: 'Sửa cục bộ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() => expect(byId('stock-voucher-note').value).toBe('Ghi chú máy chủ'));
    expect(updateMock).toHaveBeenCalledTimes(1);
    expect(updateMock.mock.calls[0][0].data.version).toBe(7);
    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({
        variant: 'destructive',
        title: 'Phiếu đã được người khác cập nhật. Đã tải lại dữ liệu — các thay đổi chưa lưu đã bị bỏ.',
      }),
    );
  });

  it('concurrency conflict keeps local edits when the reload fails', async () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    updateMock.mockRejectedValueOnce(apiError(409, 'CONCURRENCY'));
    // A failed refetch still returns the old cached data.
    refetchMock.mockResolvedValueOnce({ status: 'error', data: voucher() });
    renderPage('In');

    fireEvent.change(byId('stock-voucher-note'), { target: { value: 'Sửa cục bộ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() =>
      expect(toastMock).toHaveBeenCalledWith(
        expect.objectContaining({ variant: 'destructive', title: expect.stringContaining('không tải lại được') }),
      ),
    );
    expect(byId('stock-voucher-note').value).toBe('Sửa cục bộ');
  });

  it('validation details map lines[0].width onto the grid cell', async () => {
    createMock.mockRejectedValueOnce(
      apiError(400, 'VALIDATION', { 'lines[0].width': ['Chiều rộng phải lớn hơn 0'] }),
    );
    renderPage('In');
    await fillPerUnitLine(3);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    await waitFor(() => expect(byId('stock-line-width-0').getAttribute('aria-invalid')).toBe('true'));
  });

  it('Ctrl+S saves', async () => {
    createMock.mockResolvedValue(voucher());
    renderPage('In');
    await fillPerUnitLine(2);

    fireEvent.keyDown(byId('stock-voucher-note'), { key: 's', ctrlKey: true });

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
  });

  it('a repeated Ctrl+S submits only once', async () => {
    createMock.mockReturnValue(new Promise(() => {}));
    renderPage('In');
    await fillPerUnitLine(2);

    fireEvent.keyDown(byId('stock-voucher-note'), { key: 's', ctrlKey: true });
    fireEvent.keyDown(byId('stock-voucher-note'), { key: 's', ctrlKey: true });
    fireEvent.keyDown(byId('stock-voucher-note'), { key: 's', ctrlKey: true });

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    await new Promise((r) => setTimeout(r, 20));
    expect(createMock).toHaveBeenCalledTimes(1);
  });

  it('new voucher waits for defaults fetched after mount, never cached ones', () => {
    defaultsMock.mockImplementation((type: 'In' | 'Out', voucherAt?: string) => ({
      ...defaultsResult(type, voucherAt),
      isFetchedAfterMount: false,
    }));
    renderPage('In');

    expect(screen.getByText('Đang tải...')).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Thêm phiếu nhập kho' })).not.toBeInTheDocument();
    expect(defaultsMock).toHaveBeenCalledWith('In', undefined, { enabled: true, fresh: true });
  });

  it('restored draft shows the expected code for the draft date', async () => {
    const draftAt = '2026-09-01T08:00';
    localStorage.setItem(
      'stock_voucher_draft_In_user-test_b1',
      JSON.stringify({
        savedAt: DEFAULT_AT,
        values: { ...toFormDefaults(undefined, defaultsFor('In')), voucherAt: draftAt },
        selectedPartner: null,
      }),
    );
    renderPage('In');

    expect(byId('stock-voucher-at').value).toBe(draftAt);
    expect(screen.queryByText('Số phiếu dự kiến: PN00001')).not.toBeInTheDocument();
    expect(await screen.findByText(`Số phiếu dự kiến: PN-AT-${draftAt}`)).toBeInTheDocument();
    expect(defaultsMock).toHaveBeenCalledWith('In', draftAt, { enabled: true });
  });

  it('payment method select shows the value once its options load', () => {
    paymentMethodsState.data = [];
    const { rerender } = renderPage('In');

    paymentMethodsState.data = PAYMENT_METHODS;
    rerender(<StockVoucherFormPage type="In" />);

    expect(byId('stock-payment-method').value).toBe(PM1);
  });

  it('a voucher of the other type redirects to its own route', () => {
    routeParams.id = V1;
    voucherState.data = voucher({ type: 'Out', code: 'PX00007' });
    renderPage('In');

    expect(navigateMock).toHaveBeenCalledWith(`/stock-out/${V1}`, { replace: true });
    expect(screen.queryByRole('heading', { name: /Phiếu nhập kho/ })).not.toBeInTheDocument();
  });

  it('switching to a reason of another partner role clears a partner without that role', async () => {
    partnerResults.data = [supplier];
    renderPage('In');

    fireEvent.change(byId('stock-partner'), { target: { value: 'NCC' } });
    fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('NCC01'));
    await waitFor(() => expect(byId('stock-partner-name').value).toBe(supplier.name));

    fireEvent.change(byId('stock-reason'), { target: { value: R_IN_CUSTOMER } });

    await waitFor(() => expect(byId('stock-partner-name').value).toBe(''));
    expect(byId('stock-partner').value).toBe('');
  });

  it('new voucher sends no excludeVoucherId to stock-at', async () => {
    renderPage('In');
    await fillPerUnitLine(1);

    await waitFor(() => {
      const last = useStockAtMock.mock.calls[useStockAtMock.mock.calls.length - 1][0];
      expect(last.items).toEqual([{ productId: P1, warehouseId: WH1 }]);
    });
    const last = useStockAtMock.mock.calls[useStockAtMock.mock.calls.length - 1][0];
    expect(last).toMatchObject({ type: 'In', at: DEFAULT_AT });
    expect(last.excludeVoucherId).toBeUndefined();
  });

  it('edit shows the activity history', () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    activitiesState.data = [
      { id: 'a1', action: 'Created', actorName: 'Admin', occurredAt: DEFAULT_AT, description: 'Tạo phiếu PN00007' },
    ];
    renderPage('In');

    expect(screen.getByText('Lịch sử')).toBeInTheDocument();
    expect(screen.getByText('Tạo phiếu PN00007')).toBeInTheDocument();
    const last = useStockAtMock.mock.calls[useStockAtMock.mock.calls.length - 1][0];
    expect(last.excludeVoucherId).toBe(V1);
  });

  it('cancelled voucher is read-only and offers only restore', () => {
    routeParams.id = V1;
    voucherState.data = voucher({ status: 'Cancelled', canEdit: false });
    renderPage('In');

    expect(screen.getByText('Đã hủy')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Khôi phục' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cập nhật' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hủy phiếu' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Xóa' })).not.toBeInTheDocument();
    expect(byId('stock-voucher-note')).toBeDisabled();
    expect(byId('stock-line-quantity-0')).toBeDisabled();
  });

  it('stock-out uses Ngày xuất, Lý do xuất and Người nhận hàng labels', () => {
    renderPage('Out');

    expect(screen.getByRole('heading', { name: 'Thêm phiếu xuất kho' })).toBeInTheDocument();
    expect(screen.getByText('Ngày xuất')).toBeInTheDocument();
    expect(screen.getByText('Lý do xuất')).toBeInTheDocument();
    expect(screen.getByText('Người nhận hàng')).toBeInTheDocument();
    expect(screen.getByText('Số phiếu dự kiến: PX00001')).toBeInTheDocument();
  });
});
