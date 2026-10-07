import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { InventorySettingsPage } from './inventory-settings-page';
import type { InventorySettings } from '@/features/inventory-settings/types';

const updateMock = vi.fn();
const SETTINGS: InventorySettings = {
  costingMethod: 'PeriodicAverage',
  costingPeriod: 'Month',
  costingScope: 'Branch',
  purchaseCostIncludesVat: true,
  negativeStockPolicy: 'Warn',
  netExcludesVat: false,
  defaultDateMode: 'Now',
};

vi.mock('@/features/inventory-settings/hooks', () => ({
  useInventorySettings: () => ({ data: SETTINGS, isLoading: false, isError: false }),
  useUpdateInventorySettings: () => ({ mutateAsync: updateMock, isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

describe('InventorySettingsPage', () => {
  beforeEach(() => {
    updateMock.mockReset().mockResolvedValue(SETTINGS);
  });

  it('saving a costing change asks for confirmation then submits', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <InventorySettingsPage />
      </MemoryRouter>,
    );

    await user.click(screen.getByRole('combobox', { name: 'Kỳ tính giá' }));
    await user.click(screen.getByRole('option', { name: 'Quý' }));
    await user.click(screen.getByRole('button', { name: 'Lưu' }));

    expect(
      await screen.findByText('Thay đổi này sẽ tính lại giá vốn từ kỳ chưa khóa sổ. Tiếp tục?'),
    ).toBeInTheDocument();
    expect(updateMock).not.toHaveBeenCalled();

    await user.click(screen.getByRole('button', { name: 'Tiếp tục' }));

    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({ ...SETTINGS, costingPeriod: 'Quarter' }),
    );
  });
});
