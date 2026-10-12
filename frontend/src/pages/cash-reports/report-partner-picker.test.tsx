import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ReportPartnerPicker } from './report-partner-picker';

const useDebtPartnerSearchMock = vi.fn(
  (side: string | undefined, keyword: string, options?: { includeInactive?: boolean }) => {
    void side;
    void keyword;
    void options;
    return {
      data: [{ id: 'p1', code: 'KH01', name: 'Công ty An' }],
      isLoading: false,
      isError: false,
    };
  },
);
vi.mock('@/features/debt/hooks', () => ({
  useDebtPartnerSearch: (side: string | undefined, keyword: string, options?: { includeInactive?: boolean }) =>
    useDebtPartnerSearchMock(side, keyword, options),
}));

describe('ReportPartnerPicker', () => {
  beforeEach(() => useDebtPartnerSearchMock.mockClear());

  it('calls onSelect with the picked partner', async () => {
    const onSelect = vi.fn();
    render(<ReportPartnerPicker id="x" side="Receivable" selectedLabel="" onSelect={onSelect} />);
    await userEvent.click(screen.getByRole('combobox', { name: 'Đối tượng' }));
    await userEvent.click(screen.getByRole('option', { name: /KH01/ }));
    expect(onSelect).toHaveBeenCalledWith({ id: 'p1', code: 'KH01', name: 'Công ty An' });
    expect(useDebtPartnerSearchMock).toHaveBeenCalledWith('Receivable', expect.any(String), { includeInactive: true });
  });

  it('searches every partner when side is undefined', async () => {
    render(<ReportPartnerPicker id="x" selectedLabel="" onSelect={vi.fn()} />);
    expect(useDebtPartnerSearchMock).toHaveBeenCalledWith(undefined, expect.any(String), { includeInactive: true });
  });

  it('shows the clear entry and calls onClear when given', async () => {
    const onClear = vi.fn();
    render(<ReportPartnerPicker id="x" selectedLabel="KH01 - Công ty An" onSelect={vi.fn()} onClear={onClear} />);
    await userEvent.click(screen.getByRole('button', { name: 'Tất cả đối tượng' }));
    expect(onClear).toHaveBeenCalled();
  });

  it('has no clear entry without onClear', () => {
    render(<ReportPartnerPicker id="x" selectedLabel="KH01 - Công ty An" onSelect={vi.fn()} />);
    expect(screen.queryByRole('button', { name: 'Tất cả đối tượng' })).toBeNull();
  });
});
