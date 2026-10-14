import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, createEvent, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import type * as ReactHookForm from 'react-hook-form';
import type * as ReactRouterDom from 'react-router-dom';
import type * as DebtHooks from '@/features/debt/hooks';
import { StockVoucherFormPage } from './stock-voucher-form-page';
import { toDateTimeLocalValue } from '@/lib/vn-datetime';
import { createEmptyStockLine, toFormDefaults } from '@/features/stock-vouchers/payload';
import { useBranchStore } from '@/stores/branch-store';
import type { Fund } from '@/features/funds/types';
import type { PaymentMethod } from '@/features/payment-methods/types';
import type { ProductSuggestion } from '@/features/products/types';
import type { StockReason } from '@/features/stock-reasons/types';
import type { Warehouse } from '@/features/warehouses/types';
import type {
  PartnerSearchItem,
  StockVoucher,
  StockVoucherDefaults,
  StockVoucherLine,
} from '@/features/stock-vouchers/types';
import { stockGridPrefsKey } from './utils/use-stock-grid-prefs';

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

// The real react-hook-form, counting the form.watch(callback) subscriptions still open, so a test can see that the
// page closes its own when it unmounts.
const watchSubscriptions = vi.hoisted(() => ({ open: 0 }));
vi.mock('react-hook-form', async (importOriginal) => {
  const actual = await importOriginal<typeof ReactHookForm>();
  const counted = new WeakSet<object>();
  const useForm: typeof actual.useForm = (...args) => {
    const form = actual.useForm(...args);
    if (!counted.has(form)) {
      counted.add(form);
      const watch = form.watch as (...watchArgs: unknown[]) => unknown;
      form.watch = ((...watchArgs: unknown[]) => {
        const result = watch(...watchArgs);
        if (typeof watchArgs[0] !== 'function') return result;
        const subscription = result as { unsubscribe: () => void };
        watchSubscriptions.open += 1;
        return {
          unsubscribe: () => {
            watchSubscriptions.open -= 1;
            subscription.unsubscribe();
          },
        };
      }) as typeof form.watch;
    }
    return form;
  };
  return { ...actual, useForm };
});

// null grants every permission.
const permissionState: { granted: string[] | null } = { granted: null };
const hasPermissionMock = (p: string) => permissionState.granted === null || permissionState.granted.includes(p);
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: Object.assign(
    (selector: (s: { user: { id: string }; hasPermission: (p: string) => boolean }) => unknown) =>
      selector({ user: { id: 'user-test' }, hasPermission: hasPermissionMock }),
    { getState: () => ({ user: { id: 'user-test' }, hasPermission: hasPermissionMock }) },
  ),
}));

const partnerSummaryMock = vi.fn();
vi.mock('@/features/debt/hooks', async (importOriginal) => ({
  ...(await importOriginal<typeof DebtHooks>()),
  usePartnerDebtSummary: (...args: unknown[]) => partnerSummaryMock(...args),
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
const paymentMethodsState: { data: PaymentMethod[] } = { data: [] };
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
const R_OUT_GIFT = '22222222-2222-4222-8222-222222222226';
const PM1 = '33333333-3333-4333-8333-333333333333';
const PM_BANK = '33333333-3333-4333-8333-333333333334';
const PM_NONE = '33333333-3333-4333-8333-333333333335';
// A second cash method; in PAYMENT_METHODS only where a test adds it.
const PM_CASH2 = '33333333-3333-4333-8333-333333333336';
const FUND_CASH = '88888888-8888-4888-8888-888888888881';
const FUND_CASH2 = '88888888-8888-4888-8888-888888888882';
const FUND_BANK = '88888888-8888-4888-8888-888888888883';
const FUND_CASH_OFF = '88888888-8888-4888-8888-888888888884';
const P1 = '44444444-4444-4444-8444-444444444444';
const V1 = '55555555-5555-4555-8555-555555555555';
const L1 = '66666666-6666-4666-8666-666666666666';
const L2 = '66666666-6666-4666-8666-666666666667';

const warehouses: Warehouse[] = [
  { id: WH1, code: 'KHO01', name: 'Kho chính', branchId: 'b1', branchCode: 'CN01', branchName: 'CN 1', isActive: true },
];

const reasons: StockReason[] = [
  { id: R_IN, code: 'NMH', name: 'Nhập mua hàng', direction: 'In', partnerType: 'Supplier', debtEffect: 'PayableIncrease', isSystem: true },
  { id: R_IN2, code: 'NKH', name: 'Nhập khác', direction: 'In', partnerType: 'None', debtEffect: 'None', isSystem: true },
  { id: R_IN_CUSTOMER, code: 'NTL', name: 'Nhập hàng trả lại', direction: 'In', partnerType: 'Customer', debtEffect: 'ReceivableDecrease', isSystem: true },
  { id: R_OUT, code: 'XBH', name: 'Xuất bán hàng', direction: 'Out', partnerType: 'Customer', debtEffect: 'ReceivableIncrease', isSystem: true },
  { id: R_OUT_GIFT, code: 'XBT', name: 'Xuất biếu tặng', direction: 'Out', partnerType: 'Customer', debtEffect: 'None', isSystem: false },
];

function fund(id: string, code: string, kind: Fund['kind'], extra: Partial<Fund> = {}): Fund {
  return { id, branchId: 'b1', code, name: code, kind, isDefault: false, openingAmount: 0, isActive: true, ...extra };
}

const funds: Fund[] = [
  fund(FUND_CASH2, 'QTM2', 'Cash'),
  fund(FUND_CASH, 'QTM', 'Cash', { isDefault: true }),
  fund(FUND_CASH_OFF, 'QTM9', 'Cash', { isActive: false }),
  fund(FUND_BANK, 'NH01', 'Bank', { isDefault: true }),
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
vi.mock('@/features/funds/hooks', () => ({ useFunds: () => ({ data: funds }) }));
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
const PAYMENT_METHODS: PaymentMethod[] = [
  { id: PM1, code: 'TM', name: 'Tiền mặt', fundKind: 'Cash' },
  { id: PM_BANK, code: 'CK', name: 'Chuyển khoản', fundKind: 'Bank' },
  { id: PM_NONE, code: 'CN', name: 'Công nợ', fundKind: 'None' },
];

const supplier: PartnerSearchItem = {
  id: '77777777-7777-4777-8777-777777777777',
  code: 'NCC01',
  name: 'Công ty Thép Việt',
  status: 'Active',
  isCustomer: false,
  isSupplier: true,
  creditDays: 30,
};

const customer: PartnerSearchItem = {
  id: '99999999-9999-4999-8999-999999999999',
  code: 'KH01',
  name: 'Công ty Xây dựng An Phát',
  status: 'Active',
  isCustomer: true,
  isSupplier: false,
  creditDays: 15,
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
    reasonDebtEffect: 'PayableIncrease',
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
const CREDIT_DETAILS = { NCC01: ['Dư nợ 9.000.000 vượt hạn mức 5.000.000'] };
const FUND_DETAILS = { QTM: ['Quỹ âm 100.000'] };
const SETTLEMENT_DETAILS = { PC00003: ['Đối trừ 50.000'] };

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
    permissionState.granted = null;
    partnerSummaryMock.mockReset();
    partnerSummaryMock.mockReturnValue({
      data: {
        partnerId: customer.id,
        receivableBalance: 7_000_000,
        payableBalance: 0,
        creditLimit: 5_000_000,
        available: -2_000_000,
        overdueAmount: 0,
        creditDays: 15,
      },
      isLoading: false,
      isError: false,
    });
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

    expect(await screen.findByText('Xuất quá tồn kho')).toBeInTheDocument();
    expect(screen.getByText('SP01@KHO01')).toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Vẫn lưu' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(2));
    const [first, second] = createMock.mock.calls.map((c) => c[0]);
    expect(first.acknowledgeNegativeStock).toBe(false);
    expect(second).toEqual({ ...first, acknowledgeNegativeStock: true });
  });

  describe('focus after a 422 confirmation dialog', () => {
    async function saveFromQuantityCell() {
      createMock.mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_WARNING', NEGATIVE_DETAILS));
      renderPage('In');
      await fillPerUnitLine(3);
      const cell = byId('stock-line-quantity-0');
      act(() => cell.focus());
      fireEvent.keyDown(cell, { key: 's', ctrlKey: true });
      await screen.findByText('Xuất quá tồn kho');
      return cell;
    }

    it('"Quay lại" returns the focus to the cell that was focused, so Ctrl+S works again', async () => {
      const cell = await saveFromQuantityCell();
      fireEvent.click(dialog().getByRole('button', { name: 'Quay lại' }));
      await waitFor(() => expect(screen.queryByText('Xuất quá tồn kho')).not.toBeInTheDocument());
      expect(document.activeElement).toBe(cell);

      createMock.mockResolvedValueOnce(voucher());
      fireEvent.keyDown(document.activeElement!, { key: 's', ctrlKey: true });
      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(2));
    });

    it('Escape returns the focus to the cell that was focused', async () => {
      const cell = await saveFromQuantityCell();
      fireEvent.keyDown(document.activeElement!, { key: 'Escape' });
      await waitFor(() => expect(screen.queryByText('Xuất quá tồn kho')).not.toBeInTheDocument());
      expect(document.activeElement).toBe(cell);
    });

    it('confirming keeps the focus on the cell while the page stays', async () => {
      const cell = await saveFromQuantityCell();
      updateMock.mockResolvedValue(voucher());
      createMock.mockResolvedValueOnce(voucher());
      fireEvent.click(dialog().getByRole('button', { name: 'Vẫn lưu' }));
      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(2));
      await waitFor(() => expect(document.activeElement).toBe(cell));
    });

    // The cancel is confirmed in the "Hủy phiếu?" dialog first, so the action starts from a button of a dialog that
    // is closing. The focus must come back to the button the user used: on "Cập nhật" a blind Enter would save.
    it('dismissing the 422 of a cancel returns the focus to the "Hủy phiếu" button, not to a save button', async () => {
      routeParams.id = V1;
      voucherState.data = voucher();
      // The answer takes a while, as in the app: the title-bar buttons are disabled when the first dialog closes.
      cancelMock.mockImplementationOnce(
        () =>
          new Promise((_resolve, reject) => {
            setTimeout(() => reject(apiError(422, 'DEBT_SETTLEMENTS_WILL_BE_REMOVED', SETTLEMENT_DETAILS)), 20);
          }),
      );
      // The app renders the page in main#main-content.
      render(
        <main id="main-content">
          <StockVoucherFormPage type="In" />
        </main>,
      );
      const cancelButton = screen.getByRole('button', { name: 'Hủy phiếu' });
      act(() => cancelButton.focus());
      fireEvent.click(cancelButton);
      const confirm = dialog().getByRole('button', { name: 'Hủy phiếu' });
      act(() => confirm.focus());
      fireEvent.click(confirm);

      expect(await screen.findByText('Bỏ đối trừ chứng từ')).toBeInTheDocument();
      fireEvent.click(dialog().getByRole('button', { name: 'Quay lại' }));

      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      await waitFor(() => expect(document.activeElement).toBe(cancelButton));
      expect(document.activeElement).not.toBe(screen.getByRole('button', { name: 'Cập nhật' }));
      expect(cancelMock).toHaveBeenCalledTimes(1);
    });

    // The usual keyboard save: Enter on the last cell adds a blank row and moves into it, and saving drops that row
    // again, with the focus in it. A restored draft puts the "Xóa nháp" button first in main#main-content.
    it('Ctrl+S from a blank last row: after the 422 is dismissed the focus is on a grid cell and Ctrl+S saves again', async () => {
      localStorage.setItem(
        'stock_voucher_draft_In_user-test_b1',
        JSON.stringify({ savedAt: DEFAULT_AT, values: toFormDefaults(undefined, defaultsFor('In')), selectedPartner: null }),
      );
      createMock
        .mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_WARNING', NEGATIVE_DETAILS))
        .mockResolvedValueOnce(voucher());
      render(
        <main id="main-content">
          <StockVoucherFormPage type="In" />
        </main>,
      );
      const discardDraft = screen.getByRole('button', { name: 'Xóa nháp' });
      await fillPerUnitLine(3);
      const price = byId('stock-line-unit-price-0');
      act(() => price.focus());
      fireEvent.keyDown(price, { key: 'Enter' });
      await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-1')));

      fireEvent.keyDown(document.activeElement!, { key: 's', ctrlKey: true });
      await screen.findByText('Xuất quá tồn kho');
      // The blank row went before the save was sent.
      expect(document.getElementById('stock-line-product-code-1')).toBeNull();
      expect(createMock.mock.calls[0][0].lines).toHaveLength(1);

      fireEvent.keyDown(document.activeElement!, { key: 'Escape' });
      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());

      await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-0')));
      expect(document.querySelector('form')).toContainElement(document.activeElement as HTMLElement);
      expect(document.activeElement).not.toBe(discardDraft);
      expect(document.activeElement).not.toBe(document.body);

      fireEvent.keyDown(document.activeElement!, { key: 's', ctrlKey: true });
      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(2));
    });
  });

  // A save drops the rows without a product; the focus must not go with one of them.
  describe('focus when a save drops the blank rows', () => {
    it('the focus leaves a blank row for the product code of the last row that stays', async () => {
      const filled = (productCode: string) => ({
        ...createEmptyStockLine(0, WH1),
        productId: P1,
        productCode,
        productName: 'Bu lông',
        unitName: 'Cái',
        quantity: 2,
        unitPrice: 50_000,
        vatRate: 10,
      });
      localStorage.setItem(
        'stock_voucher_draft_In_user-test_b1',
        JSON.stringify({
          savedAt: DEFAULT_AT,
          values: {
            ...toFormDefaults(undefined, defaultsFor('In')),
            lines: [filled('SP01'), createEmptyStockLine(1, WH1), filled('SP02')],
          },
          selectedPartner: null,
        }),
      );
      createMock.mockReturnValue(new Promise(() => {}));
      renderPage('In');
      const lastKept = byId('stock-line-product-code-2');
      expect(lastKept.value).toBe('SP02');
      // Any cell of the blank row, not only its product code.
      const blankPrice = byId('stock-line-unit-price-1');
      act(() => blankPrice.focus());

      fireEvent.keyDown(blankPrice, { key: 's', ctrlKey: true });

      expect(document.activeElement).toBe(lastKept);
      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
      expect(createMock.mock.calls[0][0].lines).toHaveLength(2);
      // The row moved up one place and still has the focus.
      expect(lastKept.id).toBe('stock-line-product-code-1');
      expect(document.activeElement).toBe(lastKept);
    });

    it('the focus stays where it is in a row that is kept', async () => {
      createMock.mockReturnValue(new Promise(() => {}));
      renderPage('In');
      await fillPerUnitLine(3);
      fireEvent.click(byId('stock-line-add'));
      await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-1')));
      const quantity = byId('stock-line-quantity-0');
      act(() => quantity.focus());

      fireEvent.keyDown(quantity, { key: 's', ctrlKey: true });

      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
      expect(document.getElementById('stock-line-product-code-1')).toBeNull();
      expect(document.activeElement).toBe(quantity);
    });
  });

  it('blocked negative stock shows the blocked dialog without resend', async () => {
    createMock.mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_BLOCKED', NEGATIVE_DETAILS));
    renderPage('In');
    await fillPerUnitLine(3);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    expect(await screen.findByText('Không đủ tồn kho')).toBeInTheDocument();
    expect(dialog().queryByRole('button', { name: 'Vẫn tiếp tục' })).not.toBeInTheDocument();
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
    expect(cancelMock.mock.calls[0][0]).toEqual({ id: V1, body: { version: 7 } });
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

  it('Ctrl+S also saves with Caps Lock on (key "S"), but Ctrl+Shift+S does not', async () => {
    createMock.mockResolvedValue(voucher());
    renderPage('In');
    await fillPerUnitLine(2);

    fireEvent.keyDown(byId('stock-voucher-note'), { key: 'S', ctrlKey: true, shiftKey: true });
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(createMock).not.toHaveBeenCalled();
    fireEvent.keyDown(byId('stock-voucher-note'), { key: 'S', ctrlKey: true });
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
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

  it('paid amount follows total only for fund-backed methods', async () => {
    renderPage('In');
    await fillPerUnitLine(3);

    // 3 × 50,000 + 10% VAT.
    await waitFor(() => expect(byId('stock-paid-amount').value).toBe('165.000'));

    fireEvent.change(byId('stock-payment-method'), { target: { value: PM_NONE } });
    await waitFor(() => expect(byId('stock-paid-amount').value).toBe('0'));

    fireEvent.change(byId('stock-payment-method'), { target: { value: PM_BANK } });
    await waitFor(() => expect(byId('stock-paid-amount').value).toBe('165.000'));
  });

  it('paid amount is locked at 0 for a method without a fund, even after the user edited it', async () => {
    renderPage('In');
    await fillPerUnitLine(3);
    await waitFor(() => expect(byId('stock-paid-amount').value).toBe('165.000'));
    expect(byId('stock-paid-amount')).toBeEnabled();

    fireEvent.change(byId('stock-paid-amount'), { target: { value: '100000' } });
    fireEvent.blur(byId('stock-paid-amount'));
    fireEvent.change(byId('stock-payment-method'), { target: { value: PM_NONE } });

    await waitFor(() => expect(byId('stock-paid-amount')).toBeDisabled());
    expect(byId('stock-paid-amount').value).toBe('0');

    // A fund-backed method unlocks it and follows the total again.
    fireEvent.change(byId('stock-payment-method'), { target: { value: PM_BANK } });
    await waitFor(() => expect(byId('stock-paid-amount')).toBeEnabled());
    await waitFor(() => expect(byId('stock-paid-amount').value).toBe('165.000'));
  });

  it('server field errors show under their fields', async () => {
    createMock.mockRejectedValueOnce(
      apiError(400, 'VALIDATION', {
        fundId: ['Quỹ không hợp lệ'],
        paidAmount: ['Số tiền thanh toán không hợp lệ'],
        paymentMethodId: ['Hình thức thanh toán không có quỹ'],
      }),
    );
    renderPage('In');
    await fillPerUnitLine(3);
    await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_CASH));

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    expect(await screen.findByText('Quỹ không hợp lệ')).toBeInTheDocument();
    expect(screen.getByText('Số tiền thanh toán không hợp lệ')).toBeInTheDocument();
    expect(screen.getByText('Hình thức thanh toán không có quỹ')).toBeInTheDocument();
    expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ variant: 'destructive' }));
  });

  describe('server errors clear when the user corrects the field', () => {
    async function saveWithServerErrors(details: Record<string, string[]>) {
      createMock.mockRejectedValueOnce(apiError(400, 'VALIDATION', details));
      renderPage('In');
      await fillPerUnitLine(3);
      await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_CASH));
      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    }

    // A hidden optional column shows only while it has an error: with every column on, a cell stays to be asserted
    // on after its error has gone. Call before rendering.
    const showEveryColumn = () =>
      localStorage.setItem(
        stockGridPrefsKey('user-test', 'In'),
        JSON.stringify({ version: 1, hiddenColumns: [], enterStops: {} }),
      );

    it('the fund error and the tab dot go away when a new payment method auto-fills a fund', async () => {
      await saveWithServerErrors({ fundId: ['Chi nhánh chưa có quỹ mặc định loại ngân hàng.'] });
      expect(await screen.findByText('Chi nhánh chưa có quỹ mặc định loại ngân hàng.')).toBeInTheDocument();
      expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();

      fireEvent.change(byId('stock-payment-method'), { target: { value: PM_BANK } });

      await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_BANK));
      await waitFor(() => expect(screen.queryByText('Chi nhánh chưa có quỹ mặc định loại ngân hàng.')).toBeNull());
      expect(screen.queryByTestId('tab-error-payment')).toBeNull();
    });

    it('the fund error goes away when the user picks a fund', async () => {
      await saveWithServerErrors({ fundId: ['Quỹ không hợp lệ'] });
      expect(await screen.findByText('Quỹ không hợp lệ')).toBeInTheDocument();

      fireEvent.change(byId('stock-fund'), { target: { value: FUND_CASH2 } });

      await waitFor(() => expect(screen.queryByText('Quỹ không hợp lệ')).toBeNull());
      expect(screen.queryByTestId('tab-error-payment')).toBeNull();
    });

    // The errors below go with the field that drives them although their own value stays the same.

    // Another cash method keeps the same default fund and the same paid amount: only the method changed.
    it('the fund error goes away when another method of the same kind keeps the same fund', async () => {
      paymentMethodsState.data = [...PAYMENT_METHODS, { id: PM_CASH2, code: 'TM2', name: 'Tiền mặt quầy 2', fundKind: 'Cash' }];
      await saveWithServerErrors({ fundId: ['Quỹ không hợp lệ'] });
      expect(await screen.findByText('Quỹ không hợp lệ')).toBeInTheDocument();
      const paid = byId('stock-paid-amount').value;

      fireEvent.change(byId('stock-payment-method'), { target: { value: PM_CASH2 } });

      await waitFor(() => expect(screen.queryByText('Quỹ không hợp lệ')).toBeNull());
      expect(screen.queryByTestId('tab-error-payment')).toBeNull();
      expect(byId('stock-fund').value).toBe(FUND_CASH);
      expect(byId('stock-paid-amount').value).toBe(paid);
    });

    // The fund field goes, and the paid amount is locked at 0 with it.
    it('the fund error and the tab dot go away when the new payment method needs no fund', async () => {
      await saveWithServerErrors({ fundId: ['Quỹ không hợp lệ'] });
      expect(await screen.findByText('Quỹ không hợp lệ')).toBeInTheDocument();
      expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();

      fireEvent.change(byId('stock-payment-method'), { target: { value: PM_NONE } });

      await waitFor(() => expect(document.getElementById('stock-fund')).toBeNull());
      expect(screen.queryByTestId('tab-error-payment')).toBeNull();
    });

    // Cash and bank both make the paid amount follow the total: the amount itself does not change.
    it('the paid amount error goes away when the payment method changes', async () => {
      await saveWithServerErrors({ paidAmount: ['Số tiền thanh toán không hợp lệ'] });
      expect(await screen.findByText('Số tiền thanh toán không hợp lệ')).toBeInTheDocument();
      const paid = byId('stock-paid-amount').value;

      fireEvent.change(byId('stock-payment-method'), { target: { value: PM_BANK } });

      await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_BANK));
      expect(byId('stock-paid-amount').value).toBe(paid);
      expect(screen.queryByText('Số tiền thanh toán không hợp lệ')).toBeNull();
      expect(screen.queryByTestId('tab-error-payment')).toBeNull();
    });

    // No partner, so no credit days: the due date stays empty and only the voucher date can clear its error.
    it('the due date error goes away when the voucher date changes', async () => {
      await saveWithServerErrors({ dueDate: ['Hạn thanh toán không hợp lệ'] });
      expect(await screen.findByText('Hạn thanh toán không hợp lệ')).toBeInTheDocument();

      fireEvent.change(byId('stock-voucher-at'), { target: { value: '2026-10-08T09:00' } });

      await waitFor(() => expect(screen.queryByText('Hạn thanh toán không hợp lệ')).toBeNull());
      expect(screen.queryByTestId('tab-error-payment')).toBeNull();
      expect(byId('stock-due-date').value).toBe('');
    });

    it('the due date error and the tab dot go away when the new reason has no due date', async () => {
      await saveWithServerErrors({ dueDate: ['Hạn thanh toán không hợp lệ'] });
      expect(await screen.findByText('Hạn thanh toán không hợp lệ')).toBeInTheDocument();
      expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();

      fireEvent.change(byId('stock-reason'), { target: { value: R_IN2 } });

      await waitFor(() => expect(document.getElementById('stock-due-date')).toBeNull());
      expect(screen.queryByTestId('tab-error-payment')).toBeNull();
    });

    it('a line error goes away when that cell changes, and an unrelated error stays', async () => {
      await saveWithServerErrors({ 'lines[0].quantity': ['Số lượng không hợp lệ'], fundId: ['Quỹ không hợp lệ'] });
      await screen.findByText('Quỹ không hợp lệ');
      await waitFor(() => expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBe('true'));

      // The fund is corrected: the line error is another field's and stays.
      fireEvent.change(byId('stock-fund'), { target: { value: FUND_CASH2 } });
      await waitFor(() => expect(screen.queryByText('Quỹ không hợp lệ')).toBeNull());
      expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBe('true');

      fireEvent.change(byId('stock-line-quantity-0'), { target: { value: '4' } });
      await waitFor(() => expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBeNull());
    });

    it('a line error on another cell of the same line stays when this cell changes', async () => {
      await saveWithServerErrors({ 'lines[0].quantity': ['Số lượng không hợp lệ'] });
      await waitFor(() => expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBe('true'));

      fireEvent.change(byId('stock-line-unit-price-0'), { target: { value: '60000' } });

      expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBe('true');
    });

    it('picking another product on a line clears the server error of a cell the pick changed', async () => {
      await saveWithServerErrors({ 'lines[0].quantity': ['Số lượng không hợp lệ'] });
      await waitFor(() => expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBe('true'));

      fireEvent.change(byId('stock-line-product-code-0'), { target: { value: 'SP0' } });
      fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('SP01'));

      await waitFor(() => expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBeNull());
    });

    // A product pick rewrites the whole line at once; only what it really changed lost the value the server saw.
    it('picking another product keeps the server error of a cell the pick did not change', async () => {
      showEveryColumn();
      await saveWithServerErrors({
        'lines[0].quantity': ['Số lượng không hợp lệ'],
        'lines[0].warehouseId': ['Kho không thuộc chi nhánh đang làm việc.'],
      });
      await waitFor(() => expect(byId('stock-line-warehouse-0').getAttribute('aria-invalid')).toBe('true'));
      expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBe('true');

      fireEvent.change(byId('stock-line-product-code-0'), { target: { value: 'SP0' } });
      fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('SP01'));

      // The pick puts SL back to 0 and leaves the warehouse as it was.
      await waitFor(() => expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBeNull());
      expect(byId('stock-line-warehouse-0').getAttribute('aria-invalid')).toBe('true');
      expect(screen.getByTestId('tab-error-lines')).toBeInTheDocument();
    });

    // The same cell in error on two lines: what changes on one line leaves the other line's mark alone.
    async function updateTwoLinesWithQuantityErrors() {
      routeParams.id = V1;
      voucherState.data = voucher({ lines: [voucher().lines[0], { ...voucher().lines[0], id: L2, sortOrder: 1 }] });
      updateMock.mockRejectedValueOnce(
        apiError(400, 'VALIDATION', {
          'lines[0].quantity': ['Số lượng dòng 1 không hợp lệ'],
          'lines[1].quantity': ['Số lượng dòng 2 không hợp lệ'],
        }),
      );
      renderPage('In');
      fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));
      await waitFor(() => expect(byId('stock-line-quantity-1').getAttribute('aria-invalid')).toBe('true'));
      expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBe('true');
    }

    it('a line error on the same cell of another line stays when this cell changes', async () => {
      await updateTwoLinesWithQuantityErrors();

      fireEvent.change(byId('stock-line-quantity-0'), { target: { value: '4' } });

      await waitFor(() => expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBeNull());
      expect(byId('stock-line-quantity-1').getAttribute('aria-invalid')).toBe('true');
      expect(screen.getByTestId('tab-error-lines')).toBeInTheDocument();
    });

    it('picking another product on a line keeps the server errors of the other lines', async () => {
      await updateTwoLinesWithQuantityErrors();

      fireEvent.change(byId('stock-line-product-code-0'), { target: { value: 'SP0' } });
      fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('SP01'));

      // The pick puts SL of the first line back to 0.
      await waitFor(() => expect(byId('stock-line-quantity-0').getAttribute('aria-invalid')).toBeNull());
      expect(byId('stock-line-quantity-1').getAttribute('aria-invalid')).toBe('true');
    });

    // Nothing is paid: the fund is neither shown nor sent, so its error has nothing left to describe.
    it('the fund error and the tab dot go away when the paid amount becomes 0', async () => {
      await saveWithServerErrors({ fundId: ['Quỹ không hợp lệ'] });
      expect(await screen.findByText('Quỹ không hợp lệ')).toBeInTheDocument();
      expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();

      fireEvent.change(byId('stock-paid-amount'), { target: { value: '0' } });

      await waitFor(() => expect(document.getElementById('stock-fund')).toBeNull());
      expect(screen.queryByTestId('tab-error-payment')).toBeNull();

      // Paying again brings the fund back without the old message.
      fireEvent.change(byId('stock-paid-amount'), { target: { value: '100000' } });
      await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_CASH));
      expect(screen.queryByText('Quỹ không hợp lệ')).toBeNull();
    });

    // The paid amount follows the total until it is typed, so any line edit changes it: the fund is as wrong as before.
    it('the fund error stays while an amount is still paid, following the total or typed', async () => {
      await saveWithServerErrors({ fundId: ['Quỹ không hợp lệ'] });
      expect(await screen.findByText('Quỹ không hợp lệ')).toBeInTheDocument();

      // 4 × 50,000 + 10% VAT.
      fireEvent.change(byId('stock-line-quantity-0'), { target: { value: '4' } });
      await waitFor(() => expect(byId('stock-paid-amount').value).toBe('220.000'));
      expect(screen.getByText('Quỹ không hợp lệ')).toBeInTheDocument();

      fireEvent.change(byId('stock-paid-amount'), { target: { value: '100000' } });
      expect(screen.getByText('Quỹ không hợp lệ')).toBeInTheDocument();
      expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();
    });

    // The user cannot edit such a cell (or its value is replaced by the computed one), so its error must go when a
    // cell it is computed from changes.
    describe('on a cell computed from other cells of its line', () => {
      // SL of a line priced by dimensions is read-only: 2 × 3000 × 100 × 50 mm = 0.03 m³.
      const cubicLine = (overrides: Partial<StockVoucherLine> = {}): StockVoucherLine => ({
        ...voucher().lines[0],
        pricingMode: 'PerCubicMeter',
        unitName: 'm³',
        sheetCount: 2,
        length: 3000,
        width: 100,
        thickness: 50,
        quantity: 0.03,
        ...overrides,
      });
      const invalid = (id: string) => byId(id).getAttribute('aria-invalid');

      async function updateWithServerErrors(lines: StockVoucherLine[], details: Record<string, string[]>) {
        routeParams.id = V1;
        voucherState.data = voucher({ lines });
        updateMock.mockRejectedValueOnce(apiError(400, 'VALIDATION', details));
        renderPage('In');
        fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));
        await waitFor(() => expect(screen.getByTestId('tab-error-lines')).toBeInTheDocument());
      }

      it.each([
        ['Số tấm', 'sheet-count', '3'],
        ['Dài', 'length', '2000'],
        ['Rộng', 'width', '120'],
        ['Dày', 'thickness', '40'],
      ])('the error on a computed SL and the tab dot go away when %s changes', async (_label, cell, next) => {
        await updateWithServerErrors([cubicLine()], { 'lines[0].quantity': ['Số lượng quá lớn.'] });
        expect(byId('stock-line-quantity-0')).toBeDisabled();
        expect(invalid('stock-line-quantity-0')).toBe('true');

        fireEvent.change(byId(`stock-line-${cell}-0`), { target: { value: next } });

        await waitFor(() => expect(invalid('stock-line-quantity-0')).toBeNull());
        expect(screen.queryByTestId('tab-error-lines')).toBeNull();
      });

      it('a dimension clears the SL error of its own line only: another line and another cell keep theirs', async () => {
        await updateWithServerErrors([cubicLine(), cubicLine({ id: L2, sortOrder: 1 })], {
          'lines[0].quantity': ['Số lượng quá lớn.'],
          'lines[1].quantity': ['Số lượng quá lớn.'],
          'lines[1].unitPrice': ['Thành tiền quá lớn.'],
        });
        expect(invalid('stock-line-quantity-1')).toBe('true');

        fireEvent.change(byId('stock-line-length-1'), { target: { value: '2000' } });

        await waitFor(() => expect(invalid('stock-line-quantity-1')).toBeNull());
        expect(invalid('stock-line-unit-price-1')).toBe('true');
        expect(invalid('stock-line-quantity-0')).toBe('true');
        expect(screen.getByTestId('tab-error-lines')).toBeInTheDocument();
      });

      it('the error on a computed SL stays when a cell that SL is not computed from changes', async () => {
        await updateWithServerErrors([cubicLine()], { 'lines[0].quantity': ['Số lượng quá lớn.'] });

        fireEvent.change(byId('stock-line-unit-price-0'), { target: { value: '60000' } });
        fireEvent.change(byId('stock-line-discount-rate-0'), { target: { value: '2' } });
        fireEvent.change(byId('stock-line-vat-rate-0'), { target: { value: '8' } });

        expect(invalid('stock-line-quantity-0')).toBe('true');
        expect(screen.getByTestId('tab-error-lines')).toBeInTheDocument();
      });

      // Editing %CK turns a hand-typed Tiền CK off: the cell shows the amount computed from the rate again.
      it('the error on a hand-typed Tiền CK goes away when %CK changes, not when %VAT does', async () => {
        showEveryColumn();
        await updateWithServerErrors([{ ...voucher().lines[0], discountManual: true, discountAmount: 20_000 }], {
          'lines[0].discountAmount': ['Tiền chiết khấu phải từ 0 đến thành tiền.'],
        });
        const amount = byId('stock-line-discount-amount-0');
        // The save put the focus on the erroring cell; the user walks on to %VAT, then to %CK.
        await waitFor(() => expect(document.activeElement).toBe(amount));
        expect(invalid('stock-line-discount-amount-0')).toBe('true');

        act(() => byId('stock-line-vat-rate-0').focus());
        fireEvent.change(byId('stock-line-vat-rate-0'), { target: { value: '8' } });
        expect(amount.value).toBe('20.000');
        expect(invalid('stock-line-discount-amount-0')).toBe('true');

        act(() => byId('stock-line-discount-rate-0').focus());
        fireEvent.change(byId('stock-line-discount-rate-0'), { target: { value: '10' } });

        // 2 × 50,000 × 10%.
        await waitFor(() => expect(amount.value).toBe('10.000'));
        expect(invalid('stock-line-discount-amount-0')).toBeNull();
        expect(screen.queryByTestId('tab-error-lines')).toBeNull();
      });

      // Until it is typed by hand, Tiền CK is %CK of Số tiền (SL × Đơn giá): 2 × 50,000 × 10% = 10,000.
      const discountedLine = (overrides: Partial<StockVoucherLine> = {}): StockVoucherLine => ({
        ...voucher().lines[0],
        discountRate: 10,
        discountAmount: 10_000,
        discountManual: false,
        ...overrides,
      });
      // 0.03 m³ × 50,000 × 10%.
      const discountedCubicLine = () => cubicLine({ discountRate: 10, discountAmount: 150 });
      const DISCOUNT_MESSAGE = ['Tiền chiết khấu phải từ 0 đến thành tiền.'];

      it.each<[string, StockVoucherLine, string, string]>([
        ['%CK', discountedLine(), 'discount-rate', '5'],
        ['Đơn giá', discountedLine(), 'unit-price', '60000'],
        ['SL', discountedLine(), 'quantity', '4'],
        ['Số tấm', discountedCubicLine(), 'sheet-count', '3'],
        ['Dài', discountedCubicLine(), 'length', '2000'],
      ])('the error on a Tiền CK that follows the rate and the tab dot go away when %s changes', async (_label, line, cell, next) => {
        showEveryColumn();
        await updateWithServerErrors([line], { 'lines[0].discountAmount': DISCOUNT_MESSAGE });
        expect(invalid('stock-line-discount-amount-0')).toBe('true');

        fireEvent.change(byId(`stock-line-${cell}-0`), { target: { value: next } });

        await waitFor(() => expect(invalid('stock-line-discount-amount-0')).toBeNull());
        expect(screen.queryByTestId('tab-error-lines')).toBeNull();
      });

      it('a cell clears the error on the computed Tiền CK of its own line only', async () => {
        showEveryColumn();
        await updateWithServerErrors([discountedLine(), discountedLine({ id: L2, sortOrder: 1 })], {
          'lines[0].discountAmount': DISCOUNT_MESSAGE,
          'lines[1].discountAmount': DISCOUNT_MESSAGE,
        });
        expect(invalid('stock-line-discount-amount-1')).toBe('true');

        fireEvent.change(byId('stock-line-unit-price-1'), { target: { value: '60000' } });

        await waitFor(() => expect(invalid('stock-line-discount-amount-1')).toBeNull());
        expect(invalid('stock-line-discount-amount-0')).toBe('true');
        expect(screen.getByTestId('tab-error-lines')).toBeInTheDocument();
      });

      // A typed amount does not follow the price or the quantity, so the value the server refused is still there.
      // (Editing %CK is different: it turns the typed amount off, see the test above.)
      it('the error on a hand-typed Tiền CK stays when Đơn giá or SL changes', async () => {
        showEveryColumn();
        await updateWithServerErrors([discountedLine({ discountManual: true, discountAmount: 20_000 })], {
          'lines[0].discountAmount': DISCOUNT_MESSAGE,
        });
        expect(invalid('stock-line-discount-amount-0')).toBe('true');

        fireEvent.change(byId('stock-line-unit-price-0'), { target: { value: '60000' } });
        fireEvent.change(byId('stock-line-quantity-0'), { target: { value: '4' } });

        expect(invalid('stock-line-discount-amount-0')).toBe('true');
        expect(screen.getByTestId('tab-error-lines')).toBeInTheDocument();
      });
    });

    it('a client schema error is not cleared by an unrelated change', async () => {
      renderPage('In');
      await fillPerUnitLine(3);
      fireEvent.keyDown(byId('stock-voucher-at'), { key: '3', altKey: true });
      fireEvent.change(byId('stock-freight'), { target: { value: '-5' } });
      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
      expect(await screen.findByText('Phí vận chuyển không được âm')).toBeInTheDocument();

      fireEvent.change(byId('stock-payment-method'), { target: { value: PM_BANK } });
      await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_BANK));

      expect(screen.getByText('Phí vận chuyển không được âm')).toBeInTheDocument();
    });

    // The payment method drives the paid amount, but only a server error goes with it: the resolver's error describes
    // the value as it is now, and the amount is still -5.
    it('a client schema error on the paid amount stays when the payment method changes', async () => {
      renderPage('In');
      await fillPerUnitLine(3);
      fireEvent.change(byId('stock-paid-amount'), { target: { value: '-5' } });
      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
      expect(await screen.findByText('Giá trị phải >= 0')).toBeInTheDocument();

      fireEvent.change(byId('stock-payment-method'), { target: { value: PM_BANK } });
      await waitFor(() => expect(byId('stock-payment-method').value).toBe(PM_BANK));

      expect(screen.getByText('Giá trị phải >= 0')).toBeInTheDocument();
      expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();
      expect(createMock).not.toHaveBeenCalled();
    });

    // Tiền CK is hidden by default: the error forced it in, and after the first key of the correction only the focus
    // holds it. A Ctrl+S there that gets a 422 moves the focus into the dialog; the cell must still be on the page when
    // the dialog gives the focus back.
    it('a column that only the focus holds stays while a 422 dialog opened from its cell is up', async () => {
      routeParams.id = V1;
      voucherState.data = voucher({ lines: [{ ...voucher().lines[0], discountManual: true, discountAmount: 20_000 }] });
      updateMock
        .mockRejectedValueOnce(apiError(400, 'VALIDATION', { 'lines[0].discountAmount': ['Tiền chiết khấu phải từ 0 đến thành tiền.'] }))
        .mockRejectedValueOnce(apiError(422, 'NEGATIVE_STOCK_WARNING', NEGATIVE_DETAILS));
      renderPage('In');
      fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));
      await waitFor(() => expect(document.activeElement?.id).toBe('stock-line-discount-amount-0'));
      const cell = byId('stock-line-discount-amount-0');
      fireEvent.change(cell, { target: { value: '5' } });

      fireEvent.keyDown(cell, { key: 's', ctrlKey: true });
      await screen.findByRole('dialog');
      expect(cell.isConnected).toBe(true);
      fireEvent.click(dialog().getByRole('button', { name: 'Quay lại' }));

      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      await waitFor(() => expect(document.activeElement).toBe(cell));
    });

    it('unmounting the form closes its watch subscriptions', () => {
      const before = watchSubscriptions.open;
      const { unmount } = renderPage('In');
      expect(watchSubscriptions.open).toBeGreaterThan(before);

      unmount();

      expect(watchSubscriptions.open).toBe(before);
    });
  });

  it('server due date error shows under the due date', async () => {
    createMock.mockRejectedValueOnce(apiError(400, 'VALIDATION', { dueDate: ['Hạn thanh toán không hợp lệ'] }));
    partnerResults.data = [supplier];
    renderPage('In');
    fireEvent.change(byId('stock-partner'), { target: { value: 'NCC' } });
    fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('NCC01'));
    await fillPerUnitLine(3);
    await waitFor(() => expect(byId('stock-due-date').value).not.toBe(''));

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    expect(await screen.findByText('Hạn thanh toán không hợp lệ')).toBeInTheDocument();
  });

  it('fund select defaults to the default fund of the method kind', async () => {
    createMock.mockResolvedValue(voucher());
    renderPage('In');

    // Nothing to pay yet: no fund to choose.
    expect(document.getElementById('stock-fund')).toBeNull();
    await fillPerUnitLine(3);

    await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_CASH));
    const options = within(byId('stock-fund')).getAllByRole('option').map((o) => o.textContent);
    expect(options).toEqual(['QTM - QTM', 'QTM2 - QTM2']);

    fireEvent.change(byId('stock-payment-method'), { target: { value: PM_BANK } });
    await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_BANK));

    fireEvent.change(byId('stock-payment-method'), { target: { value: PM1 } });
    await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_CASH));
    fireEvent.change(byId('stock-fund'), { target: { value: FUND_CASH2 } });

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    expect(createMock.mock.calls[0][0].fundId).toBe(FUND_CASH2);
  });

  it('fund is hidden and not sent for a method without a fund', async () => {
    createMock.mockResolvedValue(voucher());
    renderPage('In');
    await fillPerUnitLine(3);
    await waitFor(() => expect(byId('stock-fund').value).toBe(FUND_CASH));

    fireEvent.change(byId('stock-payment-method'), { target: { value: PM_NONE } });
    await waitFor(() => expect(document.getElementById('stock-fund')).toBeNull());

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    expect(createMock.mock.calls[0][0].fundId).toBeUndefined();
  });

  it('due date pre-fills from partner credit days', async () => {
    createMock.mockResolvedValue(voucher());
    partnerResults.data = [supplier];
    renderPage('In');
    fireEvent.change(byId('stock-voucher-at'), { target: { value: '2026-10-07T08:30' } });

    expect(byId('stock-due-date').value).toBe('');
    fireEvent.change(byId('stock-partner'), { target: { value: 'NCC' } });
    fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('NCC01'));

    await waitFor(() => expect(byId('stock-due-date').value).toBe('2026-11-06'));

    // A due date typed by the user is kept when the date changes.
    fireEvent.change(byId('stock-due-date'), { target: { value: '2026-12-01' } });
    fireEvent.change(byId('stock-voucher-at'), { target: { value: '2026-10-09T08:30' } });
    expect(byId('stock-due-date').value).toBe('2026-12-01');

    await fillPerUnitLine(1);
    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    expect(createMock.mock.calls[0][0].dueDate).toBe('2026-12-01');
  });

  it('due date is shown only for debt-increasing reasons', () => {
    renderPage('In');
    expect(byId('stock-due-date')).toBeInTheDocument();

    fireEvent.change(byId('stock-reason'), { target: { value: R_IN2 } });
    expect(document.getElementById('stock-due-date')).toBeNull();

    fireEvent.change(byId('stock-reason'), { target: { value: R_IN_CUSTOMER } });
    expect(document.getElementById('stock-due-date')).toBeNull();
  });

  it('credit limit warning then negative fund warning accumulate flags', async () => {
    createMock
      .mockRejectedValueOnce(apiError(422, 'CREDIT_LIMIT_WARNING', CREDIT_DETAILS))
      .mockRejectedValueOnce(apiError(422, 'NEGATIVE_FUND_WARNING', FUND_DETAILS))
      .mockResolvedValueOnce(voucher());
    renderPage('In');
    await fillPerUnitLine(3);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    expect(await screen.findByText('Vượt hạn mức nợ')).toBeInTheDocument();
    expect(screen.getByText('NCC01')).toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Vẫn lưu' }));

    expect(await screen.findByText('Quỹ bị âm')).toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Vẫn lưu' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(3));
    const [first, second, third] = createMock.mock.calls.map((c) => c[0]);
    expect(first.acknowledgeCreditLimit).toBeUndefined();
    expect(second).toEqual({ ...first, acknowledgeCreditLimit: true });
    expect(third).toEqual({ ...first, acknowledgeCreditLimit: true, acknowledgeNegativeFund: true });
  });

  it('settlement confirmation on cancel resends with confirmRemoveSettlements', async () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    cancelMock
      .mockRejectedValueOnce(apiError(422, 'DEBT_SETTLEMENTS_WILL_BE_REMOVED', SETTLEMENT_DETAILS))
      .mockResolvedValueOnce(voucher({ status: 'Cancelled', version: 8 }));
    renderPage('In');

    fireEvent.click(screen.getByRole('button', { name: 'Hủy phiếu' }));
    fireEvent.click(dialog().getByRole('button', { name: 'Hủy phiếu' }));

    expect(await screen.findByText('Bỏ đối trừ chứng từ')).toBeInTheDocument();
    expect(screen.getByText('PC00003')).toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Vẫn hủy phiếu' }));

    await waitFor(() => expect(cancelMock).toHaveBeenCalledTimes(2));
    expect(cancelMock.mock.calls[1][0]).toEqual({ id: V1, body: { version: 7, confirmRemoveSettlements: true } });
  });

  it('blocked credit limit shows only close and does not resend', async () => {
    createMock.mockRejectedValueOnce(apiError(422, 'CREDIT_LIMIT_BLOCKED', CREDIT_DETAILS));
    renderPage('In');
    await fillPerUnitLine(3);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    expect(await screen.findByText('Vượt hạn mức nợ')).toBeInTheDocument();
    expect(dialog().queryByRole('button', { name: 'Vẫn tiếp tục' })).not.toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Đóng' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(createMock).toHaveBeenCalledTimes(1);
  });

  it('shows the automatic voucher link', () => {
    routeParams.id = V1;
    voucherState.data = voucher({
      type: 'Out',
      code: 'PX00007',
      reasonId: R_OUT,
      reasonDebtEffect: 'ReceivableIncrease',
      autoCashVoucherId: 'cv-1',
      autoCashVoucherCode: 'PT00001',
      autoCashVoucherType: 'Receipt',
    });
    renderPage('Out');

    expect(screen.getByRole('link', { name: 'Phiếu thu tự động: PT00001' })).toHaveAttribute('href', '/receipts/cv-1');
  });

  it('links an automatic payment to the payment form', () => {
    routeParams.id = V1;
    voucherState.data = voucher({ autoCashVoucherId: 'cv-2', autoCashVoucherCode: 'PC00002', autoCashVoucherType: 'Payment' });
    renderPage('In');

    expect(screen.getByRole('link', { name: 'Phiếu chi tự động: PC00002' })).toHaveAttribute('href', '/payments/cv-2');
  });

  it('shows the automatic voucher as plain text without the permission of its screen', () => {
    routeParams.id = V1;
    voucherState.data = voucher({ autoCashVoucherId: 'cv-2', autoCashVoucherCode: 'PC00002', autoCashVoucherType: 'Payment' });
    permissionState.granted = ['stock_in.view', 'stock_in.update', 'receipts.view'];
    renderPage('In');

    expect(screen.getByText('Phiếu chi tự động: PC00002')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Phiếu chi tự động: PC00002' })).not.toBeInTheDocument();
  });

  it('no automatic voucher link without an automatic voucher', () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    renderPage('In');

    expect(screen.queryByText(/tự động:/)).not.toBeInTheDocument();
  });

  it('labels the settlement and automatic voucher activities', () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    activitiesState.data = [
      { id: 'a1', action: 'SettlementRemoved', actorName: 'Admin', occurredAt: DEFAULT_AT, description: 'Bỏ đối trừ PC00003' },
      { id: 'a2', action: 'AutoVoucherChanged', actorName: 'Admin', occurredAt: DEFAULT_AT, description: 'Xóa phiếu chi PC00002' },
    ];
    renderPage('In');

    expect(screen.getByText('Bỏ đối trừ')).toBeInTheDocument();
    expect(screen.getByText('Phiếu thu/chi tự động')).toBeInTheDocument();
  });

  function tab(name: RegExp) {
    return screen.getByRole('tab', { name });
  }

  it('Alt+3 opens the payment tab and Alt+1 goes back to the lines', () => {
    renderPage('In');
    fireEvent.keyDown(byId('stock-voucher-at'), { key: '3', altKey: true });
    expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active');
    fireEvent.keyDown(byId('stock-voucher-at'), { key: '1', altKey: true });
    expect(tab(/Hàng hóa/)).toHaveAttribute('data-state', 'active');
  });

  // Hiding the panel of the focused cell would drop the focus on <body> and kill Ctrl+S / Alt+n.
  it('Alt+3 from a grid cell keeps the focus inside the page', async () => {
    renderPage('In');
    byId('stock-line-product-code-0').focus();
    fireEvent.keyDown(byId('stock-line-product-code-0'), { key: '3', altKey: true });
    await waitFor(() => expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active'));
    await waitFor(() => expect(document.activeElement).toBe(tab(/Thanh toán/)));
  });

  // The form hears its shortcuts only while the focus is inside it. After a save that opened the saved voucher, after
  // a title-bar button, after a dialog whose opener is gone, the focus is on <body> or in the title bar.
  describe('shortcuts while the focus is outside the form', () => {
    // With nothing focused, the browser sends the keys to <body>.
    function pressOnBody(init: { key: string; ctrlKey?: boolean; shiftKey?: boolean; altKey?: boolean; repeat?: boolean }) {
      const event = createEvent.keyDown(document.body, init);
      fireEvent(document.body, event);
      return event;
    }

    const blur = () => act(() => (document.activeElement as HTMLElement | null)?.blur());

    // As the app renders the page.
    const renderInMain = () =>
      render(
        <main id="main-content">
          <StockVoucherFormPage type="In" />
        </main>,
      );

    it('Ctrl+S on <body> saves exactly once, Ctrl+Shift+S does not', async () => {
      createMock.mockResolvedValue(voucher());
      renderPage('In');
      await fillPerUnitLine(2);
      blur();
      expect(document.activeElement).toBe(document.body);

      expect(pressOnBody({ key: 'S', ctrlKey: true, shiftKey: true }).defaultPrevented).toBe(false);
      await new Promise((resolve) => setTimeout(resolve, 0));
      expect(createMock).not.toHaveBeenCalled();

      // Taken from the browser too: its own Save dialog must not open.
      expect(pressOnBody({ key: 's', ctrlKey: true }).defaultPrevented).toBe(true);
      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
      // What the form holds now, as a save from inside the form would send it (the fund shows once a line is paid).
      expect(createMock.mock.calls[0][0]).toMatchObject({ fundId: FUND_CASH, lines: [expect.objectContaining({ quantity: 2 })] });
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(createMock).toHaveBeenCalledTimes(1);
    });

    it('leaves a key that something else has already taken', async () => {
      createMock.mockResolvedValue(voucher());
      renderPage('In');
      await fillPerUnitLine(2);
      blur();
      const takeKey = (e: Event) => e.preventDefault();
      document.body.addEventListener('keydown', takeKey);
      try {
        pressOnBody({ key: 's', ctrlKey: true });
        pressOnBody({ key: '3', altKey: true });
        await new Promise((resolve) => setTimeout(resolve, 20));
      } finally {
        document.body.removeEventListener('keydown', takeKey);
      }
      expect(createMock).not.toHaveBeenCalled();
      expect(tab(/Hàng hóa/)).toHaveAttribute('data-state', 'active');
    });

    it('Ctrl+S inside the form still saves exactly once', async () => {
      createMock.mockResolvedValue(voucher());
      renderInMain();
      await fillPerUnitLine(2);
      const note = byId('stock-voucher-note');
      act(() => note.focus());

      fireEvent.keyDown(note, { key: 's', ctrlKey: true });

      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(createMock).toHaveBeenCalledTimes(1);
    });

    // A key inside the form is the form's alone. A scripted key event cannot be cancelled, so the form taking it does
    // not show in defaultPrevented: only the "outside the form" check keeps the document from running it again.
    it('a key inside the form runs its shortcut once, even when the event cannot be cancelled', () => {
      renderInMain();
      const field = byId('stock-voucher-at');
      const event = createEvent.keyDown(field, { key: '3', altKey: true, cancelable: false });
      const preventDefault = vi.spyOn(event, 'preventDefault');

      fireEvent(field, event);

      expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active');
      expect(preventDefault).toHaveBeenCalledTimes(1);
    });

    // A key held down repeats until it is let go. Its repeats are taken from the browser like the first press (its
    // Save dialog must not open), but only the first press saves: not even a repeat that comes after that save ended.
    it.each<[string, () => HTMLElement]>([
      ['on <body>', () => document.body],
      ['in a field of the form', () => byId('stock-voucher-note')],
    ])('a held Ctrl+S %s saves once, also after the save has ended', async (_where, targetOf) => {
      routeParams.id = V1;
      voucherState.data = voucher();
      updateMock.mockResolvedValue(voucher({ version: 8 }));
      renderInMain();
      const target = targetOf();
      const press = (repeat: boolean) => {
        const event = createEvent.keyDown(target, { key: 's', ctrlKey: true, repeat });
        fireEvent(target, event);
        return event;
      };

      press(false);
      await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(1));
      await waitFor(() => expect(screen.getByRole('button', { name: 'Cập nhật' })).toBeEnabled());
      await new Promise((resolve) => setTimeout(resolve, 20));

      expect(press(true).defaultPrevented).toBe(true);
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(updateMock).toHaveBeenCalledTimes(1);

      // Let go and pressed again: that saves.
      press(false);
      await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(2));
    });

    // Here the first press went elsewhere: a repeat alone shows that it runs nothing.
    it('a repeat of a held Alt+n is taken from the browser and switches no tab, on <body> or in the form', () => {
      renderInMain();
      blur();

      expect(pressOnBody({ key: '3', altKey: true, repeat: true }).defaultPrevented).toBe(true);
      const field = byId('stock-voucher-at');
      const inForm = createEvent.keyDown(field, { key: '2', altKey: true, repeat: true });
      fireEvent(field, inForm);

      expect(inForm.defaultPrevented).toBe(true);
      expect(tab(/Hàng hóa/)).toHaveAttribute('data-state', 'active');
    });

    it('Alt+3 on <body> opens the payment tab and puts the focus on it; Alt+4 needs a saved voucher', async () => {
      renderPage('In');
      blur();

      expect(pressOnBody({ key: '4', altKey: true }).defaultPrevented).toBe(false);
      expect(tab(/Hàng hóa/)).toHaveAttribute('data-state', 'active');
      expect(pressOnBody({ key: '3', altKey: true }).defaultPrevented).toBe(true);
      expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active');
      await waitFor(() => expect(document.activeElement).toBe(tab(/Thanh toán/)));
    });

    it('does nothing while a dialog is open', async () => {
      routeParams.id = V1;
      voucherState.data = voucher();
      updateMock.mockResolvedValue(voucher());
      renderPage('In');
      fireEvent.click(screen.getByRole('button', { name: 'Hủy phiếu' }));
      expect(screen.getByRole('dialog')).toBeInTheDocument();

      expect(pressOnBody({ key: 's', ctrlKey: true }).defaultPrevented).toBe(false);
      expect(pressOnBody({ key: '3', altKey: true }).defaultPrevented).toBe(false);
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(updateMock).not.toHaveBeenCalled();
      // The modal dialog hides the rest of the page from the accessibility tree.
      expect(screen.getByRole('tab', { name: /Hàng hóa/, hidden: true })).toHaveAttribute('data-state', 'active');

      // Once the dialog is gone the same key saves.
      fireEvent.click(dialog().getByRole('button', { name: 'Quay lại' }));
      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      blur();
      pressOnBody({ key: 's', ctrlKey: true });
      await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(1));
      expect(cancelMock).not.toHaveBeenCalled();
    });

    // The product catalog of a Mã cell is a modal dialog inside the form in the React tree, so its keys reach the
    // form's handler. They are the dialog's, as on any other dialog: left to the browser, no save, no tab switched
    // behind the dialog.
    it('keys typed in the product catalog dialog neither save nor switch tabs', async () => {
      routeParams.id = V1;
      voucherState.data = voucher();
      updateMock.mockResolvedValue(voucher());
      renderInMain();
      fireEvent.change(byId('stock-line-product-code-0'), { target: { value: 'S' } });
      fireEvent.mouseDown(await screen.findByText('Xem danh mục đầy đủ'));
      const catalog = await screen.findByRole('dialog', { name: 'Danh mục hàng hóa' });
      const search = within(catalog).getAllByRole('textbox')[0];
      act(() => search.focus());

      const save = createEvent.keyDown(search, { key: 's', ctrlKey: true });
      fireEvent(search, save);
      const altTab = createEvent.keyDown(search, { key: '3', altKey: true });
      fireEvent(search, altTab);
      await new Promise((resolve) => setTimeout(resolve, 50));

      expect(save.defaultPrevented).toBe(false);
      expect(altTab.defaultPrevented).toBe(false);
      expect(updateMock).not.toHaveBeenCalled();
      // The modal dialog hides the rest of the page from the accessibility tree.
      expect(screen.getByRole('tab', { name: /Hàng hóa/, hidden: true })).toHaveAttribute('data-state', 'active');
      expect(document.activeElement).toBe(search);
    });

    // The popovers of the grid ("Cột & phím Enter", "Phím tắt") are role="dialog" too, but they do not block the page.
    it('Ctrl+S in a popover of the grid still saves', async () => {
      routeParams.id = V1;
      voucherState.data = voucher();
      updateMock.mockResolvedValue(voucher({ version: 8 }));
      renderInMain();
      fireEvent.click(screen.getByRole('button', { name: /Cột & phím Enter/ }));
      const checkbox = within(await screen.findByRole('dialog')).getByRole('checkbox', { name: 'Kho' });
      act(() => checkbox.focus());

      const save = createEvent.keyDown(checkbox, { key: 's', ctrlKey: true });
      fireEvent(checkbox, save);

      expect(save.defaultPrevented).toBe(true);
      await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(1));
    });

    // The listener hears every key pressed anywhere, each one typed in the grid too. Only a shortcut that it would take
    // is worth a search of the whole page for an open dialog.
    it('searches the page for an open dialog only for a shortcut that it would take', async () => {
      routeParams.id = V1;
      voucherState.data = voucher();
      updateMock.mockResolvedValue(voucher({ version: 8 }));
      renderInMain();
      const cell = byId('stock-line-quantity-0');
      // Outside main, where the app header is.
      const header = document.body.appendChild(document.createElement('input'));
      const querySelector = vi.spyOn(document, 'querySelector');
      const dialogSearches = () =>
        querySelector.mock.calls.filter(([selector]) => String(selector).includes('dialog')).length;
      try {
        // A digit typed in the grid, a key that is no shortcut on <body>, Ctrl+S outside main.
        fireEvent.keyDown(cell, { key: '5' });
        pressOnBody({ key: 'a' });
        pressOnBody({ key: 'c', ctrlKey: true });
        fireEvent.keyDown(header, { key: 's', ctrlKey: true });
        expect(dialogSearches()).toBe(0);

        // A shortcut that it takes looks, so that the key can be left to an open dialog.
        pressOnBody({ key: 's', ctrlKey: true });
        expect(dialogSearches()).toBe(1);
      } finally {
        querySelector.mockRestore();
        header.remove();
      }
      await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(1));
    });

    // Chrome sends a plain keydown Event, with no key, when an autofill entry is picked.
    it('a keydown with no key is no shortcut and throws nothing, in the form or on <body>', async () => {
      createMock.mockResolvedValue(voucher());
      renderInMain();
      const errors: unknown[] = [];
      const onError = (e: ErrorEvent) => {
        errors.push(e.error);
        e.preventDefault();
      };
      window.addEventListener('error', onError);
      try {
        act(() => {
          byId('stock-voucher-note').dispatchEvent(new Event('keydown', { bubbles: true }));
          document.body.dispatchEvent(new Event('keydown', { bubbles: true }));
        });
        await new Promise((resolve) => setTimeout(resolve, 20));
      } finally {
        window.removeEventListener('error', onError);
      }
      expect(errors).toEqual([]);
      expect(createMock).not.toHaveBeenCalled();
    });

    it('stops listening once the page is gone', async () => {
      createMock.mockResolvedValue(voucher());
      const { unmount } = renderPage('In');
      await fillPerUnitLine(2);
      unmount();

      expect(pressOnBody({ key: 's', ctrlKey: true }).defaultPrevented).toBe(false);
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(createMock).not.toHaveBeenCalled();
    });

    it.each([
      ['a cancelled voucher', { status: 'Cancelled' as const, canEdit: false }],
      ['a voucher the user may not edit', { canEdit: false }],
    ])('Ctrl+S on <body> does not save %s, as in the form; Alt+4 still opens the history', async (_label, overrides) => {
      routeParams.id = V1;
      voucherState.data = voucher(overrides);
      renderPage('In');

      // The key is taken as it is in the form, and nothing is sent.
      expect(pressOnBody({ key: 's', ctrlKey: true }).defaultPrevented).toBe(true);
      await new Promise((resolve) => setTimeout(resolve, 20));
      expect(updateMock).not.toHaveBeenCalled();

      expect(pressOnBody({ key: '4', altKey: true }).defaultPrevented).toBe(true);
      expect(tab(/Lịch sử/)).toHaveAttribute('data-state', 'active');
    });

    it('takes the keys of the title bar, in main#main-content, but never of anything outside main', async () => {
      createMock.mockResolvedValue(voucher());
      renderInMain();
      await fillPerUnitLine(2);

      // Outside main, where the app header and the sidebar are.
      const outside = document.body.appendChild(document.createElement('button'));
      try {
        act(() => outside.focus());
        const save = createEvent.keyDown(outside, { key: 's', ctrlKey: true });
        fireEvent(outside, save);
        const altTab = createEvent.keyDown(outside, { key: '3', altKey: true });
        fireEvent(outside, altTab);
        await new Promise((resolve) => setTimeout(resolve, 20));
        expect(save.defaultPrevented).toBe(false);
        expect(altTab.defaultPrevented).toBe(false);
        expect(createMock).not.toHaveBeenCalled();
        expect(tab(/Hàng hóa/)).toHaveAttribute('data-state', 'active');
      } finally {
        outside.remove();
      }

      // A title-bar button is in main but outside the form. Caps Lock on: the key is "S".
      const titleButton = screen.getByRole('button', { name: 'Lưu và thoát' });
      act(() => titleButton.focus());
      fireEvent.keyDown(titleButton, { key: 'S', ctrlKey: true });
      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
      // Ctrl+S is "save and stay", wherever it is pressed.
      await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(`/stock-in/${V1}`, { replace: true }));
    });

    // The saved voucher replaces the new one: the form remounts and the focused cell is gone.
    it('after a new voucher is saved and opened, Ctrl+S on <body> updates it', async () => {
      createMock.mockResolvedValue(voucher());
      updateMock.mockResolvedValue(voucher({ version: 8 }));
      const { rerender } = renderPage('In');
      await fillPerUnitLine(2);
      fireEvent.keyDown(byId('stock-line-quantity-0'), { key: 's', ctrlKey: true });
      await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(`/stock-in/${V1}`, { replace: true }));

      routeParams.id = V1;
      voucherState.data = voucher();
      rerender(<StockVoucherFormPage type="In" />);
      expect(screen.getByRole('button', { name: 'Cập nhật' })).toBeInTheDocument();
      expect(document.activeElement).toBe(document.body);

      pressOnBody({ key: 's', ctrlKey: true });

      await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(1));
      expect(updateMock.mock.calls[0][0].id).toBe(V1);
    });
  });

  it('clicking Phí VC on the totals bar focuses the freight input in the payment tab', async () => {
    renderPage('In');
    fireEvent.click(screen.getByRole('button', { name: /Phí VC/ }));
    expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active');
    await waitFor(() => expect(document.activeElement).toBe(byId('stock-freight')));
  });

  it('a server error on the fund switches to the payment tab, marks it and focuses the fund', async () => {
    createMock.mockRejectedValueOnce(apiError(400, 'VALIDATION', { fundId: ['Quỹ không hợp lệ'] }));
    renderPage('In');
    await fillPerUnitLine(3);
    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    await waitFor(() => expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active'));
    expect(screen.getByTestId('tab-error-payment')).toBeInTheDocument();
    await waitFor(() => expect(document.activeElement).toBe(byId('stock-fund')));
  });

  it('saving an incomplete line from another tab switches back to the lines and focuses the cell', async () => {
    renderPage('In');
    fireEvent.keyDown(byId('stock-voucher-at'), { key: '2', altKey: true });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    await waitFor(() => expect(tab(/Hàng hóa/)).toHaveAttribute('data-state', 'active'));
    expect(screen.getByTestId('tab-error-lines')).toBeInTheDocument();
    await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-0')));
    expect(createMock).not.toHaveBeenCalled();
  });

  it('saving a voucher with no lines from another tab lands the focus on Thêm dòng, not on the body', async () => {
    renderPage('In');
    fireEvent.keyDown(byId('stock-line-product-code-0'), { key: 'Delete', ctrlKey: true });
    await waitFor(() => expect(document.getElementById('stock-line-product-code-0')).toBeNull());
    fireEvent.keyDown(byId('stock-voucher-at'), { key: '2', altKey: true });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    await waitFor(() => expect(tab(/Hàng hóa/)).toHaveAttribute('data-state', 'active'));
    await waitFor(() => expect(document.activeElement).not.toBe(document.body));
    expect(document.activeElement).toBe(screen.getByRole('button', { name: 'Thêm dòng' }));
    expect(createMock).not.toHaveBeenCalled();
  });

  // SL of a line priced by size is computed and disabled: the cells it is computed from correct its error, and Số tấm
  // comes first on every such line.
  it.each<[string, Partial<StockVoucherLine>, string]>([
    ['by unit', {}, 'stock-line-quantity-1'],
    ['by metre', { pricingMode: 'PerLinearMeter', unitName: 'm', sheetCount: 2, length: 3000, quantity: 6 }, 'stock-line-sheet-count-1'],
    [
      'by square metre',
      { pricingMode: 'PerSquareMeter', unitName: 'm²', sheetCount: 2, length: 3000, width: 1000, quantity: 6 },
      'stock-line-sheet-count-1',
    ],
    [
      'by cubic metre',
      { pricingMode: 'PerCubicMeter', unitName: 'm³', sheetCount: 2, length: 3000, width: 100, thickness: 50, quantity: 0.03 },
      'stock-line-sheet-count-1',
    ],
  ])('a server error on SL of a line priced %s focuses the cell that corrects it', async (_mode, overrides, focusedId) => {
    routeParams.id = V1;
    const first = voucher().lines[0];
    voucherState.data = voucher({ lines: [first, { ...first, id: L2, sortOrder: 1, ...overrides }] });
    updateMock.mockRejectedValueOnce(apiError(400, 'VALIDATION', { 'lines[1].quantity': ['Số lượng quá lớn.'] }));
    renderPage('In');

    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() => expect(byId('stock-line-quantity-1').getAttribute('aria-invalid')).toBe('true'));
    await waitFor(() => expect(document.activeElement).toBe(byId(focusedId)));
  });

  it('a cancelled voucher keeps the fund and due date disabled', () => {
    routeParams.id = V1;
    voucherState.data = voucher({ type: 'Out', status: 'Cancelled', canEdit: false, reasonId: R_OUT, reasonDebtEffect: 'ReceivableIncrease' });
    renderPage('Out');
    expect(byId('stock-due-date')).toBeDisabled();
  });

  it('a client error on Phí VC switches to the payment tab and focuses the input', async () => {
    renderPage('In');
    await fillPerUnitLine(3);
    fireEvent.keyDown(byId('stock-voucher-at'), { key: '3', altKey: true });
    fireEvent.change(byId('stock-freight'), { target: { value: '-5' } });
    fireEvent.keyDown(byId('stock-voucher-at'), { key: '1', altKey: true });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    await waitFor(() => expect(tab(/Thanh toán/)).toHaveAttribute('data-state', 'active'));
    await waitFor(() => expect(document.activeElement).toBe(byId('stock-freight')));
    expect(createMock).not.toHaveBeenCalled();
  });

  it('shows the schema messages of a negative Phí VC and CK cả đơn under their rows', async () => {
    renderPage('In');
    await fillPerUnitLine(3);
    fireEvent.keyDown(byId('stock-voucher-at'), { key: '3', altKey: true });
    fireEvent.change(byId('stock-freight'), { target: { value: '-5' } });
    fireEvent.change(byId('stock-order-discount'), { target: { value: '-7' } });
    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    expect(await screen.findByText('Phí vận chuyển không được âm')).toBeInTheDocument();
    expect(screen.getByText('Chiết khấu không được âm')).toBeInTheDocument();
  });

  it('debt strip for a picked partner with permission, on both directions', async () => {
    partnerResults.data = [customer];
    partnerSummaryMock.mockReturnValue({
      data: { partnerId: customer.id, receivableBalance: 7_000_000, payableBalance: 0, creditLimit: 5_000_000, available: -2_000_000, overdueAmount: 0, creditDays: 15 },
      isLoading: false,
      isError: false,
    });
    const stripShown = () => screen.queryByTestId('partner-debt-receivable') !== null;

    const { unmount } = renderPage('Out');
    expect(stripShown()).toBe(false);
    fireEvent.change(byId('stock-partner'), { target: { value: 'KH' } });
    fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('KH01'));
    await waitFor(() => expect(stripShown()).toBe(true));
    expect(partnerSummaryMock).toHaveBeenCalledWith(customer.id, { enabled: true });
    // XBH raises the receivable: "after" shows, and is 7.000.000 + unpaid total (0 lines → 0).
    expect(screen.getByTestId('partner-debt-receivable-after')).toHaveTextContent('7.000.000');
    // A reason without debt keeps the strip but drops the after-voucher value.
    fireEvent.change(byId('stock-reason'), { target: { value: R_OUT_GIFT } });
    await waitFor(() => expect(screen.queryByTestId('partner-debt-receivable-after')).toBeNull());
    expect(stripShown()).toBe(true);
    unmount();

    // Stock-in shows it too (customer return).
    routeParams.id = V1;
    voucherState.data = voucher({
      reasonId: R_IN_CUSTOMER,
      reasonDebtEffect: 'ReceivableDecrease',
      partnerId: customer.id,
      partnerCode: customer.code,
      partnerName: customer.name,
      // Unpaid, so the saved voucher already lowered the server balance by its total.
      paidAmount: 0,
    });
    const second = renderPage('In');
    expect(stripShown()).toBe(true);
    // Unchanged edit: the saved impact is taken back before the current one applies, so nothing counts twice.
    expect(screen.getByTestId('partner-debt-receivable-after')).toHaveTextContent('7.000.000');
    second.unmount();

    // Without debt.partner_summary and reports.debt: hidden, and never requested.
    permissionState.granted = ['stock_in.view', 'stock_in.edit'];
    partnerSummaryMock.mockClear();
    renderPage('In');
    expect(stripShown()).toBe(false);
    expect(partnerSummaryMock).not.toHaveBeenCalled();
  });

  it('Enter moves through the main fields, never saves, and lands on the first empty line', async () => {
    renderPage('In');
    byId('stock-voucher-at').focus();
    fireEvent.keyDown(byId('stock-voucher-at'), { key: 'Enter' });
    expect(document.activeElement).toBe(byId('stock-reason'));
    fireEvent.keyDown(byId('stock-reason'), { key: 'Enter' });
    expect(document.activeElement).toBe(byId('stock-warehouse'));
    fireEvent.keyDown(byId('stock-warehouse'), { key: 'Enter' });
    expect(document.activeElement).toBe(byId('stock-payment-method'));
    fireEvent.keyDown(byId('stock-payment-method'), { key: 'Enter' });
    expect(document.activeElement).toBe(byId('stock-partner'));
    fireEvent.keyDown(byId('stock-partner'), { key: 'Enter' });
    expect(document.activeElement).toBe(byId('stock-partner-name'));
    fireEvent.keyDown(byId('stock-partner-name'), { key: 'Enter' });
    expect(document.activeElement).toBe(byId('stock-partner-address'));
    fireEvent.keyDown(byId('stock-partner-address'), { key: 'Enter' });
    await waitFor(() => expect(document.activeElement).toBe(byId('stock-line-product-code-0')));
    expect(createMock).not.toHaveBeenCalled();
  });

  // Implicit submission is blocked by preventing the Enter keydown (D7).
  it('Enter in a tab field is prevented so it cannot submit the form', () => {
    renderPage('In');
    const event = createEvent.keyDown(byId('stock-voucher-note'), { key: 'Enter' });
    fireEvent(byId('stock-voucher-note'), event);
    expect(event.defaultPrevented).toBe(true);
  });

  it('Enter picks a partner from the open list and then moves on to the partner name', async () => {
    partnerResults.data = [customer];
    renderPage('In');
    fireEvent.change(byId('stock-partner'), { target: { value: 'KH' } });
    await screen.findByRole('listbox');
    fireEvent.keyDown(byId('stock-partner'), { key: 'Enter' });
    await waitFor(() => expect(screen.queryByRole('listbox')).not.toBeInTheDocument());
    await waitFor(() => expect(document.activeElement).toBe(byId('stock-partner')));
    fireEvent.keyDown(byId('stock-partner'), { key: 'Enter' });
    expect(document.activeElement).toBe(byId('stock-partner-name'));
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
