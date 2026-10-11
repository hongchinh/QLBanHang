import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type * as ReactRouterDom from 'react-router-dom';
import { DebtOffsetFormPage } from './debt-offset-form-page';
import { toDateTimeLocalValue } from '@/lib/vn-datetime';
import { useAuthStore } from '@/stores/auth-store';
import type { DebtPartnerSearchItem, OpenItem, OpenItemsParams } from '@/features/debt/types';
import type { DebtOffset, DebtOffsetDefaults } from '@/features/debt-offsets/types';

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
  };
});

const mocks = vi.hoisted(() => ({
  toast: vi.fn(),
  partnerSearch: vi.fn(),
  openItems: vi.fn(),
  create: vi.fn(),
  cancel: vi.fn(),
  remove: vi.fn(),
  refetch: vi.fn(),
}));

vi.mock('@/lib/use-toast', () => ({ toast: (...args: unknown[]) => mocks.toast(...args) }));

vi.mock('@/features/debt/hooks', () => ({
  useDebtPartnerSearch: (...args: unknown[]) => mocks.partnerSearch(...args),
  useOpenItems: (params: OpenItemsParams, opts?: unknown) => mocks.openItems(params, opts),
}));

const DEFAULT_AT = '2026-10-07T01:30:00.000Z';
const defaults: DebtOffsetDefaults = { nextCode: 'BT00001', voucherAt: DEFAULT_AT };
const offsetState: { data: DebtOffset | undefined } = { data: undefined };
const mutation = (fn: ReturnType<typeof vi.fn>) => ({ mutateAsync: fn, isPending: false });

vi.mock('@/features/debt-offsets/hooks', () => ({
  useDebtOffset: (id: string | undefined) => ({
    data: id ? offsetState.data : undefined,
    isLoading: false,
    refetch: mocks.refetch,
  }),
  useDebtOffsetDefaults: () => ({ data: defaults, isFetchedAfterMount: true }),
  useDebtOffsetActivities: () => ({
    data: [{ id: 'a1', action: 'Created', actorName: 'Kế toán A', occurredAt: DEFAULT_AT, description: 'Tạo chứng từ' }],
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  }),
  useCreateDebtOffset: () => mutation(mocks.create),
  useCancelDebtOffset: () => mutation(mocks.cancel),
  useDeleteDebtOffset: () => mutation(mocks.remove),
}));

const P1 = '11111111-1111-4111-8111-111111111111';
const O1 = '33333333-3333-4333-8333-333333333333';
const R1 = 'a1a1a1a1-0000-4000-8000-000000000001';
const R2 = 'a2a2a2a2-0000-4000-8000-000000000002';
const Q1 = 'b1b1b1b1-0000-4000-8000-000000000001';

const partner: DebtPartnerSearchItem = {
  id: P1,
  code: 'DT01',
  name: 'Công ty Hai Chiều',
  status: 'Active',
  isCustomer: true,
  isSupplier: true,
};

function item(overrides: Partial<OpenItem>): OpenItem {
  return {
    entryId: R1,
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

// Receivables: PX00002 (10-03) is newer than PX00001 (10-01). Payables: one stock-in of 1,200,000.
const receivables: OpenItem[] = [
  item({ entryId: R2, docCode: 'PX00002', docDate: '2026-10-03', amount: 800_000, open: 800_000 }),
  item({ entryId: R1, docCode: 'PX00001', docDate: '2026-10-01', amount: 1_000_000, open: 1_000_000 }),
];
const payables: OpenItem[] = [
  item({ entryId: Q1, sourceType: 'StockIn', docCode: 'PN00001', docDate: '2026-10-02', amount: 1_200_000, open: 1_200_000 }),
];

function offset(overrides: Partial<DebtOffset> = {}): DebtOffset {
  return {
    id: O1,
    code: 'BT00007',
    voucherAt: DEFAULT_AT,
    partnerId: P1,
    partnerCode: 'DT01',
    partnerName: 'Công ty Hai Chiều',
    // Larger than the items: a document it offset was later cancelled (the server amount is shown as is).
    amount: 2_000_000,
    note: 'Bù trừ tháng 10',
    status: 'Active',
    cancelledAt: null,
    ownerUserId: 'u1',
    ownerName: 'Kế toán A',
    version: 7,
    canCancel: true,
    canDelete: true,
    items: [
      { side: 'Receivable', entryId: R1, sourceType: 'StockOut', sourceId: 's1', docCode: 'PX00001', docDate: '2026-10-01', amount: 1_500_000 },
      { side: 'Payable', entryId: Q1, sourceType: 'StockIn', sourceId: 's2', docCode: 'PN00001', docDate: '2026-10-02', amount: 1_500_000 },
    ],
    ...overrides,
  };
}

function apiError(status: number, code: string, details?: Record<string, string[]>) {
  return {
    isAxiosError: true,
    message: code,
    response: { status, data: { success: false, error: { code, message: `Thông báo ${code}`, details } } },
  };
}

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    accessToken: 't',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'u1', username: 'kt', email: 'k@example.com', fullName: 'Kế toán A', roles: [], permissions },
  });
}

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

function enter(input: HTMLElement, text: string) {
  fireEvent.focus(input);
  fireEvent.change(input, { target: { value: text } });
  fireEvent.blur(input);
}

const receivableGrid = () => screen.getByRole('region', { name: 'Chứng từ phải thu' });
const payableGrid = () => screen.getByRole('region', { name: 'Chứng từ phải trả' });

async function pickPartner() {
  const user = userEvent.setup();
  await user.click(screen.getByRole('combobox', { name: 'Đối tượng' }));
  await user.click(await screen.findByRole('option', { name: /DT01/ }));
}

describe('DebtOffsetFormPage', () => {
  beforeEach(() => {
    routeParams.id = 'new';
    offsetState.data = undefined;
    navigateMock.mockReset();
    for (const m of Object.values(mocks)) m.mockReset();
    mocks.partnerSearch.mockReturnValue({ data: [partner], isLoading: false, isError: false });
    mocks.openItems.mockImplementation((params: OpenItemsParams) => ({
      data: params.side === 'Receivable' ? receivables : payables,
      isLoading: false,
      isError: false,
    }));
    setPermissions(['debt_offset.view', 'debt_offset.create', 'debt_offset.cancel', 'debt_offset.delete']);
  });

  it('requires equal sides', async () => {
    render(<DebtOffsetFormPage />);

    expect(screen.getByRole('heading', { name: 'Thêm phiếu bù trừ công nợ' })).toBeInTheDocument();
    expect(screen.getByText('Số phiếu dự kiến: BT00001')).toBeInTheDocument();
    expect(byId('debt-offset-at').value).toBe(toDateTimeLocalValue(DEFAULT_AT));
    expect(screen.getByRole('button', { name: 'Lưu' })).toBeDisabled();
    expect(screen.queryByRole('region', { name: 'Chứng từ phải thu' })).not.toBeInTheDocument();

    await pickPartner();

    // Partners that are both customer and supplier; open Increase items of each side.
    expect(mocks.partnerSearch).toHaveBeenLastCalledWith(undefined, expect.any(String), {
      bothRoles: true,
      includeInactive: true,
    });
    expect(mocks.openItems).toHaveBeenCalledWith({ partnerId: P1, side: 'Receivable', direction: 'Increase' }, undefined);
    expect(mocks.openItems).toHaveBeenCalledWith({ partnerId: P1, side: 'Payable', direction: 'Increase' }, undefined);
    expect(within(receivableGrid()).getByText('PX00001')).toBeInTheDocument();
    expect(within(payableGrid()).getByText('PN00001')).toBeInTheDocument();

    enter(within(receivableGrid()).getByRole('textbox', { name: 'Đối trừ PX00001' }), '1000000');
    enter(within(payableGrid()).getByRole('textbox', { name: 'Đối trừ PN00001' }), '900000');

    expect(screen.getByTestId('sum-receivable')).toHaveTextContent('1.000.000');
    expect(screen.getByTestId('sum-payable')).toHaveTextContent('900.000');
    expect(screen.getByText('Hai bên chưa bằng nhau')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lưu' })).toBeDisabled();

    enter(within(payableGrid()).getByRole('textbox', { name: 'Đối trừ PN00001' }), '1000000');

    expect(screen.queryByText('Hai bên chưa bằng nhau')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lưu' })).toBeEnabled();

    // An amount above the open amount blocks the save even when the sums match.
    enter(within(receivableGrid()).getByRole('textbox', { name: 'Đối trừ PX00001' }), '1200000');
    enter(within(payableGrid()).getByRole('textbox', { name: 'Đối trừ PN00001' }), '1200000');
    expect(screen.getByRole('button', { name: 'Lưu' })).toBeDisabled();
    expect(mocks.create).not.toHaveBeenCalled();
  });

  it('blocks the save while a row is negative', async () => {
    render(<DebtOffsetFormPage />);
    await pickPartner();

    // Shown sums 100,000 = 100,000, but only the positive rows would be sent (200,000 against 100,000).
    enter(within(receivableGrid()).getByRole('textbox', { name: 'Đối trừ PX00001' }), '200000');
    const negative = within(receivableGrid()).getByRole('textbox', { name: 'Đối trừ PX00002' });
    enter(negative, '-100000');
    enter(within(payableGrid()).getByRole('textbox', { name: 'Đối trừ PN00001' }), '100000');

    expect(negative).toHaveAttribute('aria-invalid', 'true');
    expect(within(receivableGrid()).getByText('Số tiền không được âm')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Lưu' })).toBeDisabled();
    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));
    expect(mocks.create).not.toHaveBeenCalled();

    // The negative row fixed: the sides match again and can be saved.
    enter(negative, '0');
    enter(within(payableGrid()).getByRole('textbox', { name: 'Đối trừ PN00001' }), '200000');
    expect(screen.getByTestId('sum-receivable')).toHaveTextContent('200.000');
    expect(screen.getByTestId('sum-payable')).toHaveTextContent('200.000');
    expect(screen.getByRole('button', { name: 'Lưu' })).toBeEnabled();
  });

  it('creates an offset', async () => {
    mocks.create.mockResolvedValue(offset({ id: O1 }));
    render(<DebtOffsetFormPage />);
    await pickPartner();
    fireEvent.change(byId('debt-offset-note'), { target: { value: 'Bù trừ tháng 10' } });

    // Both sides up to min(1,800,000 ; 1,200,000), oldest first.
    fireEvent.click(screen.getByRole('button', { name: 'Gợi ý' }));

    expect(within(receivableGrid()).getByRole('textbox', { name: 'Đối trừ PX00001' })).toHaveValue('1.000.000');
    expect(within(receivableGrid()).getByRole('textbox', { name: 'Đối trừ PX00002' })).toHaveValue('200.000');
    expect(within(payableGrid()).getByRole('textbox', { name: 'Đối trừ PN00001' })).toHaveValue('1.200.000');
    expect(screen.getByTestId('sum-receivable')).toHaveTextContent('1.200.000');
    expect(screen.getByTestId('sum-payable')).toHaveTextContent('1.200.000');

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(mocks.create).toHaveBeenCalledTimes(1));
    expect(mocks.create.mock.calls[0][0]).toEqual({
      voucherAt: DEFAULT_AT,
      partnerId: P1,
      note: 'Bù trừ tháng 10',
      receivables: [
        { entryId: R1, amount: 1_000_000 },
        { entryId: R2, amount: 200_000 },
      ],
      payables: [{ entryId: Q1, amount: 1_200_000 }],
    });
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(`/debt/offsets/${O1}`, { replace: true }));
    expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));
  });

  it('shows the server errors when the create is rejected', async () => {
    mocks.create.mockRejectedValue(
      apiError(400, 'VALIDATION', { 'payables[0].amount': ['Số tiền bù trừ vượt số còn nợ của chứng từ.'] }),
    );
    render(<DebtOffsetFormPage />);
    await pickPartner();
    fireEvent.click(screen.getByRole('button', { name: 'Gợi ý' }));

    fireEvent.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(mocks.toast).toHaveBeenCalledWith(
        expect.objectContaining({
          variant: 'destructive',
          description: expect.stringContaining('Số tiền bù trừ vượt số còn nợ của chứng từ.'),
        }),
      ),
    );
    expect(navigateMock).not.toHaveBeenCalled();
  });

  it('view mode cancels with version', async () => {
    routeParams.id = O1;
    offsetState.data = offset();
    mocks.cancel
      .mockRejectedValueOnce(apiError(422, 'DEBT_SETTLEMENTS_WILL_BE_REMOVED', { PT00009: ['Đối trừ 300.000'] }))
      .mockResolvedValueOnce(offset({ status: 'Cancelled', canCancel: false, items: [] }));
    render(<DebtOffsetFormPage />);

    expect(screen.getByRole('heading', { name: 'Phiếu bù trừ công nợ BT00007' })).toBeInTheDocument();
    // The server amount, not Σ items (1,500,000).
    expect(screen.getByTestId('debt-offset-amount')).toHaveTextContent('2.000.000');
    expect(screen.getByText('DT01 - Công ty Hai Chiều')).toBeInTheDocument();
    const items = screen.getByRole('table', { name: 'Chứng từ bù trừ' });
    expect(within(items).getByText('PX00001')).toBeInTheDocument();
    expect(within(items).getByText('PN00001')).toBeInTheDocument();
    expect(screen.getByText('Tạo chứng từ', { selector: 'p' })).toBeInTheDocument();
    // No edit and no restore (P23); the view loads no open items.
    expect(screen.queryByRole('button', { name: /Lưu|Cập nhật|Khôi phục/ })).not.toBeInTheDocument();
    expect(screen.queryByRole('textbox', { name: /Đối trừ/ })).not.toBeInTheDocument();
    expect(mocks.openItems).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Hủy phiếu' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Hủy phiếu' }));

    await waitFor(() =>
      expect(mocks.cancel).toHaveBeenCalledWith({ id: O1, body: { version: 7, confirmRemoveSettlements: false } }),
    );

    // Another document reused an offset entry: confirm, then resend with the flag.
    const dialog = within(await screen.findByRole('dialog'));
    expect(dialog.getByText('Bỏ đối trừ chứng từ')).toBeInTheDocument();
    expect(dialog.getByText('Đối trừ 300.000')).toBeInTheDocument();
    fireEvent.click(dialog.getByRole('button', { name: 'Vẫn hủy phiếu' }));

    await waitFor(() => expect(mocks.cancel).toHaveBeenCalledTimes(2));
    expect(mocks.cancel).toHaveBeenLastCalledWith({ id: O1, body: { version: 7, confirmRemoveSettlements: true } });
    await waitFor(() =>
      expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success', title: 'Đã hủy phiếu' })),
    );
  });

  it('view mode deletes with version and shows a period lock', async () => {
    routeParams.id = O1;
    offsetState.data = offset({ status: 'Cancelled', canCancel: false, items: [] });
    mocks.remove
      .mockRejectedValueOnce(apiError(400, 'PERIOD_LOCKED'))
      .mockResolvedValueOnce(undefined);
    render(<DebtOffsetFormPage />);

    expect(screen.getByText('Đã hủy')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hủy phiếu' })).not.toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Xóa' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xóa' }));

    await waitFor(() =>
      expect(mocks.toast).toHaveBeenCalledWith(
        expect.objectContaining({ variant: 'destructive', description: expect.stringContaining('Thông báo PERIOD_LOCKED') }),
      ),
    );
    expect(mocks.remove).toHaveBeenCalledWith({ id: O1, body: { version: 7, confirmRemoveSettlements: false } });
    expect(navigateMock).not.toHaveBeenCalled();

    fireEvent.click(screen.getByRole('button', { name: 'Xóa' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xóa' }));

    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith('/debt/offsets'));
  });

  it('without create permission a new offset cannot be saved', () => {
    setPermissions(['debt_offset.view']);
    render(<DebtOffsetFormPage />);

    expect(screen.getByText('Bạn không có quyền tạo phiếu bù trừ công nợ.')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Lưu' })).not.toBeInTheDocument();
  });
});
