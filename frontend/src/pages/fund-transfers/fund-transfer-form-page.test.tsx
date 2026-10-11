import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import type * as ReactRouterDom from 'react-router-dom';
import { FundTransferFormPage } from './fund-transfer-form-page';
import { toDateTimeLocalValue } from '@/lib/vn-datetime';
import { useAuthStore } from '@/stores/auth-store';
import type { Fund } from '@/features/funds/types';
import type { FundTransfer, FundTransferDefaults } from '@/features/fund-transfers/types';

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

const toastMock = vi.fn();
vi.mock('@/lib/use-toast', () => ({ toast: (...args: unknown[]) => toastMock(...args) }));

const createMock = vi.fn();
const updateMock = vi.fn();
const cancelMock = vi.fn();
const restoreMock = vi.fn();
const deleteMock = vi.fn();
const refetchMock = vi.fn();
const transferState: { data: FundTransfer | undefined } = { data: undefined };

const DEFAULT_AT = '2026-10-07T01:30:00.000Z';
const defaults: FundTransferDefaults = { nextCode: 'CQ00001', voucherAt: DEFAULT_AT };

const mutation = (fn: ReturnType<typeof vi.fn>) => ({ mutateAsync: fn, isPending: false });

vi.mock('@/features/fund-transfers/hooks', () => ({
  useFundTransfer: (id: string | undefined) => ({
    data: id ? transferState.data : undefined,
    isLoading: false,
    refetch: refetchMock,
  }),
  useFundTransferDefaults: () => ({ data: defaults, isFetchedAfterMount: true }),
  useFundTransferActivities: () => ({
    data: [{ id: 'a1', action: 'Created', actorName: 'Kế toán A', occurredAt: DEFAULT_AT, description: 'Tạo phiếu' }],
    isLoading: false,
    isError: false,
    error: null,
    refetch: vi.fn(),
  }),
  useCreateFundTransfer: () => mutation(createMock),
  useUpdateFundTransfer: () => mutation(updateMock),
  useCancelFundTransfer: () => mutation(cancelMock),
  useRestoreFundTransfer: () => mutation(restoreMock),
  useDeleteFundTransfer: () => mutation(deleteMock),
}));

const F_CASH = '11111111-1111-4111-8111-111111111111';
const F_BANK = '22222222-2222-4222-8222-222222222222';
const F_OLD = '22222222-2222-4222-8222-222222222223';
const T1 = '33333333-3333-4333-8333-333333333333';

const funds: Fund[] = [
  { id: F_CASH, branchId: 'b1', code: 'QTM', name: 'Quỹ tiền mặt', kind: 'Cash', isDefault: true, openingAmount: 0, isActive: true },
  { id: F_BANK, branchId: 'b1', code: 'VCB', name: 'Vietcombank', kind: 'Bank', isDefault: true, openingAmount: 0, isActive: true },
  { id: F_OLD, branchId: 'b1', code: 'ACB', name: 'ACB cũ', kind: 'Bank', isDefault: false, openingAmount: 0, isActive: false },
];

vi.mock('@/features/funds/hooks', () => ({ useFunds: () => ({ data: funds }) }));

function transfer(overrides: Partial<FundTransfer> = {}): FundTransfer {
  return {
    id: T1,
    code: 'CQ00007',
    voucherAt: DEFAULT_AT,
    branchId: 'b1',
    fromFundId: F_CASH,
    fromFundCode: 'QTM',
    fromFundName: 'Quỹ tiền mặt',
    fromFundKind: 'Cash',
    toFundId: F_BANK,
    toFundCode: 'VCB',
    toFundName: 'Vietcombank',
    toFundKind: 'Bank',
    amount: 2_000_000,
    description: 'Nộp tiền',
    note: null,
    status: 'Active',
    cancelledAt: null,
    ownerUserId: 'u1',
    ownerName: 'Kế toán A',
    version: 9,
    canEdit: true,
    canCancel: true,
    canDelete: true,
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

function byId(id: string): HTMLInputElement {
  const el = document.getElementById(id);
  if (!el) throw new Error(`#${id} not found`);
  return el as HTMLInputElement;
}

function setPermissions(permissions: string[]) {
  useAuthStore.setState({
    accessToken: 't',
    expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    user: { id: 'u1', username: 'kt', email: 'k@example.com', fullName: 'Kế toán A', roles: [], permissions },
  });
}

function fillNewTransfer() {
  fireEvent.change(byId('fund-transfer-from'), { target: { value: F_CASH } });
  fireEvent.change(byId('fund-transfer-to'), { target: { value: F_BANK } });
  fireEvent.change(byId('fund-transfer-amount'), { target: { value: '1.500.000' } });
  fireEvent.change(byId('fund-transfer-description'), { target: { value: 'Nộp tiền vào ngân hàng' } });
}

describe('FundTransferFormPage', () => {
  beforeEach(() => {
    routeParams.id = 'new';
    transferState.data = undefined;
    for (const m of [navigateMock, toastMock, createMock, updateMock, cancelMock, restoreMock, deleteMock, refetchMock]) {
      m.mockReset();
    }
    setPermissions([
      'fund_transfers.view',
      'fund_transfers.create',
      'fund_transfers.edit',
      'fund_transfers.cancel',
      'fund_transfers.delete',
    ]);
  });

  it('creates a transfer', async () => {
    createMock.mockResolvedValue(transfer({ id: T1 }));
    render(<FundTransferFormPage />);

    expect(screen.getByRole('heading', { name: 'Thêm phiếu chuyển quỹ' })).toBeInTheDocument();
    expect(screen.getByText('Số phiếu dự kiến: CQ00001')).toBeInTheDocument();
    expect(byId('fund-transfer-at').value).toBe(toDateTimeLocalValue(DEFAULT_AT));
    // Active funds only, labelled with their kind.
    const fromOptions = within(byId('fund-transfer-from')).getAllByRole('option').map((o) => o.textContent);
    expect(fromOptions).toEqual(['-- Chọn quỹ --', 'QTM — Quỹ tiền mặt (Tiền mặt)', 'VCB — Vietcombank (Ngân hàng)']);

    fillNewTransfer();
    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(1));
    expect(createMock.mock.calls[0][0]).toEqual({
      voucherAt: DEFAULT_AT,
      fromFundId: F_CASH,
      toFundId: F_BANK,
      amount: 1_500_000,
      description: 'Nộp tiền vào ngân hàng',
      note: undefined,
      version: undefined,
      acknowledgeNegativeFund: false,
    });
    await waitFor(() => expect(navigateMock).toHaveBeenCalledWith(`/fund-transfers/${T1}`, { replace: true }));
  });

  it('same funds show a field error and do not submit', async () => {
    render(<FundTransferFormPage />);
    fillNewTransfer();
    fireEvent.change(byId('fund-transfer-to'), { target: { value: F_CASH } });

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    expect(await screen.findByText('Quỹ đến phải khác quỹ đi')).toBeInTheDocument();
    expect(createMock).not.toHaveBeenCalled();
  });

  it('negative fund warning resends with acknowledgement', async () => {
    createMock
      .mockRejectedValueOnce(apiError(422, 'NEGATIVE_FUND_WARNING', { QTM: ['Âm 500.000 từ 07/10/2026'] }))
      .mockResolvedValueOnce(transfer());
    render(<FundTransferFormPage />);
    fillNewTransfer();

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    const dialog = within(await screen.findByRole('dialog'));
    expect(dialog.getByText('Quỹ bị âm')).toBeInTheDocument();
    expect(dialog.getByText('Âm 500.000 từ 07/10/2026')).toBeInTheDocument();
    fireEvent.click(dialog.getByRole('button', { name: 'Vẫn lưu' }));

    await waitFor(() => expect(createMock).toHaveBeenCalledTimes(2));
    expect(createMock.mock.calls[0][0].acknowledgeNegativeFund).toBe(false);
    expect(createMock.mock.calls[1][0]).toEqual({ ...createMock.mock.calls[0][0], acknowledgeNegativeFund: true });
  });

  it('blocked negative fund shows close only and does not resend', async () => {
    createMock.mockRejectedValueOnce(apiError(422, 'NEGATIVE_FUND_BLOCKED', { QTM: ['Âm 500.000'] }));
    render(<FundTransferFormPage />);
    fillNewTransfer();

    fireEvent.click(screen.getByRole('button', { name: 'Lưu tạm' }));

    const dialog = within(await screen.findByRole('dialog'));
    expect(dialog.queryByRole('button', { name: 'Vẫn lưu' })).not.toBeInTheDocument();
    fireEvent.click(dialog.getByRole('button', { name: 'Đóng' }));
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(createMock).toHaveBeenCalledTimes(1);
  });

  it('updates with the loaded version and reloads on concurrency', async () => {
    routeParams.id = T1;
    transferState.data = transfer();
    updateMock.mockRejectedValueOnce(apiError(409, 'CONCURRENCY'));
    refetchMock.mockResolvedValue({ status: 'success', data: transfer({ amount: 3_000_000, version: 10 }) });
    render(<FundTransferFormPage />);

    expect(screen.getByRole('heading', { name: 'Phiếu chuyển quỹ CQ00007' })).toBeInTheDocument();
    expect(byId('fund-transfer-amount').value).toBe('2.000.000');
    expect(screen.getByText('Tạo phiếu', { selector: 'p' })).toBeInTheDocument();
    fireEvent.change(byId('fund-transfer-amount'), { target: { value: '2.500.000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(1));
    expect(updateMock.mock.calls[0][0]).toMatchObject({ id: T1, data: { amount: 2_500_000, version: 9 } });
    await waitFor(() => expect(byId('fund-transfer-amount').value).toBe('3.000.000'));
    expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ variant: 'destructive' }));
  });

  it('cancelled transfer is read-only', async () => {
    routeParams.id = T1;
    transferState.data = transfer({ status: 'Cancelled', canEdit: false, toFundId: F_OLD, toFundCode: 'ACB' });
    restoreMock.mockResolvedValue(transfer());
    render(<FundTransferFormPage />);

    expect(screen.getByText('Đã hủy')).toBeInTheDocument();
    expect(byId('fund-transfer-amount')).toBeDisabled();
    expect(byId('fund-transfer-from')).toBeDisabled();
    // The inactive fund of the stored transfer stays visible.
    expect(byId('fund-transfer-to').value).toBe(F_OLD);
    expect(screen.queryByRole('button', { name: 'Cập nhật' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Hủy phiếu' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Xóa' })).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: 'Khôi phục' }));
    fireEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Khôi phục' }));

    await waitFor(() =>
      expect(restoreMock).toHaveBeenCalledWith({ id: T1, body: { version: 9, acknowledgeNegativeFund: false } }),
    );
  });

  it('without create permission a new form cannot save', () => {
    setPermissions(['fund_transfers.view']);
    render(<FundTransferFormPage />);

    expect(screen.queryByRole('button', { name: 'Lưu tạm' })).not.toBeInTheDocument();
    expect(byId('fund-transfer-amount')).toBeDisabled();
  });
});
