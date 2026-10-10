import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { CashSettingsPage } from './cash-settings-page';
import type { CashSettings } from '@/features/cash-settings/types';

const updateMock = vi.fn();
const SETTINGS: CashSettings = {
  creditLimitPolicy: 'Warn',
  negativeFundPolicy: 'Block',
  updatedAt: '2026-10-01T00:00:00Z',
};

vi.mock('@/features/cash-settings/hooks', () => ({
  useCashSettings: () => ({ data: SETTINGS, isLoading: false, isError: false }),
  useUpdateCashSettings: () => ({ mutateAsync: updateMock, isPending: false }),
}));

const toastMock = vi.fn();
vi.mock('@/lib/use-toast', () => ({ toast: (...args: unknown[]) => toastMock(...args) }));

describe('CashSettingsPage', () => {
  beforeEach(() => {
    updateMock.mockReset().mockResolvedValue(SETTINGS);
    toastMock.mockReset();
  });

  it('loads and saves policies', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <CashSettingsPage />
      </MemoryRouter>,
    );

    expect(screen.getByText('Tiền & Công nợ')).toBeInTheDocument();
    expect(screen.getByRole('combobox', { name: 'Vượt hạn mức nợ' })).toHaveTextContent('Cảnh báo');
    expect(screen.getByRole('combobox', { name: 'Chi âm quỹ' })).toHaveTextContent('Chặn');

    await user.click(screen.getByRole('combobox', { name: 'Vượt hạn mức nợ' }));
    await user.click(screen.getByRole('option', { name: 'Chặn' }));
    await user.click(screen.getByRole('combobox', { name: 'Chi âm quỹ' }));
    await user.click(screen.getByRole('option', { name: 'Cho phép' }));
    await user.click(screen.getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({ creditLimitPolicy: 'Block', negativeFundPolicy: 'Allow' }),
    );
    expect(toastMock).toHaveBeenCalledWith(expect.objectContaining({ variant: 'success' }));
  });
});
