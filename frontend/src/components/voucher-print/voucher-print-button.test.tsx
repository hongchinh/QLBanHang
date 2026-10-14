import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';

const printMock = vi.fn();
vi.mock('@/features/voucher-print/use-voucher-print', () => ({
  useVoucherPrint: () => ({ print: printMock, isPending: false }),
}));

const profile: { data: { name: string | null } | undefined } = { data: { name: 'Công ty A' } };
vi.mock('@/features/company-profile/hooks', () => ({ useCompanyProfile: () => profile }));

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
    profile.data = { name: 'Công ty A' };
  });

  it('prints a PDF', async () => {
    const user = userEvent.setup();
    renderButton();
    await user.click(screen.getByRole('button', { name: 'In' }));
    await user.click(screen.getByRole('menuitem', { name: 'In (PDF)' }));
    expect(printMock).toHaveBeenCalledWith({ kind: 'cash', type: 'Receipt', vouchers: VOUCHERS, format: 'pdf' });
  });

  it('prints Excel', async () => {
    const user = userEvent.setup();
    renderButton();
    await user.click(screen.getByRole('button', { name: 'In' }));
    await user.click(screen.getByRole('menuitem', { name: 'Excel' }));
    expect(printMock).toHaveBeenCalledWith({ kind: 'cash', type: 'Receipt', vouchers: VOUCHERS, format: 'xlsx' });
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
    expect(printMock).toHaveBeenCalledWith({ kind: 'cash', type: 'Receipt', vouchers: VOUCHERS, format: 'xlsx' });
    expect(parentClick).not.toHaveBeenCalled();
  });
});
