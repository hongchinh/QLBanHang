import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { NumberingSettingsPage } from './numbering-settings-page';
import type { DocumentNumbering } from '@/features/inventory-settings/types';

const updateMock = vi.fn();
const NUMBERING: DocumentNumbering[] = [
  { docType: 'StockIn', prefix: 'PN', length: 5, resetPolicy: 'None', pattern: '{KH}{STT}' },
  { docType: 'StockOut', prefix: 'PX', length: 5, resetPolicy: 'None', pattern: '{KH}{STT}' },
];

vi.mock('@/features/inventory-settings/hooks', () => ({
  useNumbering: () => ({ data: NUMBERING, isLoading: false, isError: false }),
  useUpdateNumbering: () => ({ mutateAsync: updateMock, isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

describe('NumberingSettingsPage', () => {
  beforeEach(() => {
    updateMock.mockReset().mockResolvedValue(NUMBERING[0]);
  });

  it('shows live preview and submits update for StockIn', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <NumberingSettingsPage />
      </MemoryRouter>,
    );

    const card = screen.getByRole('region', { name: 'Phiếu nhập kho' });
    expect(within(card).getByText('Ví dụ: PN00001')).toBeInTheDocument();
    expect(within(card).getByText('{KH} {STT} {THANG} {NAM}', { exact: false })).toBeInTheDocument();

    const prefix = within(card).getByLabelText('Ký hiệu');
    await user.clear(prefix);
    await user.type(prefix, 'NK');
    expect(within(card).getByText('Ví dụ: NK00001')).toBeInTheDocument();

    await user.click(within(card).getByRole('button', { name: 'Lưu' }));

    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({
        docType: 'StockIn',
        data: { prefix: 'NK', length: 5, resetPolicy: 'None', pattern: '{KH}{STT}' },
      }),
    );
  });
});
