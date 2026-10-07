import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { PeriodLockPage } from './period-lock-page';
import type { Branch } from '@/features/branches/types';

const setLockMock = vi.fn();
const BRANCHES: Branch[] = [
  { id: 'b1', code: 'CN01', name: 'Chi nhánh chính', lockedUntil: '2026-09-30' },
  { id: 'b2', code: 'CN02', name: 'Chi nhánh 2', lockedUntil: null },
];

vi.mock('@/features/branches/hooks', () => ({
  useBranches: () => ({ data: BRANCHES, isLoading: false, isError: false }),
  useSetPeriodLock: () => ({ mutateAsync: setLockMock, isPending: false }),
}));

vi.mock('@/lib/use-toast', () => ({ toast: vi.fn() }));

describe('PeriodLockPage', () => {
  beforeEach(() => {
    setLockMock.mockReset().mockResolvedValue({});
  });

  it('saves and clears the lock date of a branch', async () => {
    const user = userEvent.setup();
    render(
      <MemoryRouter>
        <PeriodLockPage />
      </MemoryRouter>,
    );

    expect(
      screen.getByText('Chứng từ có ngày ≤ ngày khóa sổ không được thêm, sửa, xóa, hủy.'),
    ).toBeInTheDocument();

    const row2 = screen.getByText('CN02').closest('tr') as HTMLElement;
    fireEvent.change(within(row2).getByLabelText('Khóa sổ đến'), { target: { value: '2026-10-31' } });
    await user.click(within(row2).getByRole('button', { name: 'Lưu' }));
    await waitFor(() => expect(setLockMock).toHaveBeenCalledWith({ id: 'b2', lockedUntil: '2026-10-31' }));

    const row1 = screen.getByText('CN01').closest('tr') as HTMLElement;
    expect(within(row1).getByLabelText('Khóa sổ đến')).toHaveValue('2026-09-30');
    await user.click(within(row1).getByRole('button', { name: 'Bỏ khóa' }));
    await waitFor(() => expect(setLockMock).toHaveBeenCalledWith({ id: 'b1', lockedUntil: null }));
  });
});
