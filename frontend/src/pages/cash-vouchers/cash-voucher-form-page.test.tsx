import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import type * as ReactRouterDom from 'react-router-dom';
import { CashVoucherFormPage } from './cash-voucher-form-page';
import { useBranchStore } from '@/stores/branch-store';
import type { CashReason } from '@/features/cash-reasons/types';
import type { Customer } from '@/features/customers/types';
import type { Fund } from '@/features/funds/types';
import type { OpenItem, OpenItemsParams } from '@/features/debt/types';
import type {
  CashPartnerSearchItem,
  CashVoucher,
  CashVoucherDefaults,
  CashVoucherType,
} from '@/features/cash-vouchers/types';

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
const defaultsMock = vi.fn();
const useCashReasonsMock = vi.fn();
const useOpenItemsMock = vi.fn();
const partnerSearchMock = vi.fn();
const partnerResults: { data: CashPartnerSearchItem[] } = { data: [] };
const openItemsState: { data: OpenItem[] } = { data: [] };
const voucherState: { data: CashVoucher | undefined } = { data: undefined };
const activitiesState: { data: unknown[] } = { data: [] };

const mutation = (fn: ReturnType<typeof vi.fn>) => ({ mutateAsync: fn, isPending: false });

vi.mock('@/features/cash-vouchers/hooks', () => ({
  useCashVoucher: (id: string | undefined) => ({
    data: id ? voucherState.data : undefined,
    isLoading: false,
    refetch: refetchMock,
  }),
  useCashVoucherDefaults: (...args: unknown[]) => defaultsMock(...args),
  useCashVoucherActivities: () => ({
    data: activitiesState.data,
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  }),
  useCashPartnerSearch: (type: string, keyword: string, activeOnly?: boolean) => {
    partnerSearchMock(type, keyword, activeOnly);
    return { data: keyword ? partnerResults.data : [], isFetching: false, isLoading: false, isError: false };
  },
  useCreateCashVoucher: () => mutation(createMock),
  useUpdateCashVoucher: () => mutation(updateMock),
  useCancelCashVoucher: () => mutation(cancelMock),
  useRestoreCashVoucher: () => mutation(restoreMock),
  useDeleteCashVoucher: () => mutation(deleteMock),
}));

// The quick add dialog of the partner field (mounted only while open).
const createCustomerMock = vi.fn();
const createSupplierMock = vi.fn();
vi.mock('@/features/customers/hooks', () => ({
  useCreateCustomer: () => ({
    mutateAsync: createCustomerMock,
    reset: vi.fn(),
    isPending: false,
    isError: false,
    error: null,
  }),
}));
vi.mock('@/features/suppliers/hooks', () => ({
  useCreateSupplier: () => ({
    mutateAsync: createSupplierMock,
    reset: vi.fn(),
    isPending: false,
    isError: false,
    error: null,
  }),
}));

vi.mock('@/features/debt/hooks', () => ({
  useOpenItems: (params: OpenItemsParams, opts?: { enabled?: boolean }) => {
    useOpenItemsMock(params, opts);
    const enabled = (opts?.enabled ?? true) && !!params.partnerId;
    return { data: enabled ? openItemsState.data : undefined, isLoading: false, isError: false };
  },
}));

let printProps: Record<string, unknown> = {};
vi.mock('@/components/voucher-print/voucher-print-button', () => ({
  VoucherPrintButton: (props: { disabled?: boolean; disabledReason?: string }) => {
    printProps = props as Record<string, unknown>;
    return <button type="button" data-testid="voucher-print" disabled={props.disabled} title={props.disabledReason} />;
  },
}));

const FUND_CASH = '11111111-1111-4111-8111-111111111111';
const FUND_BANK = '11111111-1111-4111-8111-111111111112';
const R_TKH = '22222222-2222-4222-8222-222222222221';
const R_TK = '22222222-2222-4222-8222-222222222222';
const R_TNCC = '22222222-2222-4222-8222-222222222223';
const R_TPX = '22222222-2222-4222-8222-222222222224';
const R_THNCC = '22222222-2222-4222-8222-222222222225';
const R_OLD = '22222222-2222-4222-8222-222222222226';
const V1 = '55555555-5555-4555-8555-555555555555';
const L1 = '66666666-6666-4666-8666-666666666666';
const SV1 = '77777777-7777-4777-8777-777777777777';
const ENTRY_A = 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa';
const ENTRY_B = 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb';

const funds: Fund[] = [
  {
    id: FUND_CASH,
    branchId: 'b1',
    code: 'QTM',
    name: 'Quỹ tiền mặt',
    kind: 'Cash',
    isDefault: true,
    openingAmount: 0,
    isActive: true,
  },
  {
    id: FUND_BANK,
    branchId: 'b1',
    code: 'NH01',
    name: 'Ngân hàng ACB',
    kind: 'Bank',
    isDefault: true,
    openingAmount: 0,
    isActive: true,
  },
];

const reasons: CashReason[] = [
  {
    id: R_TKH,
    code: 'TKH',
    name: 'Thu tiền khách hàng',
    type: 'Receipt',
    partnerType: 'Customer',
    debtEffect: 'ReceivableDecrease',
    isSystem: true,
    isAuto: false,
  },
  {
    id: R_TK,
    code: 'TK',
    name: 'Thu khác',
    type: 'Receipt',
    partnerType: 'None',
    debtEffect: 'None',
    isSystem: true,
    isAuto: false,
  },
  {
    id: R_THNCC,
    code: 'THNCC',
    name: 'Thu hoàn tiền NCC',
    type: 'Receipt',
    partnerType: 'Supplier',
    debtEffect: 'PayableIncrease',
    isSystem: true,
    isAuto: false,
  },
  {
    id: R_TNCC,
    code: 'TNCC',
    name: 'Trả tiền nhà cung cấp',
    type: 'Payment',
    partnerType: 'Supplier',
    debtEffect: 'PayableDecrease',
    isSystem: true,
    isAuto: false,
  },
];

vi.mock('@/features/funds/hooks', () => ({ useFunds: () => ({ data: funds }) }));
vi.mock('@/features/cash-reasons/hooks', () => ({
  useCashReasons: (params: { type?: CashVoucherType }) => {
    useCashReasonsMock(params);
    return { data: reasons.filter((r) => !params?.type || r.type === params.type) };
  },
}));

const DEFAULT_AT = '2026-10-07T01:30:00.000Z';

const customer: CashPartnerSearchItem = {
  id: '88888888-8888-4888-8888-888888888888',
  code: 'KH01',
  name: 'Công ty ABC',
  taxCode: '0100000001',
  companyAddress: '12 Đường Số 1',
  status: 'Active',
  isCustomer: true,
  isSupplier: false,
};

const supplier: CashPartnerSearchItem = {
  id: '88888888-8888-4888-8888-888888888889',
  code: 'NCC001',
  name: 'Công ty TNHH Cách nhiệt Đông Á',
  companyAddress: '5 Đường Số 2',
  status: 'Active',
  isCustomer: false,
  isSupplier: true,
};

// What POST /customers returns for the quick add.
const createdCustomer: Customer = {
  id: '88888888-8888-4888-8888-888888888890',
  code: 'KH0099',
  name: 'Công ty mới',
  group: 'Company',
  status: 'Active',
  createdAt: DEFAULT_AT,
  isCustomer: true,
  isSupplier: false,
};

function openItem(overrides: Partial<OpenItem>): OpenItem {
  return {
    entryId: ENTRY_A,
    sourceType: 'StockOut',
    sourceId: '99999999-9999-4999-8999-999999999999',
    docCode: 'PX00001',
    docDate: '2026-10-01',
    dueDate: null,
    postedAt: '2026-10-01T03:00:00Z',
    direction: 'Increase',
    amount: 0,
    settled: 0,
    open: 0,
    ...overrides,
  };
}

// Listed out of due order: the suggestion fills PX00001 (due 10-10) first.
const OPEN_ITEMS: OpenItem[] = [
  openItem({ entryId: ENTRY_B, docCode: 'PX00002', dueDate: '2026-10-20', amount: 2_000, open: 2_000 }),
  openItem({ entryId: ENTRY_A, docCode: 'PX00001', dueDate: '2026-10-10', amount: 1_000, open: 1_000 }),
];

function defaultsFor(type: CashVoucherType): CashVoucherDefaults {
  return {
    nextCode: type === 'Receipt' ? 'PT00001' : 'PC00001',
    voucherAt: DEFAULT_AT,
    fundId: FUND_CASH,
    reasonId: type === 'Receipt' ? R_TKH : R_TNCC,
  };
}

function voucher(overrides: Partial<CashVoucher> = {}): CashVoucher {
  return {
    id: V1,
    type: 'Receipt',
    code: 'PT00007',
    voucherAt: DEFAULT_AT,
    branchId: 'b1',
    fundId: FUND_CASH,
    fundCode: 'QTM',
    fundName: 'Quỹ tiền mặt',
    fundKind: 'Cash',
    reasonId: R_TKH,
    reasonCode: 'TKH',
    reasonName: 'Thu tiền khách hàng',
    reasonDebtEffect: 'ReceivableDecrease',
    partnerId: customer.id,
    partnerCode: customer.code,
    partnerName: customer.name,
    personName: 'Anh Nam',
    note: 'Ghi chú cũ',
    total: 1_000,
    status: 'Active',
    ownerUserId: 'user-test',
    version: 7,
    isManaged: false,
    canEdit: true,
    canCancel: true,
    canDelete: true,
    lines: [{ id: L1, sortOrder: 0, description: 'Thu tiền hàng', amount: 1_000 }],
    allocations: [
      {
        settlementId: 's1',
        entryId: ENTRY_A,
        sourceType: 'StockOut',
        sourceId: '99999999-9999-4999-8999-999999999999',
        docCode: 'PX00001',
        docDate: '2026-10-01',
        amount: 1_000,
        origin: 'AtVoucher',
        isOwn: true,
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

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

function typeMoney(input: HTMLElement, text: string) {
  fireEvent.focus(input);
  fireEvent.change(input, { target: { value: text } });
  fireEvent.blur(input);
}

function fillLine(amount: string, description = 'Thu tiền hàng') {
  fireEvent.change(screen.getByRole('textbox', { name: 'Diễn giải dòng 1' }), { target: { value: description } });
  typeMoney(screen.getByRole('textbox', { name: 'Số tiền dòng 1' }), amount);
}

async function pickCustomer() {
  fireEvent.change(byId('cash-partner'), { target: { value: 'KH' } });
  fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText('KH01'));
  await waitFor(() => expect(byId('cash-partner-name').value).toBe(customer.name));
}

function allocationInput(docCode: string): HTMLInputElement {
  return screen.getByRole('textbox', { name: `Phân bổ ${docCode}` }) as HTMLInputElement;
}

function renderPage(type: CashVoucherType = 'Receipt') {
  return render(<CashVoucherFormPage type={type} />);
}

function dialog() {
  return within(screen.getByRole('dialog'));
}

describe('CashVoucherFormPage', () => {
  beforeEach(() => {
    printProps = {};
    routeParams.id = 'new';
    voucherState.data = undefined;
    activitiesState.data = [];
    openItemsState.data = OPEN_ITEMS;
    partnerResults.data = [customer];
    navigateMock.mockReset();
    toastMock.mockReset();
    createMock.mockReset();
    updateMock.mockReset();
    cancelMock.mockReset();
    restoreMock.mockReset();
    deleteMock.mockReset();
    refetchMock.mockReset();
    useCashReasonsMock.mockReset();
    useOpenItemsMock.mockReset();
    partnerSearchMock.mockReset();
    createCustomerMock.mockReset();
    createSupplierMock.mockReset();
    defaultsMock.mockReset();
    defaultsMock.mockImplementation((type: CashVoucherType) => ({
      data: defaultsFor(type),
      isLoading: false,
      isFetchedAfterMount: true,
    }));
    useBranchStore.setState({ workingBranchId: 'b1' });
  });

  it('new receipt shows expected code and default fund and reason', () => {
    renderPage('Receipt');

    expect(screen.getByRole('heading', { name: 'Thêm phiếu thu' })).toBeInTheDocument();
    expect(screen.getByText('Số phiếu dự kiến: PT00001')).toBeInTheDocument();
    expect(defaultsMock).toHaveBeenCalledWith('Receipt', { enabled: true, fresh: true });
    expect(byId('cash-fund').value).toBe(FUND_CASH);
    expect(within(byId('cash-fund')).getByRole('option', { name: 'QTM – Quỹ tiền mặt' })).toBeInTheDocument();
    expect(screen.getByText('Tiền mặt')).toBeInTheDocument();
    expect(useCashReasonsMock).toHaveBeenCalledWith({ type: 'Receipt' });
    expect(byId('cash-reason').value).toBe(R_TKH);
    const reasonOptions = within(byId('cash-reason')).getAllByRole('option').map((o) => o.textContent);
    expect(reasonOptions).not.toContain('Trả tiền nhà cung cấp');
    expect(screen.getByText('Người nộp')).toBeInTheDocument();
    expect(screen.getByText('Lý do nộp')).toBeInTheDocument();
    // No partner yet: no allocation card.
    expect(screen.queryByText('Phân bổ chứng từ')).not.toBeInTheDocument();
  });

  it('create sends mapped payload with suggested allocations', async () => {
    createMock.mockResolvedValue(voucher());
    renderPage('Receipt');

    await pickCustomer();
    fillLine('2500');

    expect(await screen.findByText('Phân bổ chứng từ')).toBeInTheDocument();
    expect(useOpenItemsMock).toHaveBeenLastCalledWith(
      { partnerId: customer.id, side: 'Receivable', direction: 'Increase', forSourceType: 'Receipt', forSourceId: undefined },
      { enabled: true },
    );
    await waitFor(() => expect(allocationInput('PX00002')).toHaveValue('1.500'));
    expect(allocationInput('PX00001')).toHaveValue('1.000');
    expect(partnerSearchMock).toHaveBeenLastCalledWith('Receipt', expect.any(String), true);

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    const payload = createMock.mock.calls[0][0];
    expect(payload).toMatchObject({
      type: 'Receipt',
      voucherAt: DEFAULT_AT,
      fundId: FUND_CASH,
      reasonId: R_TKH,
      partnerId: customer.id,
      partnerName: customer.name,
      lines: [{ description: 'Thu tiền hàng', amount: 2_500 }],
    });
    expect(payload.allocations).toEqual([
      { entryId: ENTRY_A, amount: 1_000 },
      { entryId: ENTRY_B, amount: 1_500 },
    ]);
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(`/receipts/${V1}`, { replace: true }));
  });

  it('a manual allocation edit stops following the suggestion', async () => {
    createMock.mockResolvedValue(voucher());
    renderPage('Receipt');
    await pickCustomer();
    fillLine('2500');
    await waitFor(() => expect(allocationInput('PX00002')).toHaveValue('1.500'));

    typeMoney(allocationInput('PX00002'), '500');
    typeMoney(screen.getByRole('textbox', { name: 'Số tiền dòng 1' }), '3000');

    expect(allocationInput('PX00002')).toHaveValue('500');
    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    expect(createMock.mock.calls[0][0].allocations).toEqual([
      { entryId: ENTRY_B, amount: 500 },
      { entryId: ENTRY_A, amount: 1_000 },
    ]);
  });

  it('blocks saving while an allocation is above its open amount', async () => {
    createMock.mockResolvedValue(voucher());
    renderPage('Receipt');
    await pickCustomer();
    fillLine('2500');
    await waitFor(() => expect(allocationInput('PX00001')).toHaveValue('1.000'));

    typeMoney(allocationInput('PX00001'), '1500');

    expect(await screen.findByText('Phân bổ vượt số còn nợ')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lưu tạm' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Lưu và thoát' })).toBeDisabled();
    // Ctrl+S goes through the same guard.
    fireEvent.keyDown(byId('cash-fund'), { key: 's', ctrlKey: true });
    await new Promise((resolve) => setTimeout(resolve, 20));
    expect(createMock).not.toHaveBeenCalled();
  });

  it('blocks saving while the allocations exceed the voucher amount', async () => {
    createMock.mockResolvedValue(voucher());
    renderPage('Receipt');
    await pickCustomer();
    fillLine('2500');
    await waitFor(() => expect(allocationInput('PX00002')).toHaveValue('1.500'));

    typeMoney(allocationInput('PX00002'), '1400');
    typeMoney(screen.getByRole('textbox', { name: 'Số tiền dòng 1' }), '1000');

    expect(await screen.findByText('Tổng phân bổ vượt số tiền phiếu')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lưu tạm' })).toBeDisabled();

    // Back inside the capacity: saving works again.
    typeMoney(allocationInput('PX00002'), '0');
    await waitFor(() => expect(screen.getByRole('button', { name: 'Lưu tạm' })).toBeEnabled());
  });

  it('changing to a reason without debt effect hides allocations and sends an empty list', async () => {
    createMock.mockResolvedValue(voucher());
    renderPage('Receipt');
    await pickCustomer();
    fillLine('2500');
    await waitFor(() => expect(allocationInput('PX00001')).toHaveValue('1.000'));

    fireEvent.change(byId('cash-reason'), { target: { value: R_TK } });

    await waitFor(() => expect(screen.queryByText('Phân bổ chứng từ')).not.toBeInTheDocument());
    expect(byId('cash-partner')).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    const payload = createMock.mock.calls[0][0];
    expect(payload.reasonId).toBe(R_TK);
    expect(payload.partnerId).toBeUndefined();
    expect(payload.allocations).toEqual([]);
  });

  it('credit/fund warning resends with accumulated flags', async () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    updateMock
      .mockRejectedValueOnce(
        apiError(422, 'DEBT_SETTLEMENTS_WILL_BE_REMOVED', { PX00009: ['Bỏ đối trừ 500'] }),
      )
      .mockRejectedValueOnce(apiError(422, 'NEGATIVE_FUND_WARNING', { QTM: ['Quỹ âm 200 tại 07/10/2026'] }))
      .mockResolvedValueOnce(voucher({ version: 8 }));
    renderPage('Receipt');

    fireEvent.change(byId('cash-voucher-note'), { target: { value: 'Sửa' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

    expect(await screen.findByText('Bỏ đối trừ chứng từ')).toBeInTheDocument();
    expect(dialog().getByText('PX00009')).toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Vẫn lưu' }));

    expect(await screen.findByText('Quỹ bị âm')).toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Vẫn lưu' }));

    await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(3));
    const [first, second, third] = updateMock.mock.calls.map((c) => c[0]);
    expect(first.id).toBe(V1);
    expect(first.data.version).toBe(7);
    expect(first.data.note).toBe('Sửa');
    expect(first.data.confirmRemoveSettlements).toBeFalsy();
    expect(first.data.acknowledgeNegativeFund).toBeFalsy();
    expect(second.data).toEqual({ ...first.data, confirmRemoveSettlements: true });
    expect(third.data).toEqual({ ...first.data, confirmRemoveSettlements: true, acknowledgeNegativeFund: true });
    await waitFor(() =>
      expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success', title: 'Đã cập nhật phiếu' })),
    );
  });

  it('blocked negative fund shows close only and does not resend', async () => {
    createMock.mockRejectedValueOnce(apiError(422, 'NEGATIVE_FUND_BLOCKED', { QTM: ['Quỹ âm 200'] }));
    renderPage('Receipt');
    fillLine('2500');

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    expect(await screen.findByText('Không đủ tiền trong quỹ')).toBeInTheDocument();
    expect(dialog().queryByRole('button', { name: 'Vẫn lưu' })).not.toBeInTheDocument();
    fireEvent.click(dialog().getByRole('button', { name: 'Đóng' }));
    await waitFor(() => expect(screen.queryByText('Không đủ tiền trong quỹ')).not.toBeInTheDocument());
    expect(createMock).toHaveBeenCalledTimes(1);
  });

  it('cancel confirmation resends cancel with the settlement flag', async () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    cancelMock
      .mockRejectedValueOnce(apiError(422, 'DEBT_SETTLEMENTS_WILL_BE_REMOVED', { PX00001: ['Bỏ đối trừ 1.000'] }))
      .mockResolvedValueOnce(voucher({ status: 'Cancelled', version: 8 }));
    renderPage('Receipt');

    fireEvent.click(screen.getByRole('button', { name: 'Hủy phiếu' }));
    fireEvent.click(dialog().getByRole('button', { name: 'Hủy phiếu' }));
    fireEvent.click(await screen.findByRole('button', { name: 'Vẫn hủy phiếu' }));

    await waitFor(() => expect(cancelMock).toHaveBeenCalledTimes(2));
    expect(cancelMock.mock.calls[0][0]).toEqual({ id: V1, body: { version: 7 } });
    expect(cancelMock.mock.calls[1][0]).toEqual({ id: V1, body: { version: 7, confirmRemoveSettlements: true } });
  });

  it('concurrency resets the form', async () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    updateMock.mockRejectedValueOnce(apiError(409, 'CONCURRENCY'));
    refetchMock.mockResolvedValueOnce({ status: 'success', data: voucher({ version: 8, note: 'Ghi chú máy chủ' }) });
    renderPage('Receipt');

    fireEvent.change(byId('cash-voucher-note'), { target: { value: 'Sửa cục bộ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() => expect(byId('cash-voucher-note').value).toBe('Ghi chú máy chủ'));
    expect(updateMock).toHaveBeenCalledTimes(1);
    expect(toastMock).toHaveBeenCalledWith(
      expect.objectContaining({
        variant: 'destructive',
        title: 'Phiếu đã được người khác cập nhật. Đã tải lại dữ liệu — các thay đổi chưa lưu đã bị bỏ.',
      }),
    );
  });

  it('validation maps allocation errors to the grid', async () => {
    createMock.mockRejectedValueOnce(
      apiError(400, 'VALIDATION', {
        'allocations[1].amount': ['Vượt số còn lại 1.200.'],
        'lines[0].description': ['Diễn giải không hợp lệ'],
      }),
    );
    renderPage('Receipt');
    await pickCustomer();
    fillLine('2500');
    await waitFor(() => expect(allocationInput('PX00002')).toHaveValue('1.500'));

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    // Payload index 1 is PX00002 (the suggestion fills PX00001 first).
    await waitFor(() => expect(allocationInput('PX00002')).toHaveAttribute('aria-invalid', 'true'));
    expect(screen.getByText('Vượt số còn lại 1.200.')).toBeInTheDocument();
    expect(allocationInput('PX00001')).not.toHaveAttribute('aria-invalid');
    expect(screen.getByRole('textbox', { name: 'Diễn giải dòng 1' })).toHaveAttribute('aria-invalid', 'true');
  });

  it('the close button of a managed voucher carries the muted closeForm icon', () => {
    routeParams.id = V1;
    voucherState.data = voucher({ isManaged: true, canEdit: false, allocations: [] });
    renderPage('Receipt');

    const icon = screen.getByRole('button', { name: 'Đóng' }).querySelector('[data-icon="closeForm"]');
    expect(icon).toHaveClass('text-icon-muted');
  });

  it('managed voucher is read-only with a link to the stock voucher', () => {
    routeParams.id = V1;
    voucherState.data = voucher({
      reasonId: R_TPX,
      reasonCode: 'TPX',
      reasonName: 'Thu tiền theo phiếu xuất',
      isManaged: true,
      sourceStockVoucherId: SV1,
      sourceStockVoucherCode: 'PX00012',
      sourceStockVoucherType: 'Out',
      canEdit: false,
      allocations: [],
    });
    renderPage('Receipt');

    expect(screen.getByText('Phiếu thu tự động theo phiếu xuất PX00012 — sửa trên phiếu kho.')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Mở phiếu kho' })).toHaveAttribute('href', `/stock-out/${SV1}`);
    expect(byId('cash-reason').value).toBe(R_TPX);
    expect(byId('cash-voucher-note')).toBeDisabled();
    expect(byId('cash-fund')).toBeDisabled();
    expect(screen.getByRole('textbox', { name: 'Số tiền dòng 1' })).toBeDisabled();
    for (const name of ['Cập nhật', 'Hủy phiếu', 'Khôi phục', 'Xóa', 'Lưu tạm']) {
      expect(screen.queryByRole('button', { name })).not.toBeInTheDocument();
    }
    fireEvent.click(screen.getByRole('button', { name: 'Đóng' }));
    expect(navigateMock).toHaveBeenCalledWith('/receipts');
  });

  it('cancelled voucher offers restore and delete only', async () => {
    routeParams.id = V1;
    voucherState.data = voucher({ status: 'Cancelled', canEdit: false });
    activitiesState.data = [
      { id: 'a1', action: 'Cancelled', actorName: 'Admin', occurredAt: DEFAULT_AT, description: 'Hủy phiếu PT00007' },
    ];
    restoreMock.mockResolvedValueOnce(voucher({ version: 8 }));
    renderPage('Receipt');

    expect(screen.getByText('Đã hủy')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Khôi phục' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xóa' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Cập nhật' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hủy phiếu' })).not.toBeInTheDocument();
    expect(byId('cash-voucher-note')).toBeDisabled();
    expect(screen.getByRole('textbox', { name: 'Số tiền dòng 1' })).toBeDisabled();
    expect(screen.getByText('Lịch sử')).toBeInTheDocument();
    expect(screen.getByText('Hủy phiếu PT00007')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Khôi phục' }));
    fireEvent.click(dialog().getByRole('button', { name: 'Khôi phục' }));

    await waitFor(() => expect(restoreMock).toHaveBeenCalledWith({ id: V1, body: { version: 7 } }));
  });

  it('no print button on a new voucher', () => {
    renderPage('Receipt');

    expect(screen.queryByTestId('voucher-print')).not.toBeInTheDocument();
  });

  it('prints a saved voucher', () => {
    routeParams.id = V1;
    const saved = voucher();
    voucherState.data = saved;
    renderPage('Receipt');

    expect(screen.getByTestId('voucher-print')).toBeEnabled();
    expect(printProps).toMatchObject({
      kind: 'cash',
      type: 'Receipt',
      vouchers: [{ id: saved.id, code: saved.code }],
      disabled: false,
    });
  });

  it('print waits for unsaved changes', () => {
    routeParams.id = V1;
    voucherState.data = voucher();
    renderPage('Receipt');

    fireEvent.change(byId('cash-voucher-note'), { target: { value: 'Sửa chưa lưu' } });

    expect(printProps).toMatchObject({ disabled: true, disabledReason: 'Lưu thay đổi trước khi in' });
  });

  it('a cancelled voucher can be printed', () => {
    routeParams.id = V1;
    voucherState.data = voucher({ status: 'Cancelled', canEdit: false });
    renderPage('Receipt');

    expect(screen.getByTestId('voucher-print')).toBeEnabled();
    expect(printProps).toMatchObject({ disabled: false });
  });

  it('payment labels', () => {
    renderPage('Payment');

    expect(screen.getByRole('heading', { name: 'Thêm phiếu chi' })).toBeInTheDocument();
    expect(screen.getByText('Số phiếu dự kiến: PC00001')).toBeInTheDocument();
    expect(screen.getByText('Người nhận')).toBeInTheDocument();
    expect(screen.getByText('Lý do chi')).toBeInTheDocument();
    expect(byId('cash-reason').value).toBe(R_TNCC);
    expect(useCashReasonsMock).toHaveBeenCalledWith({ type: 'Payment' });
  });

  it('a voucher of the other type redirects to its own route', () => {
    routeParams.id = V1;
    voucherState.data = voucher({ type: 'Payment', code: 'PC00007' });
    renderPage('Receipt');

    expect(navigateMock).toHaveBeenCalledWith(`/payments/${V1}`, { replace: true });
  });

  describe('redesign', () => {
    const line = (n: number) => screen.getByRole('textbox', { name: `Diễn giải dòng ${n}` }) as HTMLInputElement;
    const active = () => document.activeElement as HTMLElement;
    const enterOn = (el: Element, init: KeyboardEventInit = {}) => fireEvent.keyDown(el, { key: 'Enter', ...init });
    const settle = () => new Promise((resolve) => setTimeout(resolve, 20));
    const AUTO_KH01 = 'Thu tiền khách hàng - Công ty ABC';
    const PICK_MESSAGE = 'Vui lòng chọn đối tượng từ danh sách';

    async function pickPartner(item: CashPartnerSearchItem, typed: string) {
      partnerResults.data = [item];
      fireEvent.change(byId('cash-partner'), { target: { value: typed } });
      fireEvent.mouseDown(within(await screen.findByRole('listbox')).getByText(item.code));
      await waitFor(() => expect(byId('cash-partner-name').value).toBe(item.name));
    }

    function withoutDefaultReason() {
      defaultsMock.mockImplementation((type: CashVoucherType) => ({
        data: { ...defaultsFor(type), reasonId: '' },
        isLoading: false,
        isFetchedAfterMount: true,
      }));
    }

    async function addLineWithButton(n: number) {
      fireEvent.click(screen.getByRole('button', { name: /Thêm dòng/ }));
      await waitFor(() => expect(line(n)).toHaveFocus());
    }

    const readOnlyVouchers = () => [
      voucher({ isManaged: true, canEdit: false, allocations: [], description: '' }),
      voucher({ status: 'Cancelled', canEdit: false, description: '' }),
    ];

    it('1. a new voucher opens on the partner code', async () => {
      renderPage('Receipt');

      await waitFor(() => expect(byId('cash-partner')).toHaveFocus());
    });

    it('1. a saved or read-only voucher does not take the focus', async () => {
      routeParams.id = V1;
      for (const data of [voucher(), ...readOnlyVouchers()]) {
        voucherState.data = data;
        const view = renderPage('Receipt');
        await settle();
        expect(document.activeElement).toBe(document.body);
        view.unmount();
      }
    });

    it('2. auto text follows the section-01 §3.3 example', async () => {
      renderPage('Receipt');
      expect(byId('cash-description').value).toBe('Thu tiền khách hàng');
      expect(line(1).value).toBe('Thu tiền khách hàng');

      await pickCustomer();
      expect(byId('cash-description').value).toBe(AUTO_KH01);
      expect(line(1).value).toBe(AUTO_KH01);
      await waitFor(() => expect(byId('cash-partner-name')).toHaveFocus());

      fireEvent.change(byId('cash-partner-name'), { target: { value: 'Công ty ABC Hà Nội' } });
      expect(byId('cash-description').value).toBe('Thu tiền khách hàng - Công ty ABC Hà Nội');
      expect(line(1).value).toBe('Thu tiền khách hàng - Công ty ABC Hà Nội');

      fireEvent.change(byId('cash-description'), { target: { value: 'Thu nợ tháng 10' } });
      expect(line(1).value).toBe('Thu nợ tháng 10');

      fireEvent.change(byId('cash-reason'), { target: { value: R_THNCC } });
      expect(byId('cash-reason').value).toBe(R_THNCC);
      expect(byId('cash-description').value).toBe('Thu nợ tháng 10');
      expect(line(1).value).toBe('Thu nợ tháng 10');
    });

    it('3. a saved voucher opens clean and keeps its hand-written description', () => {
      routeParams.id = V1;
      voucherState.data = voucher({ description: 'Thu theo hợp đồng 12' });
      renderPage('Receipt');

      expect(screen.getByTestId('voucher-print')).toBeEnabled();
      expect(printProps.disabled).toBeFalsy();
      expect(byId('cash-description').value).toBe('Thu theo hợp đồng 12');

      fireEvent.change(byId('cash-reason'), { target: { value: R_THNCC } });
      expect(byId('cash-description').value).toBe('Thu theo hợp đồng 12');
      expect(line(1).value).toBe('Thu tiền hàng');
    });

    it('3. a saved voucher whose description is the auto text follows a partner change', () => {
      routeParams.id = V1;
      voucherState.data = voucher({
        description: AUTO_KH01,
        lines: [{ id: L1, sortOrder: 0, description: AUTO_KH01, amount: 1_000 }],
      });
      renderPage('Receipt');
      expect(printProps.disabled).toBeFalsy();

      fireEvent.change(byId('cash-partner-name'), { target: { value: 'Công ty ABC Hà Nội' } });

      expect(byId('cash-description').value).toBe('Thu tiền khách hàng - Công ty ABC Hà Nội');
      expect(line(1).value).toBe('Thu tiền khách hàng - Công ty ABC Hà Nội');
      expect(printProps.disabled).toBe(true);
    });

    it('3. a background refetch writes nothing and the reloaded voucher becomes the baseline', () => {
      routeParams.id = V1;
      voucherState.data = voucher({ description: 'Tự ghi' });
      const view = renderPage('Receipt');

      const reloadedAuto = 'Thu tiền khách hàng - Công ty ABC Mới';
      voucherState.data = voucher({
        version: 8,
        note: 'Ghi chú máy chủ',
        partnerName: 'Công ty ABC Mới',
        description: reloadedAuto,
      });
      view.rerender(<CashVoucherFormPage type="Receipt" />);

      expect(byId('cash-voucher-note').value).toBe('Ghi chú máy chủ');
      expect(printProps.disabled).toBeFalsy();
      expect(byId('cash-description').value).toBe(reloadedAuto);

      fireEvent.change(byId('cash-reason'), { target: { value: R_THNCC } });
      expect(byId('cash-description').value).toBe('Thu hoàn tiền NCC - Công ty ABC Mới');
    });

    it('3. after a concurrency reload the form is clean and follows the reloaded baseline', async () => {
      routeParams.id = V1;
      voucherState.data = voucher({ description: 'Tự ghi' });
      updateMock.mockRejectedValueOnce(apiError(409, 'CONCURRENCY'));
      const reloadedAuto = 'Thu tiền khách hàng - Công ty ABC Mới';
      refetchMock.mockResolvedValueOnce({
        status: 'success',
        data: voucher({ version: 8, note: 'Ghi chú máy chủ', partnerName: 'Công ty ABC Mới', description: reloadedAuto }),
      });
      renderPage('Receipt');

      await pickPartner(supplier, 'NCC');
      expect(screen.getByText('Đã đổi lý do cho hợp với NCC001 (nhà cung cấp).')).toHaveAttribute('role', 'status');
      fireEvent.change(byId('cash-voucher-note'), { target: { value: 'Sửa cục bộ' } });
      expect(printProps.disabled).toBe(true);
      fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

      await waitFor(() => expect(byId('cash-voucher-note').value).toBe('Ghi chú máy chủ'));
      await waitFor(() => expect(printProps.disabled).toBeFalsy());
      expect(byId('cash-description').value).toBe(reloadedAuto);
      // The other user's reason and partner are shown: the switch notice of the dropped edit is gone.
      expect(byId('cash-reason').value).toBe(R_TKH);
      expect(byId('cash-partner').value).toBe('KH01');
      expect(screen.queryByText(/Đã đổi lý do/)).toBeNull();
      expect(screen.queryByRole('status')).toBeNull();

      fireEvent.change(byId('cash-reason'), { target: { value: R_THNCC } });
      expect(byId('cash-description').value).toBe('Thu hoàn tiền NCC - Công ty ABC Mới');
    });

    it('4. picking a supplier switches the reason; a later reason change keeps it with a red line', async () => {
      renderPage('Receipt');

      await pickPartner(supplier, 'NCC');

      expect(byId('cash-reason').value).toBe(R_THNCC);
      const notice = screen.getByText('Đã đổi lý do cho hợp với NCC001 (nhà cung cấp).');
      expect(notice).toHaveAttribute('role', 'status');
      expect(byId('cash-reason')).toHaveClass('ring-amber-400');
      expect(byId('cash-description').value).toBe('Thu hoàn tiền NCC - Công ty TNHH Cách nhiệt Đông Á');

      fireEvent.change(byId('cash-reason'), { target: { value: R_TKH } });
      expect(byId('cash-partner').value).toBe('NCC001');
      expect(byId('cash-partner-name').value).toBe(supplier.name);
      expect(
        screen.getByText('Lý do "Thu tiền khách hàng" cần khách hàng — NCC001 không phải khách hàng'),
      ).toHaveClass('text-destructive');
      expect(screen.queryByText(/Đã đổi lý do/)).toBeNull();

      fireEvent.change(byId('cash-reason'), { target: { value: R_TK } });
      expect(byId('cash-partner').value).toBe('');
      expect(byId('cash-partner-name').value).toBe('');
      expect(byId('cash-partner-address').value).toBe('');
      expect(screen.queryByText(/không phải khách hàng/)).toBeNull();
      expect(screen.queryByText(/Đã đổi lý do/)).toBeNull();
    });

    it('4. clearing the picked partner drops the yellow line and the partner from the auto text', async () => {
      renderPage('Receipt');
      await pickPartner(supplier, 'NCC');
      expect(screen.getByText(/Đã đổi lý do/)).toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: 'Bỏ chọn đối tượng' }));

      await waitFor(() => expect(screen.queryByText(/Đã đổi lý do/)).toBeNull());
      expect(byId('cash-reason').value).toBe(R_THNCC);
      expect(byId('cash-partner-name').value).toBe('');
      expect(byId('cash-description').value).toBe('Thu hoàn tiền NCC');
      expect(line(1).value).toBe('Thu hoàn tiền NCC');
    });

    it('4. a second pick that fits the switched reason keeps it and drops the yellow line', async () => {
      createSupplierMock.mockResolvedValue({
        ...createdCustomer,
        id: '88888888-8888-4888-8888-888888888891',
        code: 'NCC0099',
        name: 'Công ty NCC mới',
        isCustomer: false,
        isSupplier: true,
      });
      renderPage('Receipt');
      await pickPartner(supplier, 'NCC');
      expect(byId('cash-reason').value).toBe(R_THNCC);
      expect(screen.getByRole('status')).toHaveTextContent('Đã đổi lý do cho hợp với NCC001 (nhà cung cấp).');

      // F4 with a partner picked: the quick add picks the new one straight away, without clearing the first.
      fireEvent.keyDown(byId('cash-partner'), { key: 'F4' });
      const quickAdd = await screen.findByRole('dialog');
      const name = within(quickAdd).getByLabelText('Tên *');
      await waitFor(() => expect(name).toHaveFocus());
      fireEvent.change(name, { target: { value: 'Công ty NCC mới' } });
      fireEvent.keyDown(name, { key: 'Enter', ctrlKey: true });

      await waitFor(() => expect(createSupplierMock).toHaveBeenCalledTimes(1));
      await waitFor(() => expect(byId('cash-partner').value).toBe('NCC0099'));
      expect(byId('cash-reason').value).toBe(R_THNCC);
      expect(screen.queryByRole('status')).toBeNull();
      expect(createMock).not.toHaveBeenCalled();
    });

    it('4. the partner of a saved voucher (roles unknown) gets no red line', () => {
      routeParams.id = V1;
      voucherState.data = voucher();
      renderPage('Receipt');

      fireEvent.change(byId('cash-reason'), { target: { value: R_THNCC } });

      expect(byId('cash-partner').value).toBe('KH01');
      expect(screen.queryByText(/không phải/)).toBeNull();
    });

    it('4. a saved voucher keeps a reason no longer offered when the picked partner fits it', async () => {
      routeParams.id = V1;
      voucherState.data = voucher({
        reasonId: R_OLD,
        reasonCode: 'TCU',
        reasonName: 'Thu tiền cũ',
        reasonDebtEffect: 'None',
        partnerId: undefined,
        partnerCode: undefined,
        partnerName: undefined,
        allocations: [],
      });
      renderPage('Receipt');
      expect(byId('cash-reason').value).toBe(R_OLD);

      await pickCustomer();

      expect(byId('cash-reason').value).toBe(R_OLD);
      expect(screen.queryByText(/Đã đổi lý do/)).toBeNull();
    });

    it('5. Enter walks the header into the last grid row and appends a row after the last cell', async () => {
      renderPage('Receipt');
      await addLineWithButton(2);
      fireEvent.change(line(2), { target: { value: 'Dòng hai' } });

      byId('cash-voucher-at').focus();
      for (let i = 0; i < 9; i++) enterOn(active());
      expect(byId('cash-voucher-note')).toHaveFocus();

      enterOn(active());
      const last = line(2);
      await waitFor(() => expect(last).toHaveFocus());
      expect([last.selectionStart, last.selectionEnd]).toEqual([0, 'Dòng hai'.length]);

      enterOn(active());
      expect(screen.getByRole('textbox', { name: 'Số tiền dòng 2' })).toHaveFocus();
      enterOn(active());
      expect(screen.getByRole('textbox', { name: 'Ghi chú dòng 2' })).toHaveFocus();
      enterOn(active());
      await waitFor(() => expect(line(3)).toHaveFocus());
      expect(line(3).value).toBe('Thu tiền khách hàng');
      await settle();
      expect(createMock).not.toHaveBeenCalled();
    });

    it('5. Enter in Ghi chú adds a row first when the grid is empty', async () => {
      renderPage('Receipt');
      fireEvent.click(screen.getByRole('button', { name: 'Xóa dòng 1' }));
      expect(screen.queryByRole('textbox', { name: 'Diễn giải dòng 1' })).toBeNull();

      byId('cash-voucher-note').focus();
      enterOn(active());

      await waitFor(() => expect(line(1)).toHaveFocus());
      expect(line(1).value).toBe('Thu tiền khách hàng');
    });

    it('6. text typed in the partner code but not picked blocks saving until it is cleared', async () => {
      createMock.mockResolvedValue(voucher());
      partnerResults.data = [];
      renderPage('Receipt');
      fillLine('1000');
      fireEvent.change(byId('cash-partner'), { target: { value: 'zzz' } });

      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
      await settle();

      expect(createMock).not.toHaveBeenCalled();
      const message = screen.getByText(PICK_MESSAGE);
      const row = byId('cash-partner').closest('.partner-code-name') as HTMLElement;
      expect(message.parentElement).toBe(row.parentElement);
      expect(byId('cash-partner')).toHaveFocus();

      fireEvent.change(byId('cash-partner'), { target: { value: '' } });
      await waitFor(() => expect(screen.queryByText(PICK_MESSAGE)).toBeNull());
      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    });

    it('6. a locked partner field never blocks saving', async () => {
      createMock.mockResolvedValue(voucher());
      partnerResults.data = [];
      renderPage('Receipt');
      fireEvent.change(byId('cash-partner'), { target: { value: 'zzz' } });
      fireEvent.change(byId('cash-reason'), { target: { value: R_TK } });
      fillLine('1000');

      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
      expect(screen.queryByText(PICK_MESSAGE)).toBeNull();
    });

    it('7. the payload has no tax code and drops trailing untouched rows', async () => {
      createMock.mockResolvedValue(voucher());
      renderPage('Receipt');
      await pickCustomer();
      fillLine('2500', 'Thu tiền hàng');
      await addLineWithButton(2);
      expect(line(2).value).toBe(AUTO_KH01);
      await addLineWithButton(3);
      fireEvent.change(line(3), { target: { value: '' } });

      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

      await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
      const payload = createMock.mock.calls[0][0];
      expect(payload).not.toHaveProperty('partnerTaxCode');
      expect(payload).toMatchObject({ partnerName: customer.name, partnerAddress: customer.companyAddress });
      expect(payload.lines).toEqual([expect.objectContaining({ description: 'Thu tiền hàng', amount: 2_500 })]);
    });

    it('7. when every row is untouched nothing is dropped and the rows ask for an amount', async () => {
      renderPage('Receipt');
      await addLineWithButton(2);

      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

      await waitFor(() => expect(screen.getAllByText('Số tiền phải lớn hơn 0')).toHaveLength(2));
      expect([line(1).value, line(2).value]).toEqual(['Thu tiền khách hàng', 'Thu tiền khách hàng']);
      expect(createMock).not.toHaveBeenCalled();
    });

    it('8. quick add with F4 picks the new partner and never saves the voucher', async () => {
      withoutDefaultReason();
      createCustomerMock.mockResolvedValue(createdCustomer);
      renderPage('Receipt');
      // The quick add walks its fields by these global ids: the page must not use them.
      for (const id of [
        'code', 'name', 'taxCode', 'phoneNumber', 'email', 'group', 'creditDays', 'contactPerson',
        'companyAddress', 'defaultShippingAddress', 'note', 'status', 'role-customer', 'role-supplier',
      ]) {
        expect(document.getElementById(id)).toBeNull();
      }
      expect(screen.getByRole('button', { name: /Thêm nhanh/ })).toBeInTheDocument();

      fireEvent.change(byId('cash-partner'), { target: { value: 'Công ty mới' } });
      fireEvent.keyDown(byId('cash-partner'), { key: 'F4' });
      const quickAdd = await screen.findByRole('dialog');
      const name = within(quickAdd).getByLabelText('Tên *');
      await waitFor(() => expect(name).toHaveFocus());

      enterOn(active());
      expect(quickAdd).toContainElement(active());
      expect(active()).not.toBe(name);
      enterOn(active(), { shiftKey: true });
      expect(name).toHaveFocus();
      fireEvent.keyDown(active(), { key: 's', ctrlKey: true });
      await settle();
      expect(createMock).not.toHaveBeenCalled();
      expect(createCustomerMock).not.toHaveBeenCalled();
      expect(quickAdd).toContainElement(active());

      fireEvent.keyDown(active(), { key: 'Enter', ctrlKey: true });

      await waitFor(() => expect(createCustomerMock).toHaveBeenCalledTimes(1));
      await waitFor(() => expect(screen.queryByRole('dialog')).toBeNull());
      await waitFor(() => expect(byId('cash-partner-name')).toHaveFocus());
      expect(byId('cash-partner').value).toBe('KH0099');
      expect(byId('cash-partner-name').value).toBe('Công ty mới');
      expect(byId('cash-reason').value).toBe(R_TKH);
      expect(screen.getByText('Đã đổi lý do cho hợp với KH0099 (khách hàng).')).toBeInTheDocument();
      expect(byId('cash-description').value).toBe('Thu tiền khách hàng - Công ty mới');
      await settle();
      expect(createMock).not.toHaveBeenCalled();
    });

    it('9. a read-only voucher has no quick add', async () => {
      routeParams.id = V1;
      for (const data of readOnlyVouchers()) {
        voucherState.data = data;
        const view = renderPage('Receipt');
        expect(screen.queryByRole('button', { name: /Thêm nhanh/ })).toBeNull();
        fireEvent.keyDown(byId('cash-partner'), { key: 'F4' });
        await settle();
        expect(screen.queryByRole('dialog')).toBeNull();
        view.unmount();
      }
    });

    it('10. a server partnerId error shows under the partner row', async () => {
      createMock.mockRejectedValueOnce(apiError(400, 'VALIDATION', { partnerId: ['Đối tượng không hợp với lý do'] }));
      renderPage('Receipt');
      await pickCustomer();
      fillLine('2500');

      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

      const message = await screen.findByText('Đối tượng không hợp với lý do');
      const row = byId('cash-partner').closest('.partner-code-name') as HTMLElement;
      expect(message.parentElement).toBe(row.parentElement);
      expect(byId('cash-reason').parentElement).not.toContainElement(message);
    });

    it('10. picking another partner removes the server partnerId error', async () => {
      const MESSAGE = 'Đối tượng không hợp với lý do';
      createMock.mockRejectedValueOnce(apiError(400, 'VALIDATION', { partnerId: [MESSAGE] }));
      renderPage('Receipt');
      await pickCustomer();
      fillLine('2500');
      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
      expect(await screen.findByText(MESSAGE)).toBeInTheDocument();

      await pickPartner(supplier, 'NCC');

      expect(screen.queryByText(MESSAGE)).toBeNull();
    });

    it('10. a reason change removes the server partnerId error', async () => {
      const MESSAGE = 'Đối tượng không hợp với lý do';
      createMock.mockRejectedValueOnce(apiError(400, 'VALIDATION', { partnerId: [MESSAGE] }));
      renderPage('Receipt');
      await pickCustomer();
      fillLine('2500');
      fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));
      expect(await screen.findByText(MESSAGE)).toBeInTheDocument();

      fireEvent.change(byId('cash-reason'), { target: { value: R_THNCC } });

      expect(screen.queryByText(MESSAGE)).toBeNull();
    });

    it('11. a read-only voucher is not auto filled', () => {
      routeParams.id = V1;
      for (const data of readOnlyVouchers()) {
        voucherState.data = data;
        const view = renderPage('Receipt');
        expect(byId('cash-description').value).toBe('');
        expect(line(1).value).toBe('Thu tiền hàng');
        expect(printProps.disabled).toBeFalsy();
        view.unmount();
      }
    });

    it('12. a new voucher without a default reason fills in once a partner is picked', async () => {
      withoutDefaultReason();
      renderPage('Receipt');
      expect(byId('cash-reason').value).toBe('');
      expect(byId('cash-description').value).toBe('');
      expect(line(1).value).toBe('');
      expect(byId('cash-partner')).toBeEnabled();

      await pickCustomer();

      expect(byId('cash-reason').value).toBe(R_TKH);
      expect(screen.getByText('Đã đổi lý do cho hợp với KH01 (khách hàng).')).toHaveAttribute('role', 'status');
      expect(byId('cash-description').value).toBe(AUTO_KH01);
      expect(line(1).value).toBe(AUTO_KH01);
    });

    it('12. an empty reason and a picked partner that no reason fits get a red line', async () => {
      withoutDefaultReason();
      renderPage('Receipt');

      await pickPartner({ ...customer, isCustomer: false, isSupplier: false }, 'KH');

      expect(byId('cash-reason').value).toBe('');
      expect(screen.getByText('Không có lý do nào hợp với KH01 (đối tượng)')).toHaveClass('text-destructive');
      expect(screen.queryByText(/Đã đổi lý do/)).toBeNull();
    });

    it('13. added and deleted rows keep following the header; a hand-typed row does not', async () => {
      renderPage('Receipt');
      await pickCustomer();
      fireEvent.change(line(1), { target: { value: 'Dòng tay' } });
      await addLineWithButton(2);
      expect(line(2).value).toBe(AUTO_KH01);

      fireEvent.click(screen.getByRole('button', { name: 'Xóa dòng 1' }));
      expect(screen.queryByRole('textbox', { name: 'Diễn giải dòng 2' })).toBeNull();
      expect(line(1).value).toBe(AUTO_KH01);

      fireEvent.change(byId('cash-partner-name'), { target: { value: 'Công ty ABC Hà Nội' } });
      expect(line(1).value).toBe('Thu tiền khách hàng - Công ty ABC Hà Nội');

      await addLineWithButton(2);
      fireEvent.change(line(2), { target: { value: 'Tự ghi' } });
      fireEvent.change(byId('cash-partner-name'), { target: { value: 'Công ty ABC Hải Phòng' } });
      expect(line(1).value).toBe('Thu tiền khách hàng - Công ty ABC Hải Phòng');
      expect(line(2).value).toBe('Tự ghi');
    });
  });
});
