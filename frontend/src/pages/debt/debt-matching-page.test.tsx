import { beforeEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { DebtMatchingPage } from './debt-matching-page';
import { ApiCallError } from '@/lib/api-client';
import type {
  DebtPartnerSearchItem,
  DebtSettlement,
  OpenItem,
  OpenItemsParams,
  SettlementListParams,
} from '@/features/debt/types';

const mocks = vi.hoisted(() => ({
  partnerSearch: vi.fn(),
  openItems: vi.fn(),
  settlements: vi.fn(),
  create: vi.fn(),
  deleteOne: vi.fn(),
  deleteBatch: vi.fn(),
  toast: vi.fn(),
}));

vi.mock('@/features/debt/hooks', () => ({
  useDebtPartnerSearch: (...args: unknown[]) => mocks.partnerSearch(...args),
  useOpenItems: (params: OpenItemsParams) => mocks.openItems(params),
  useDebtSettlements: (params: SettlementListParams, opts?: unknown) => mocks.settlements(params, opts),
  useCreateSettlementBatch: () => ({ mutate: mocks.create, isPending: false }),
  useDeleteSettlement: () => ({ mutate: mocks.deleteOne, isPending: false }),
  useDeleteSettlementBatch: () => ({ mutate: mocks.deleteBatch, isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: mocks.toast }));

const P1 = '11111111-1111-4111-8111-111111111111';
const D1 = 'd1d1d1d1-0000-4000-8000-000000000001';
const I1 = 'a1a1a1a1-0000-4000-8000-000000000001';
const I2 = 'a2a2a2a2-0000-4000-8000-000000000002';
const I3 = 'a3a3a3a3-0000-4000-8000-000000000003';

const partner: DebtPartnerSearchItem = {
  id: P1,
  code: 'KH01',
  name: 'Công ty An Phát',
  status: 'Active',
  isCustomer: true,
  isSupplier: false,
};

function item(overrides: Partial<OpenItem>): OpenItem {
  return {
    entryId: I1,
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

const decreases: OpenItem[] = [
  item({ entryId: D1, sourceType: 'Receipt', docCode: 'PT00001', docDate: '2026-10-06', direction: 'Decrease', amount: 3_000_000, open: 3_000_000 }),
];
// Oldest first: PX00001 (due 10-10), PX00002 (due = doc date 10-12), PX00003 (10-20).
const increases: OpenItem[] = [
  item({ entryId: I3, docCode: 'PX00003', docDate: '2026-10-20', amount: 5_000_000, open: 5_000_000 }),
  item({ entryId: I1, docCode: 'PX00001', docDate: '2026-10-01', dueDate: '2026-10-10', amount: 1_000_000, open: 1_000_000 }),
  item({ entryId: I2, docCode: 'PX00002', docDate: '2026-10-12', amount: 2_000_000, open: 2_000_000 }),
];

function settlement(overrides: Partial<DebtSettlement>): DebtSettlement {
  return {
    id: 's1',
    batchId: 'b1',
    origin: 'Manual',
    increaseEntryId: I1,
    increaseDocCode: 'PX00001',
    increaseDocDate: '2026-10-01',
    increaseSourceType: 'StockOut',
    increaseSourceId: 'sx1',
    decreaseEntryId: D1,
    decreaseDocCode: 'PT00001',
    decreaseDocDate: '2026-10-06',
    decreaseSourceType: 'Receipt',
    decreaseSourceId: 'rc1',
    amount: 1_000_000,
    effectiveAt: '2026-10-06T03:00:00Z',
    createdAt: '2026-10-07T03:00:00Z',
    createdByName: 'Kế toán A',
    ...overrides,
  };
}

function LocationProbe() {
  const location = useLocation();
  return <div data-testid="location">{location.search}</div>;
}

function renderPage(entry = '/debt/matching') {
  return render(
    <MemoryRouter initialEntries={[entry]}>
      <Routes>
        <Route
          path="/debt/matching"
          element={
            <>
              <DebtMatchingPage />
              <LocationProbe />
            </>
          }
        />
      </Routes>
    </MemoryRouter>,
  );
}

const withPartner = `/debt/matching?side=Receivable&partnerId=${P1}&partnerName=${encodeURIComponent('KH01 - Công ty An Phát')}`;

function currentSearch() {
  return new URLSearchParams(screen.getByTestId('location').textContent ?? '');
}

function leftGrid() {
  return screen.getByRole('region', { name: 'Chứng từ giảm nợ chưa đối trừ' });
}

function rightGrid() {
  return screen.getByRole('region', { name: 'Chứng từ ghi nợ còn nợ' });
}

function enter(input: HTMLElement, text: string) {
  fireEvent.focus(input);
  fireEvent.change(input, { target: { value: text } });
  fireEvent.blur(input);
}

describe('DebtMatchingPage', () => {
  beforeEach(() => {
    mocks.partnerSearch.mockReset().mockReturnValue({ data: [partner], isLoading: false, isError: false });
    mocks.openItems.mockReset().mockImplementation((params: OpenItemsParams) => ({
      data: params.direction === 'Decrease' ? decreases : increases,
      isLoading: false,
      isError: false,
    }));
    mocks.settlements.mockReset().mockReturnValue({ data: [], isLoading: false, isError: false });
    mocks.create.mockReset();
    mocks.deleteOne.mockReset();
    mocks.deleteBatch.mockReset();
    mocks.toast.mockReset();
  });

  it('loads open items for the selected partner and side', async () => {
    const user = userEvent.setup();
    renderPage();

    expect(screen.getByText('Chọn đối tượng để đối trừ chứng từ.')).toBeInTheDocument();
    expect(mocks.openItems).not.toHaveBeenCalled();
    expect(screen.getByRole('button', { name: 'Phải thu' })).toHaveAttribute('aria-pressed', 'true');

    await user.click(screen.getByRole('combobox', { name: 'Đối tượng' }));
    await user.click(await screen.findByRole('option', { name: /KH01/ }));

    expect(mocks.partnerSearch).toHaveBeenLastCalledWith('Receivable', expect.any(String), {
      bothRoles: false,
      includeInactive: true,
    });
    expect(mocks.openItems).toHaveBeenCalledWith({ partnerId: P1, side: 'Receivable', direction: 'Decrease' });
    expect(mocks.openItems).toHaveBeenCalledWith({ partnerId: P1, side: 'Receivable', direction: 'Increase' });
    expect(within(leftGrid()).getByText('PT00001')).toBeInTheDocument();
    expect(within(rightGrid()).getByText('PX00003')).toBeInTheDocument();
    expect(within(rightGrid()).queryByText('PT00001')).not.toBeInTheDocument();
    expect(currentSearch().get('side')).toBe('Receivable');
    expect(currentSearch().get('partnerId')).toBe(P1);

    // A partner belongs to one side's list: switching the side clears it.
    await user.click(screen.getByRole('button', { name: 'Phải trả' }));

    expect(currentSearch().get('side')).toBe('Payable');
    expect(currentSearch().get('partnerId')).toBeNull();
    expect(screen.getByRole('button', { name: 'Phải trả' })).toHaveAttribute('aria-pressed', 'true');
    expect(mocks.partnerSearch).toHaveBeenLastCalledWith('Payable', expect.any(String), {
      bothRoles: false,
      includeInactive: true,
    });
    expect(screen.getByText('Chọn đối tượng để đối trừ chứng từ.')).toBeInTheDocument();
  });

  it('suggest then save sends paired items', async () => {
    renderPage(withPartner);

    expect(screen.getByRole('combobox', { name: 'Đối tượng' })).toHaveValue('KH01 - Công ty An Phát');

    fireEvent.click(screen.getByRole('button', { name: 'Gợi ý' }));

    expect(within(leftGrid()).getByRole('textbox', { name: 'Đối trừ PT00001' })).toHaveValue('3.000.000');
    expect(within(rightGrid()).getByRole('textbox', { name: 'Đối trừ PX00001' })).toHaveValue('1.000.000');
    expect(within(rightGrid()).getByRole('textbox', { name: 'Đối trừ PX00002' })).toHaveValue('2.000.000');
    expect(within(rightGrid()).getByRole('textbox', { name: 'Đối trừ PX00003' })).toHaveValue('');
    expect(screen.getByTestId('sum-decrease')).toHaveTextContent('3.000.000');
    expect(screen.getByTestId('sum-increase')).toHaveTextContent('3.000.000');

    const save = screen.getByRole('button', { name: 'Đối trừ' });
    expect(save).toBeEnabled();
    fireEvent.click(save);

    expect(mocks.create).toHaveBeenCalledTimes(1);
    const [payload, options] = mocks.create.mock.calls[0];
    expect(payload).toEqual({
      partnerId: P1,
      side: 'Receivable',
      items: [
        { increaseEntryId: I1, decreaseEntryId: D1, amount: 1_000_000 },
        { increaseEntryId: I2, decreaseEntryId: D1, amount: 2_000_000 },
      ],
    });

    // Success clears both sides.
    act(() => options.onSuccess({ batchId: 'b9', settlements: [] }));
    await waitFor(() => expect(screen.getByTestId('sum-decrease')).toHaveTextContent('0'));
    expect(screen.getByTestId('sum-increase')).toHaveTextContent('0');
    expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));

    // "Xóa" clears a suggestion.
    fireEvent.click(screen.getByRole('button', { name: 'Gợi ý' }));
    expect(screen.getByTestId('sum-increase')).toHaveTextContent('3.000.000');
    fireEvent.click(screen.getByRole('button', { name: 'Xóa' }));
    expect(screen.getByTestId('sum-decrease')).toHaveTextContent('0');
    expect(screen.getByTestId('sum-increase')).toHaveTextContent('0');
  });

  it('shows the server item errors in a toast', () => {
    mocks.create.mockImplementation((_payload, options) =>
      options.onError(
        new ApiCallError({
          code: 'VALIDATION',
          message: 'Dữ liệu không hợp lệ.',
          details: { 'items[0].amount': ['Số tiền đối trừ vượt số còn nợ của chứng từ.'] },
        }),
      ),
    );
    renderPage(withPartner);

    fireEvent.click(screen.getByRole('button', { name: 'Gợi ý' }));
    fireEvent.click(screen.getByRole('button', { name: 'Đối trừ' }));

    expect(mocks.toast).toHaveBeenCalledWith(
      expect.objectContaining({
        variant: 'destructive',
        description: 'Dữ liệu không hợp lệ.; Số tiền đối trừ vượt số còn nợ của chứng từ.',
      }),
    );
    // The selection stays for a retry.
    expect(screen.getByTestId('sum-decrease')).toHaveTextContent('3.000.000');
  });

  it('save disabled when sums differ', () => {
    renderPage(withPartner);

    const save = screen.getByRole('button', { name: 'Đối trừ' });
    expect(save).toBeDisabled();

    enter(within(leftGrid()).getByRole('textbox', { name: 'Đối trừ PT00001' }), '1000000');
    enter(within(rightGrid()).getByRole('textbox', { name: 'Đối trừ PX00001' }), '500000');

    expect(screen.getByTestId('sum-decrease')).toHaveTextContent('1.000.000');
    expect(screen.getByTestId('sum-increase')).toHaveTextContent('500.000');
    expect(save).toBeDisabled();

    enter(within(rightGrid()).getByRole('textbox', { name: 'Đối trừ PX00002' }), '500000');
    expect(save).toBeEnabled();

    // An amount above the open amount blocks the save even when the sums match.
    enter(within(leftGrid()).getByRole('textbox', { name: 'Đối trừ PT00001' }), '4000000');
    enter(within(rightGrid()).getByRole('textbox', { name: 'Đối trừ PX00003' }), '3000000');
    expect(screen.getByTestId('sum-decrease')).toHaveTextContent('4.000.000');
    expect(screen.getByTestId('sum-increase')).toHaveTextContent('4.000.000');
    expect(save).toBeDisabled();
    expect(mocks.create).not.toHaveBeenCalled();
  });

  it('unmatch single and batch', async () => {
    mocks.settlements.mockReturnValue({
      data: [
        settlement({ id: 's1', batchId: 'b1', origin: 'Manual', amount: 1_000_000 }),
        settlement({ id: 's2', batchId: 'b1', origin: 'Manual', increaseEntryId: I2, increaseDocCode: 'PX00002', amount: 2_000_000 }),
        settlement({ id: 's3', batchId: 'b2', origin: 'AtVoucher', increaseDocCode: 'PX00009', decreaseDocCode: 'PT00005', amount: 400_000 }),
      ],
      isLoading: false,
      isError: false,
    });
    const user = userEvent.setup();
    renderPage(withPartner);

    await user.click(screen.getByRole('tab', { name: 'Đã đối trừ' }));

    expect(mocks.settlements).toHaveBeenLastCalledWith({ partnerId: P1, side: 'Receivable' }, expect.anything());
    const manual = screen.getByTestId('settlement-batch-b1');
    expect(within(manual).getByText('Thủ công')).toBeInTheDocument();
    expect(within(manual).getByTestId('batch-total')).toHaveTextContent('3.000.000');
    expect(within(screen.getByTestId('settlement-batch-b2')).getByText('Lúc lập phiếu')).toBeInTheDocument();

    // One settlement.
    const row = within(manual).getByText('PX00002').closest('tr')!;
    await user.click(within(row).getByRole('button', { name: 'Bỏ' }));
    expect(mocks.deleteOne).not.toHaveBeenCalled();
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Bỏ đối trừ' }));
    expect(mocks.deleteOne).toHaveBeenCalledWith('s2', expect.anything());
    // The dialog stays open until the server answers.
    act(() => mocks.deleteOne.mock.calls[0][1].onSuccess());
    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
    expect(mocks.toast).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));

    // The whole batch.
    await user.click(within(manual).getByRole('button', { name: 'Bỏ cả lô' }));
    expect(mocks.deleteBatch).not.toHaveBeenCalled();
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Bỏ đối trừ' }));
    expect(mocks.deleteBatch).toHaveBeenCalledWith('b1', expect.anything());

    // A server refusal (period lock) shows its message.
    const [, options] = mocks.deleteBatch.mock.calls[0];
    act(() => options.onError(new ApiCallError({ code: 'PERIOD_LOCKED', message: 'Kỳ đã khóa sổ.' })));
    expect(mocks.toast).toHaveBeenCalledWith(
      expect.objectContaining({ variant: 'destructive', description: 'Kỳ đã khóa sổ.' }),
    );
  });

  it('managed settlements have no unmatch button', async () => {
    mocks.settlements.mockReturnValue({
      data: [
        settlement({ id: 's4', batchId: 'b3', origin: 'Auto' }),
        settlement({ id: 's5', batchId: 'b4', origin: 'Offset', decreaseSourceType: 'Offset', decreaseDocCode: 'BT00001' }),
        settlement({ id: 's6', batchId: 'b5', origin: 'Manual', increaseDocCode: 'PX00004' }),
      ],
      isLoading: false,
      isError: false,
    });
    const user = userEvent.setup();
    renderPage(withPartner);

    await user.click(screen.getByRole('tab', { name: 'Đã đối trừ' }));

    for (const [batchId, label] of [
      ['b3', 'Tự động'],
      ['b4', 'Bù trừ'],
    ] as const) {
      const group = screen.getByTestId(`settlement-batch-${batchId}`);
      expect(within(group).getByText(label)).toBeInTheDocument();
      expect(within(group).getAllByText('Do chứng từ quản lý').length).toBeGreaterThan(0);
      expect(within(group).queryByRole('button')).not.toBeInTheDocument();
    }
    const manual = screen.getByTestId('settlement-batch-b5');
    expect(within(manual).getByRole('button', { name: 'Bỏ' })).toBeInTheDocument();
    expect(within(manual).getByRole('button', { name: 'Bỏ cả lô' })).toBeInTheDocument();
    expect(within(manual).queryByText('Do chứng từ quản lý')).not.toBeInTheDocument();
  });
});
