import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { FundFormDialog } from './fund-form-dialog';
import type { Fund } from '@/features/funds/types';

const createMock = vi.fn();
const updateMock = vi.fn();

vi.mock('@/features/funds/hooks', () => ({
  useCreateFund: () => ({ mutateAsync: createMock, isPending: false }),
  useUpdateFund: () => ({ mutateAsync: updateMock, isPending: false }),
}));

vi.mock('@/features/banks/hooks', () => ({
  useBanks: () => ({
    data: [
      { id: 'b1', code: 'VCB', name: 'Ngân hàng Vietcombank', shortName: 'Vietcombank', bin: '970436' },
      { id: 'b2', code: 'TCB', name: 'Techcombank', bin: '970407' },
    ],
  }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

const CASH_FUND: Fund = {
  id: 'f1',
  branchId: 'br1',
  code: 'QTM',
  name: 'Quỹ tiền mặt',
  kind: 'Cash',
  bankName: null,
  accountNumber: null,
  isDefault: true,
  openingAmount: 0,
  isActive: true,
};

const BANK_FUND: Fund = {
  id: 'f2',
  branchId: 'br1',
  code: 'VCB',
  name: 'TK Vietcombank',
  kind: 'Bank',
  bankName: 'Vietcombank',
  accountNumber: '0123456789',
  bankId: 'b1',
  accountHolderName: 'CONG TY ABC',
  isDefault: false,
  openingAmount: 0,
  isActive: true,
};

function renderDialog(initial: Fund) {
  return render(<FundFormDialog open onOpenChange={vi.fn()} initial={initial} />);
}

describe('FundFormDialog bank fields', () => {
  beforeEach(() => {
    createMock.mockReset().mockResolvedValue({});
    updateMock.mockReset().mockResolvedValue({});
  });

  it('shows_bank_fields_only_for_a_bank_fund', () => {
    const { unmount } = renderDialog(CASH_FUND);
    expect(screen.queryByText('Ngân hàng (cho QR)')).not.toBeInTheDocument();
    expect(screen.queryByText('Chủ tài khoản')).not.toBeInTheDocument();
    unmount();

    renderDialog(BANK_FUND);
    expect(screen.getByText('Ngân hàng (cho QR)')).toBeInTheDocument();
    expect(screen.getByLabelText('Chủ tài khoản')).toBeInTheDocument();
    expect(
      screen.getByText('Có đủ ngân hàng, số tài khoản và chủ tài khoản thì phiếu thu/xuất in được QR.'),
    ).toBeInTheDocument();
  });

  it('bank_options_show_short_name_and_bin', async () => {
    const user = userEvent.setup();
    renderDialog(BANK_FUND);

    await user.click(screen.getByRole('combobox', { name: 'Ngân hàng (cho QR)' }));
    expect(await screen.findByRole('option', { name: 'Vietcombank · 970436' })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: 'TCB · 970407' })).toBeInTheDocument();
  });

  it('submits_bank_and_holder', async () => {
    const user = userEvent.setup();
    renderDialog(BANK_FUND);

    await user.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(1));
    expect(updateMock.mock.calls[0][0].data).toEqual(
      expect.objectContaining({
        bankId: 'b1',
        accountHolderName: 'CONG TY ABC',
        accountNumber: '0123456789',
      }),
    );
  });

  it('cash_fund_sends_no_bank_fields', async () => {
    const user = userEvent.setup();
    renderDialog(CASH_FUND);

    await user.click(screen.getByRole('button', { name: 'Cập nhật' }));

    await waitFor(() => expect(updateMock).toHaveBeenCalledTimes(1));
    expect(updateMock.mock.calls[0][0].data).toEqual(
      expect.objectContaining({
        bankId: null,
        accountHolderName: null,
        bankName: null,
        accountNumber: null,
      }),
    );
  });

  it('rejects_a_bad_account_number_with_a_bank', async () => {
    const user = userEvent.setup();
    renderDialog({ ...BANK_FUND, accountNumber: '12 34' });

    await user.click(screen.getByRole('button', { name: 'Cập nhật' }));

    expect(await screen.findByText('Số tài khoản chỉ gồm chữ và số, tối đa 19 ký tự.')).toBeInTheDocument();
    expect(updateMock).not.toHaveBeenCalled();
  });
});
