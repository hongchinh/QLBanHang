import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const printMock = vi.fn();
const printState = { isPending: false };
vi.mock('@/features/voucher-print/use-voucher-print', () => ({
  useVoucherPrint: () => ({ print: printMock, isPending: printState.isPending }),
}));

const profile: { data: { name: string | null } | undefined } = { data: { name: 'Công ty A' } };
vi.mock('@/features/company-profile/hooks', () => ({ useCompanyProfile: () => profile }));

import { useAuthStore } from '@/stores/auth-store';
import { VoucherPrintButton, type VoucherPrintButtonProps } from './voucher-print-button';

const VOUCHERS = [{ id: 'v1', code: 'PT00001' }];

function renderButton(props: Partial<VoucherPrintButtonProps> = {}, parentClick?: () => void) {
  const button = <VoucherPrintButton kind="cash" type="Receipt" vouchers={VOUCHERS} {...props} />;
  return render(
    <MemoryRouter>
      {parentClick ? (
        <>
          {/* eslint-disable-next-line jsx-a11y/click-events-have-key-events, jsx-a11y/no-static-element-interactions -- stands in for a clickable table row */}
          <div onClick={parentClick}>{button}</div>
        </>
      ) : (
        button
      )}
    </MemoryRouter>,
  );
}

describe('VoucherPrintButton', () => {
  beforeEach(() => {
    printMock.mockReset();
    printState.isPending = false;
    profile.data = { name: 'Công ty A' };
    useAuthStore.setState({
      user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions: [] },
    });
    localStorage.clear();
  });

  it('shows the print and chevron icons when idle, and the format icons in the menu', async () => {
    const user = userEvent.setup();
    renderButton();
    const button = screen.getByRole('button', { name: 'In' });
    expect(button.querySelector('[data-icon="print"]')).not.toBeNull();
    expect(button.querySelector('[data-icon="print"]')).not.toHaveClass('text-icon-print');
    expect(button.querySelector('[data-icon="chevronDown"]')).not.toBeNull();
    await user.click(button);
    expect(screen.getByRole('menuitem', { name: 'In (PDF)' }).querySelector('[data-icon="exportPdf"]')).not.toBeNull();
    expect(screen.getByRole('menuitem', { name: 'Excel' }).querySelector('[data-icon="exportExcel"]')).not.toBeNull();
  });

  it.each([
    ['button', 'In'],
    ['icon', 'In PT00001'],
  ] as const)('%s variant shows the button loader while printing', (variant, name) => {
    printState.isPending = true;
    renderButton({ variant });
    const button = screen.getByRole('button', { name });
    expect(button.querySelector('.button-loader')).not.toBeNull();
    expect(button.querySelector('svg.animate-spin')).toBeNull();
  });

  it('prints a PDF', async () => {
    const user = userEvent.setup();
    renderButton();
    await user.click(screen.getByRole('button', { name: 'In' }));
    await user.click(screen.getByRole('menuitem', { name: 'In (PDF)' }));
    expect(printMock).toHaveBeenCalledWith({
      kind: 'cash',
      type: 'Receipt',
      vouchers: VOUCHERS,
      format: 'pdf',
      includeQr: true,
    });
  });

  it('prints Excel', async () => {
    const user = userEvent.setup();
    renderButton();
    await user.click(screen.getByRole('button', { name: 'In' }));
    await user.click(screen.getByRole('menuitem', { name: 'Excel' }));
    expect(printMock).toHaveBeenCalledWith({
      kind: 'cash',
      type: 'Receipt',
      vouchers: VOUCHERS,
      format: 'xlsx',
      includeQr: true,
    });
  });

  it('is disabled with a reason', () => {
    renderButton({ disabled: true, disabledReason: 'Lưu thay đổi trước khi in' });
    const button = screen.getByRole('button', { name: 'In' });
    expect(button).toBeDisabled();
    expect(button).toHaveAttribute('title', 'Lưu thay đổi trước khi in');
  });

  it('is disabled without vouchers', () => {
    renderButton({ vouchers: [] });
    expect(screen.getByRole('button', { name: 'In' })).toBeDisabled();
  });

  it('shows the company hint only for the button variant', () => {
    profile.data = { name: '' };
    const { unmount } = renderButton();
    expect(screen.getByText('Chưa có thông tin đơn vị')).toBeInTheDocument();
    unmount();
    renderButton({ variant: 'icon' });
    expect(screen.queryByText('Chưa có thông tin đơn vị')).not.toBeInTheDocument();
  });

  it('icon variant stays inside the row', async () => {
    const user = userEvent.setup();
    const parentClick = vi.fn();
    renderButton({ variant: 'icon' }, parentClick);
    await user.click(screen.getByRole('button', { name: 'In PT00001' }));
    await user.click(screen.getByRole('menuitem', { name: 'Excel' }));
    expect(printMock).toHaveBeenCalledWith({
      kind: 'cash',
      type: 'Receipt',
      vouchers: VOUCHERS,
      format: 'xlsx',
      includeQr: true,
    });
    expect(parentClick).not.toHaveBeenCalled();
  });

  it('stock: the format opens the price mode dialog', async () => {
    const user = userEvent.setup();
    renderButton({ kind: 'stock', type: 'In' });
    await user.click(screen.getByRole('button', { name: 'In' }));
    await user.click(screen.getByRole('menuitem', { name: 'Excel' }));
    expect(await screen.findByText('Chọn giá in')).toBeInTheDocument();
    expect(printMock).not.toHaveBeenCalled();
    await user.click(screen.getByRole('radio', { name: 'Không in tiền' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'In' }));
    expect(printMock).toHaveBeenCalledWith({
      kind: 'stock',
      type: 'In',
      vouchers: VOUCHERS,
      format: 'xlsx',
      priceMode: 'None',
    });
  });

  it('stock: cancelling the dialog prints nothing', async () => {
    const user = userEvent.setup();
    renderButton({ kind: 'stock', type: 'In' });
    await user.click(screen.getByRole('button', { name: 'In' }));
    await user.click(screen.getByRole('menuitem', { name: 'In (PDF)' }));
    await user.click(await screen.findByRole('button', { name: 'Hủy' }));
    expect(printMock).not.toHaveBeenCalled();
    expect(screen.queryByText('Chọn giá in')).not.toBeInTheDocument();
  });

  it('cash prints without the dialog', async () => {
    const user = userEvent.setup();
    renderButton();
    await user.click(screen.getByRole('button', { name: 'In' }));
    await user.click(screen.getByRole('menuitem', { name: 'Excel' }));
    expect(screen.queryByText('Chọn giá in')).not.toBeInTheDocument();
    expect(printMock).toHaveBeenCalledTimes(1);
  });

  it('icon variant: the dialog stays inside the row', async () => {
    const user = userEvent.setup();
    const parentClick = vi.fn();
    renderButton({ kind: 'stock', type: 'In', variant: 'icon' }, parentClick);
    await user.click(screen.getByRole('button', { name: 'In PT00001' }));
    await user.click(screen.getByRole('menuitem', { name: 'In (PDF)' }));
    await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'In' }));
    expect(printMock).toHaveBeenCalledWith(expect.objectContaining({ kind: 'stock', format: 'pdf' }));
    expect(parentClick).not.toHaveBeenCalled();
  });

  it('stock: Escape on the dialog returns the focus to the trigger', async () => {
    const user = userEvent.setup();
    renderButton({ kind: 'stock', type: 'In' });
    const trigger = screen.getByRole('button', { name: 'In' });
    await user.click(trigger);
    await user.click(screen.getByRole('menuitem', { name: 'Excel' }));
    await screen.findByRole('dialog');
    await user.keyboard('{Escape}');
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  it('stock: Hủy returns the focus to the trigger', async () => {
    const user = userEvent.setup();
    renderButton({ kind: 'stock', type: 'In', variant: 'icon' });
    const trigger = screen.getByRole('button', { name: 'In PT00001' });
    await user.click(trigger);
    await user.click(screen.getByRole('menuitem', { name: 'In (PDF)' }));
    await user.click(await screen.findByRole('button', { name: 'Hủy' }));
    await waitFor(() => expect(trigger).toHaveFocus());
  });

  describe('QR switch in the menu', () => {
    const QR = 'Kèm QR chuyển khoản';
    const QR_KEY = 'qldh:voucher-print-qr:u1';

    it.each([
      ['cash', 'Receipt', true],
      ['cash', 'Payment', false],
      ['stock', 'In', false],
      ['stock', 'Out', false],
    ] as const)('shows the QR item for receipts only: %s %s', async (kind, type, shown) => {
      const user = userEvent.setup();
      renderButton({ kind, type });
      await user.click(screen.getByRole('button', { name: 'In' }));
      const item = screen.queryByRole('menuitemcheckbox', { name: QR });
      if (shown) expect(item).toBeInTheDocument();
      else expect(item).not.toBeInTheDocument();
    });

    it('defaults to on and remembers', async () => {
      const user = userEvent.setup();
      const { unmount } = renderButton();
      await user.click(screen.getByRole('button', { name: 'In' }));
      expect(screen.getByRole('menuitemcheckbox', { name: QR })).toBeChecked();
      await user.click(screen.getByRole('menuitemcheckbox', { name: QR }));
      expect(screen.getByRole('menuitemcheckbox', { name: QR })).not.toBeChecked();
      expect(localStorage.getItem(QR_KEY)).toBe('0');
      unmount();

      renderButton();
      await user.click(screen.getByRole('button', { name: 'In' }));
      expect(screen.getByRole('menuitemcheckbox', { name: QR })).not.toBeChecked();
    });

    it('menu stays open after the tick', async () => {
      const user = userEvent.setup();
      renderButton();
      await user.click(screen.getByRole('button', { name: 'In' }));
      await user.click(screen.getByRole('menuitemcheckbox', { name: QR }));
      expect(screen.getByRole('menu')).toBeInTheDocument();
      expect(screen.getByRole('menuitem', { name: 'In (PDF)' })).toBeInTheDocument();
    });

    it('print carries the switch', async () => {
      const user = userEvent.setup();
      renderButton();
      await user.click(screen.getByRole('button', { name: 'In' }));
      await user.click(screen.getByRole('menuitem', { name: 'In (PDF)' }));
      expect(printMock).toHaveBeenLastCalledWith({
        kind: 'cash',
        type: 'Receipt',
        vouchers: VOUCHERS,
        format: 'pdf',
        includeQr: true,
      });

      await user.click(screen.getByRole('button', { name: 'In' }));
      await user.click(screen.getByRole('menuitemcheckbox', { name: QR }));
      await user.click(screen.getByRole('menuitem', { name: 'In (PDF)' }));
      expect(printMock).toHaveBeenLastCalledWith({
        kind: 'cash',
        type: 'Receipt',
        vouchers: VOUCHERS,
        format: 'pdf',
        includeQr: false,
      });
    });

    it('a payment voucher sends no includeQr', async () => {
      const user = userEvent.setup();
      renderButton({ type: 'Payment' });
      await user.click(screen.getByRole('button', { name: 'In' }));
      await user.click(screen.getByRole('menuitem', { name: 'In (PDF)' }));
      expect(Object.keys(printMock.mock.calls[0][0])).not.toContain('includeQr');
      expect(printMock).toHaveBeenCalledWith({ kind: 'cash', type: 'Payment', vouchers: VOUCHERS, format: 'pdf' });
    });
  });

  describe('focus after a print', () => {
    async function printWhilePending() {
      const user = userEvent.setup();
      printMock.mockImplementation(() => {
        printState.isPending = true; // a running print disables the trigger
      });
      const ui = () => (
        <MemoryRouter>
          <input aria-label="Tìm kiếm" />
          <VoucherPrintButton kind="stock" type="In" vouchers={VOUCHERS} />
        </MemoryRouter>
      );
      const view = render(ui());
      const trigger = screen.getByRole('button', { name: 'In' });
      await user.click(trigger);
      await user.click(screen.getByRole('menuitem', { name: 'In (PDF)' }));
      await user.click(within(await screen.findByRole('dialog')).getByRole('button', { name: 'In' }));
      await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
      expect(trigger).toBeDisabled();
      const finish = () => {
        printState.isPending = false;
        view.rerender(ui());
      };
      return { user, trigger, finish };
    }

    it('keeps the focus where the user went while the print ran', async () => {
      const { user, trigger, finish } = await printWhilePending();
      const input = screen.getByRole('textbox', { name: 'Tìm kiếm' });
      await user.click(input);
      finish();
      expect(trigger).toBeEnabled();
      expect(input).toHaveFocus();
    });

    it('gives the focus back to the trigger when nothing else has it', async () => {
      const { trigger, finish } = await printWhilePending();
      finish();
      await waitFor(() => expect(trigger).toHaveFocus());
    });
  });
});
