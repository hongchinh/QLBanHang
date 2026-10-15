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
  { docType: 'Receipt', prefix: 'PT', length: 5, resetPolicy: 'None', pattern: '{KH}{STT}' },
  { docType: 'Payment', prefix: 'PC', length: 5, resetPolicy: 'None', pattern: '{KH}{STT}' },
  { docType: 'FundTransfer', prefix: 'CQ', length: 5, resetPolicy: 'None', pattern: '{KH}{STT}' },
  { docType: 'DebtOffset', prefix: 'BT', length: 5, resetPolicy: 'None', pattern: '{KH}{STT}' },
];

vi.mock('@/features/inventory-settings/hooks', () => ({
  useNumbering: () => ({ data: NUMBERING, isLoading: false, isError: false }),
  useUpdateNumbering: () => ({ mutateAsync: updateMock, isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p) }),
}));

const STOCK_TITLES = ['Phiếu nhập kho', 'Phiếu xuất kho'];
const CASH_TITLES = ['Phiếu thu', 'Phiếu chi', 'Chuyển quỹ', 'Bù trừ công nợ'];

function renderPage() {
  return render(
    <MemoryRouter>
      <NumberingSettingsPage />
    </MemoryRouter>,
  );
}

describe('NumberingSettingsPage', () => {
  beforeEach(() => {
    updateMock.mockReset().mockResolvedValue(NUMBERING[0]);
    granted = new Set(['inventory.settings']);
  });

  it('shows live preview and submits update for StockIn', async () => {
    const user = userEvent.setup();
    renderPage();

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

  it('shows cash document types for cash.settings only', async () => {
    granted = new Set(['cash.settings']);
    const user = userEvent.setup();
    const view = renderPage();

    for (const title of CASH_TITLES) expect(screen.getByRole('region', { name: title })).toBeInTheDocument();
    for (const title of STOCK_TITLES) expect(screen.queryByRole('region', { name: title })).not.toBeInTheDocument();

    const card = screen.getByRole('region', { name: 'Phiếu thu' });
    expect(within(card).getByText('Ví dụ: PT00001')).toBeInTheDocument();
    await user.click(within(card).getByRole('button', { name: 'Lưu' }));
    await waitFor(() =>
      expect(updateMock).toHaveBeenCalledWith({
        docType: 'Receipt',
        data: { prefix: 'PT', length: 5, resetPolicy: 'None', pattern: '{KH}{STT}' },
      }),
    );
    view.unmount();

    granted = new Set(['inventory.settings']);
    renderPage();
    for (const title of STOCK_TITLES) expect(screen.getByRole('region', { name: title })).toBeInTheDocument();
    for (const title of CASH_TITLES) expect(screen.queryByRole('region', { name: title })).not.toBeInTheDocument();
  });

  it('links back to the settings hub', () => {
    renderPage();
    expect(screen.getByRole('link', { name: 'Quay lại Cấu hình' })).toHaveAttribute('href', '/settings');
  });
});
