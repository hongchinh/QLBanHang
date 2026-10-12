import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes } from 'react-router-dom';
import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { OpeningDebtPage } from './opening-debt-page';
import type { DebtSide } from '@/features/cash-common/types';
import type { OpeningDebt, OpeningDebtListParams, OpeningDebtListResult } from '@/features/opening-debts/types';
import { toast } from '@/lib/use-toast';

const ROWS: OpeningDebt[] = [
  {
    id: 'o1',
    partnerId: '11111111-1111-4111-8111-111111111111',
    partnerCode: 'KH001',
    partnerName: 'Công ty An Phát',
    side: 'Receivable',
    kind: 'Debt',
    docNo: 'HD001',
    docDate: '2025-12-15',
    dueDate: '2026-01-15',
    amount: 5000000,
    settled: 2000000,
    ownerUserId: '99999999-9999-4999-8999-999999999999',
    ownerName: 'Nguyễn Văn A',
    note: 'Nợ cũ',
    version: 11,
  },
  {
    id: 'o2',
    partnerId: '22222222-2222-4222-8222-222222222222',
    partnerCode: 'NCC01',
    partnerName: 'Nhà cung cấp Bình Minh',
    side: 'Payable',
    kind: 'Prepayment',
    docNo: 'UT01',
    docDate: '2025-12-20',
    dueDate: null,
    amount: 1000000,
    settled: 0,
    ownerUserId: null,
    ownerName: null,
    note: null,
    version: 12,
  },
];

const PARTNERS = [
  {
    id: '33333333-3333-4333-8333-333333333333',
    code: 'KH003',
    name: 'Cửa hàng Cát Tường',
    status: 'Active' as const,
    isCustomer: true,
    isSupplier: false,
  },
  {
    id: '44444444-4444-4444-8444-444444444444',
    code: 'NCC04',
    name: 'NCC Đông Á',
    status: 'Active' as const,
    isCustomer: false,
    isSupplier: true,
  },
];

const OWNERS = [
  { id: '99999999-9999-4999-8999-999999999999', fullName: 'Nguyễn Văn A' },
  { id: '88888888-8888-4888-8888-888888888888', fullName: 'Trần Thị B' },
];

const GONE_OWNER_ID = '77777777-7777-4777-8777-777777777777';
function goneOwnerRow(ownerName: string | null): OpeningDebt {
  return { ...ROWS[1], id: 'o3', docNo: 'HD777', kind: 'Debt', ownerUserId: GONE_OWNER_ID, ownerName, version: 13 };
}

let extraRows: OpeningDebt[] = [];
let cachedRows: OpeningDebt[] | null = null;
let openingDate: string | null = '2026-01-01';
const listParams = vi.fn<(params: OpeningDebtListParams) => void>();
const createMock = vi.fn();
const updateMock = vi.fn();
const deleteMock = vi.fn();

function listResult(): OpeningDebtListResult {
  return {
    // Stable reference per render: a fresh array on every call would loop useReactTable.
    items: extraRows.length === 0 ? ROWS : (cachedRows ??= [...ROWS, ...extraRows]),
    page: 1,
    pageSize: 20,
    totalItems: ROWS.length + extraRows.length,
    totalPages: 1,
    hasNextPage: false,
    hasPreviousPage: false,
    openingDate,
  };
}

// The OPENING_DATE_NOT_SET handler stays real.
vi.mock('@/features/opening-debts/hooks', async (importOriginal) => ({
  ...(await importOriginal<Record<string, unknown>>()),
  useOpeningDebts: (params: OpeningDebtListParams) => {
    listParams(params);
    return { data: listResult(), isLoading: false, isError: false, error: null };
  },
  useOpeningDebtOwners: () => ({ data: OWNERS, isLoading: false, isError: false }),
  useCreateOpeningDebt: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateOpeningDebt: () => ({ mutateAsync: updateMock, isPending: false }),
  useDeleteOpeningDebt: () => ({ mutateAsync: deleteMock, isPending: false }),
  useOpeningDebtPartnerSearch: (keyword: string) => ({
    data: keyword ? PARTNERS : [],
    isFetching: false,
    isLoading: false,
    isError: false,
  }),
}));

// The form picker searches the partners of the chosen side.
const sideSearches = vi.fn<(side: DebtSide) => void>();
vi.mock('@/features/debt/hooks', () => ({
  useDebtPartnerSearch: (side: DebtSide, keyword: string) => {
    sideSearches(side);
    const data = keyword
      ? PARTNERS.filter((p) => (side === 'Receivable' ? p.isCustomer : p.isSupplier))
      : [];
    return { data, isFetching: false, isLoading: false, isError: false };
  },
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

function apiError(status: number, code: string, message: string, details?: Record<string, string[]>, token?: string) {
  const config = { headers: new AxiosHeaders(), url: '/opening-debts/o1' } as InternalAxiosRequestConfig;
  return new AxiosError('Request failed', 'ERR_BAD_RESPONSE', config, null, {
    status,
    statusText: '',
    headers: {},
    config,
    data: { success: false, error: { code, message, details, token }, timestamp: '' },
  });
}

function renderPage() {
  return render(
    <MemoryRouter initialEntries={['/debt/opening']}>
      <Routes>
        <Route path="/debt/opening" element={<OpeningDebtPage />} />
        <Route path="/settings/branches" element={<div>Trang chi nhánh</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe('OpeningDebtPage', () => {
  beforeEach(() => {
    openingDate = '2026-01-01';
    extraRows = [];
    cachedRows = null;
    listParams.mockReset();
    sideSearches.mockReset();
    createMock.mockReset().mockResolvedValue({});
    updateMock.mockReset().mockResolvedValue({});
    deleteMock.mockReset().mockResolvedValue(undefined);
    vi.mocked(toast).mockReset();
  });

  it('lists opening debts', async () => {
    const user = userEvent.setup();
    renderPage();

    expect(screen.getByText('Ngày đầu kỳ: 01/01/2026')).toBeInTheDocument();
    for (const header of ['Đối tượng', 'Phía', 'Loại', 'Số CT', 'Ngày CT', 'Hạn TT', 'Số tiền', 'Đã đối trừ', 'Nhân viên', 'Ghi chú']) {
      expect(screen.getByRole('columnheader', { name: header })).toBeInTheDocument();
    }

    const debtRow = screen.getByText('HD001').closest('tr')!;
    expect(within(debtRow).getByText('KH001 - Công ty An Phát')).toBeInTheDocument();
    expect(within(debtRow).getByText('Phải thu')).toBeInTheDocument();
    expect(within(debtRow).getByText('Nợ')).toBeInTheDocument();
    expect(within(debtRow).getByText('15/12/2025')).toBeInTheDocument();
    expect(within(debtRow).getByText('15/01/2026')).toBeInTheDocument();
    expect(within(debtRow).getByText('5.000.000')).toBeInTheDocument();
    expect(within(debtRow).getByText('2.000.000')).toBeInTheDocument();
    expect(within(debtRow).getByText('Nợ cũ')).toBeInTheDocument();
    expect(within(debtRow).getByText('Nguyễn Văn A')).toBeInTheDocument();

    const prepaymentRow = screen.getByText('UT01').closest('tr')!;
    expect(within(prepaymentRow).getByText('Phải trả')).toBeInTheDocument();
    expect(within(prepaymentRow).getByText('Trả trước')).toBeInTheDocument();
    expect(within(prepaymentRow).getByText('—')).toBeInTheDocument();

    expect(listParams).toHaveBeenLastCalledWith({ page: 1, pageSize: 20 });

    await user.selectOptions(screen.getByLabelText('Lọc phía'), 'Payable');
    await waitFor(() => expect(listParams).toHaveBeenLastCalledWith({ side: 'Payable', page: 1, pageSize: 20 }));

    // The filter partner may have any role.
    await user.type(screen.getByRole('combobox', { name: 'Lọc đối tượng' }), 'a');
    await user.click(await screen.findByRole('option', { name: /NCC04/ }));
    await waitFor(() =>
      expect(listParams).toHaveBeenLastCalledWith({
        side: 'Payable',
        partnerId: '44444444-4444-4444-8444-444444444444',
        page: 1,
        pageSize: 20,
      }),
    );
  });

  it('creates a receivable debt with due date', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole('button', { name: /Thêm/ }));
    const dialog = await screen.findByRole('dialog');
    expect(within(dialog).getByLabelText('Phía *')).toHaveValue('Receivable');
    expect(within(dialog).getByLabelText('Ngày CT *')).toHaveValue('2026-01-01');

    await user.type(within(dialog).getByRole('combobox', { name: 'Đối tượng *' }), 'cat');
    // Receivable lists customers only.
    expect(within(dialog).queryByRole('option', { name: /NCC04/ })).not.toBeInTheDocument();
    await user.click(await within(dialog).findByRole('option', { name: /KH003/ }));
    expect(sideSearches).toHaveBeenLastCalledWith('Receivable');

    await user.type(within(dialog).getByLabelText('Số CT *'), ' HD009 ');
    fireEvent.change(within(dialog).getByLabelText('Ngày CT *'), { target: { value: '2025-12-31' } });
    fireEvent.change(within(dialog).getByLabelText('Hạn TT'), { target: { value: '2026-02-28' } });
    await user.type(within(dialog).getByLabelText('Số tiền *'), '3.500.000');
    await user.type(within(dialog).getByLabelText('Ghi chú'), 'Chuyển từ sổ cũ');
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({
        partnerId: '33333333-3333-4333-8333-333333333333',
        side: 'Receivable',
        kind: 'Debt',
        docNo: 'HD009',
        docDate: '2025-12-31',
        dueDate: '2026-02-28',
        amount: 3500000,
        note: 'Chuyển từ sổ cũ',
        confirmRemoveSettlements: false,
      }),
    );
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));
  });

  it('creates with the picked owner', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole('button', { name: /Thêm/ }));
    const dialog = await screen.findByRole('dialog');
    const owner = within(dialog).getByLabelText('Nhân viên');
    expect(within(owner).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Không chọn',
      'Nguyễn Văn A',
      'Trần Thị B',
    ]);
    expect(owner).toHaveValue('');

    await user.type(within(dialog).getByRole('combobox', { name: 'Đối tượng *' }), 'cat');
    await user.click(await within(dialog).findByRole('option', { name: /KH003/ }));
    await user.type(within(dialog).getByLabelText('Số CT *'), 'HD010');
    await user.type(within(dialog).getByLabelText('Số tiền *'), '100000');
    await user.selectOptions(owner, '88888888-8888-4888-8888-888888888888');
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith(
        expect.objectContaining({ docNo: 'HD010', ownerUserId: '88888888-8888-4888-8888-888888888888' }),
      ),
    );
  });

  it('editing preselects the owner and Không chọn sends null', async () => {
    const user = userEvent.setup();
    renderPage();

    const row = screen.getByText('HD001').closest('tr')!;
    await user.click(within(row).getByRole('button', { name: 'Sửa nợ đầu kỳ' }));
    const dialog = await screen.findByRole('dialog');
    const owner = within(dialog).getByLabelText('Nhân viên');
    expect(owner).toHaveValue('99999999-9999-4999-8999-999999999999');

    await user.selectOptions(owner, '');
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));
    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({
        id: 'o1',
        data: expect.objectContaining({ ownerUserId: null, version: 11 }),
      }),
    );
  });

  it.each([
    ['Lê Văn Cũ', 'Lê Văn Cũ'],
    [null, 'Nhân viên đã bị xóa — vui lòng chọn lại'],
  ])('an owner missing from the options (name %s) stays selectable and is kept', async (ownerName, label) => {
    const user = userEvent.setup();
    extraRows = [goneOwnerRow(ownerName)];
    cachedRows = null;
    renderPage();

    const row = screen.getByText('HD777').closest('tr')!;
    await user.click(within(row).getByRole('button', { name: 'Sửa nợ đầu kỳ' }));
    const dialog = await screen.findByRole('dialog');
    const owner = within(dialog).getByLabelText('Nhân viên');
    expect(within(owner).getAllByRole('option').map((o) => o.textContent)).toEqual([
      'Không chọn',
      'Nguyễn Văn A',
      'Trần Thị B',
      label,
    ]);
    expect(owner).toHaveValue(GONE_OWNER_ID);

    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));
    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({
        id: 'o3',
        data: expect.objectContaining({ ownerUserId: GONE_OWNER_ID }),
      }),
    );
  });

  it('lists a row whose owner user was deleted as inactive, not as having no owner', () => {
    extraRows = [goneOwnerRow(null)];
    cachedRows = null;
    renderPage();

    const row = screen.getByText('HD777').closest('tr')!;
    expect(within(row).getByText('Nhân viên đã ngừng hoạt động')).toBeInTheDocument();
  });

  it('shows the server owner error under the select and clears it on change', async () => {
    const user = userEvent.setup();
    extraRows = [goneOwnerRow(null)];
    cachedRows = null;
    updateMock.mockRejectedValueOnce(
      apiError(400, 'VALIDATION_ERROR', 'Dữ liệu không hợp lệ.', { ownerUserId: ['Nhân viên không tồn tại.'] }),
    );
    renderPage();

    const row = screen.getByText('HD777').closest('tr')!;
    await user.click(within(row).getByRole('button', { name: 'Sửa nợ đầu kỳ' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    expect(await within(dialog).findByText('Nhân viên không tồn tại.')).toBeInTheDocument();
    await user.selectOptions(within(dialog).getByLabelText('Nhân viên'), '');
    expect(within(dialog).queryByText('Nhân viên không tồn tại.')).not.toBeInTheDocument();
  });

  it('prepayment hides due date', async () => {
    const user = userEvent.setup();
    renderPage();

    await user.click(screen.getByRole('button', { name: /Thêm/ }));
    const dialog = await screen.findByRole('dialog');

    // The picker follows the side: Payable lists suppliers.
    await user.selectOptions(within(dialog).getByLabelText('Phía *'), 'Payable');
    await user.type(within(dialog).getByRole('combobox', { name: 'Đối tượng *' }), 'a');
    expect(within(dialog).queryByRole('option', { name: /KH003/ })).not.toBeInTheDocument();
    await user.click(await within(dialog).findByRole('option', { name: /NCC04/ }));
    expect(sideSearches).toHaveBeenLastCalledWith('Payable');

    fireEvent.change(within(dialog).getByLabelText('Hạn TT'), { target: { value: '2026-02-28' } });
    await user.selectOptions(within(dialog).getByLabelText('Loại *'), 'Prepayment');
    expect(within(dialog).queryByLabelText('Hạn TT')).not.toBeInTheDocument();

    await user.type(within(dialog).getByLabelText('Số CT *'), 'UT09');
    await user.type(within(dialog).getByLabelText('Số tiền *'), '800000');
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(createMock).toHaveBeenCalledWith({
        partnerId: '44444444-4444-4444-8444-444444444444',
        side: 'Payable',
        kind: 'Prepayment',
        docNo: 'UT09',
        docDate: '2026-01-01',
        dueDate: null,
        amount: 800000,
        note: undefined,
        confirmRemoveSettlements: false,
      }),
    );
  });

  it('shrinking a settled row asks to remove settlements', async () => {
    const user = userEvent.setup();
    updateMock
      .mockRejectedValueOnce(
        apiError(422, 'DEBT_SETTLEMENTS_WILL_BE_REMOVED', 'Các đối trừ sau sẽ bị bỏ.', {
          HD001: ['PT00012: 2.000.000'],
        }, 't1'),
      )
      .mockResolvedValueOnce({});
    renderPage();

    const row = screen.getByText('HD001').closest('tr')!;
    await user.click(within(row).getByRole('button', { name: 'Sửa nợ đầu kỳ' }));
    const dialog = await screen.findByRole('dialog');
    const amount = within(dialog).getByLabelText('Số tiền *');
    expect(amount).toHaveValue('5.000.000');
    expect(within(dialog).getByLabelText('Hạn TT')).toHaveValue('2026-01-15');
    await user.clear(amount);
    await user.type(amount, '1.000.000');
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    const expected = {
      partnerId: '11111111-1111-4111-8111-111111111111',
      side: 'Receivable',
      kind: 'Debt',
      docNo: 'HD001',
      docDate: '2025-12-15',
      dueDate: '2026-01-15',
      amount: 1000000,
      // Unchanged: the loaded owner is sent back.
      ownerUserId: '99999999-9999-4999-8999-999999999999',
      note: 'Nợ cũ',
    };
    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({
        id: 'o1',
        data: { ...expected, confirmRemoveSettlements: false, version: 11 },
      }),
    );
    expect(await screen.findByText('Bỏ đối trừ chứng từ')).toBeInTheDocument();
    expect(screen.getByText('PT00012: 2.000.000')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: 'Vẫn tiếp tục' }));
    await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(2));
    expect(updateMock).toHaveBeenLastCalledWith({
      id: 'o1',
      data: { ...expected, confirmRemoveSettlements: true, confirmedSettlements: 't1', version: 11 },
    });
    await waitFor(() => expect(screen.queryByLabelText('Số tiền *')).not.toBeInTheDocument());
  });

  it('a stale version on save closes the dialog with a reload message', async () => {
    const user = userEvent.setup();
    updateMock.mockRejectedValueOnce(apiError(409, 'CONCURRENCY', 'Dữ liệu đã được người khác cập nhật.'));
    renderPage();

    const row = screen.getByText('HD001').closest('tr')!;
    await user.click(within(row).getByRole('button', { name: 'Sửa nợ đầu kỳ' }));
    const dialog = await screen.findByRole('dialog');
    await user.click(within(dialog).getByRole('button', { name: 'Lưu' }));

    await waitFor(() => expect(screen.queryByLabelText('Số tiền *')).not.toBeInTheDocument());
    expect(toast).toHaveBeenCalledWith(
      expect.objectContaining({ variant: 'destructive', title: expect.stringContaining('người khác cập nhật') }),
    );
  });

  it('deleting a settled row asks to remove settlements', async () => {
    const user = userEvent.setup();
    deleteMock
      .mockRejectedValueOnce(apiError(422, 'DEBT_SETTLEMENTS_WILL_BE_REMOVED', 'Các đối trừ sau sẽ bị bỏ.', undefined, 't1'))
      .mockResolvedValueOnce(undefined);
    renderPage();

    const row = screen.getByText('HD001').closest('tr')!;
    await user.click(within(row).getByRole('button', { name: 'Xóa nợ đầu kỳ' }));
    await user.click(await screen.findByRole('button', { name: 'Xóa' }));
    await waitFor(() => expect(deleteMock).toHaveBeenCalledWith({ id: 'o1', version: 11, confirmRemoveSettlements: false }));

    expect(await screen.findByText('Bỏ đối trừ chứng từ')).toBeInTheDocument();
    await user.click(screen.getByRole('button', { name: 'Vẫn tiếp tục' }));
    await waitFor(() => expect(deleteMock).toHaveBeenLastCalledWith({ id: 'o1', version: 11, confirmRemoveSettlements: true, confirmedSettlements: 't1' }));
    await waitFor(() => expect(toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' })));
  });

  it('no opening date disables adding', async () => {
    const user = userEvent.setup();
    openingDate = null;
    renderPage();

    expect(screen.getByRole('button', { name: /Thêm/ })).toBeDisabled();
    expect(screen.queryByText(/Ngày đầu kỳ:/)).not.toBeInTheDocument();
    const warning = screen.getByRole('alert');
    expect(warning).toHaveTextContent('Chi nhánh chưa có ngày đầu kỳ');

    await user.click(within(warning).getByRole('link', { name: 'Cài đặt → Chi nhánh' }));
    expect(await screen.findByText('Trang chi nhánh')).toBeInTheDocument();
  });

  it('opening date not set on save links to branch settings', async () => {
    const user = userEvent.setup();
    deleteMock.mockRejectedValueOnce(apiError(400, 'OPENING_DATE_NOT_SET', 'Chưa đặt ngày đầu kỳ của chi nhánh.'));
    renderPage();

    const row = screen.getByText('UT01').closest('tr')!;
    await user.click(within(row).getByRole('button', { name: 'Xóa nợ đầu kỳ' }));
    await user.click(await screen.findByRole('button', { name: 'Xóa' }));

    await waitFor(() =>
      expect(toast).toHaveBeenCalledWith(
        expect.objectContaining({
          variant: 'destructive',
          description: 'Chưa đặt ngày đầu kỳ của chi nhánh.',
          action: expect.objectContaining({ label: 'Mở Cài đặt → Chi nhánh' }),
        }),
      ),
    );
    const { action } = vi.mocked(toast).mock.calls[0][0];
    action!.onClick();
    expect(await screen.findByText('Trang chi nhánh')).toBeInTheDocument();
  });
});
