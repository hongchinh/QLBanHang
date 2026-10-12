import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { ActivityHistory, type DocumentActivity } from './activity-history';

const baseProps = { activities: [], isLoading: false, isError: false, errorMessage: '', onRetry: () => {} };

function activity(action: DocumentActivity['action'], id = action): DocumentActivity {
  return { id, action, occurredAt: '2026-10-05T03:00:00Z', description: `desc ${action}`, actorName: 'An' };
}

describe('ActivityHistory', () => {
  it('shows the loading state', () => {
    render(<ActivityHistory {...baseProps} isLoading />);
    expect(screen.getByText('Đang tải lịch sử...')).toBeInTheDocument();
  });

  it('shows the error with a retry button', async () => {
    const onRetry = vi.fn();
    render(<ActivityHistory {...baseProps} isError errorMessage="Lỗi mạng" onRetry={onRetry} />);
    expect(screen.getByText('Lỗi mạng')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(onRetry).toHaveBeenCalledTimes(1);
  });

  it('shows the empty state', () => {
    render(<ActivityHistory {...baseProps} />);
    expect(screen.getByText('Chưa có lịch sử.')).toBeInTheDocument();
  });

  it('lists the rows with labels, description and actor', () => {
    render(
      <ActivityHistory
        {...baseProps}
        activities={[activity('Created'), activity('SettlementRemoved'), activity('AutoVoucherChanged')]}
      />,
    );
    expect(screen.getByText('Tạo phiếu')).toBeInTheDocument();
    expect(screen.getByText('Bỏ đối trừ')).toBeInTheDocument();
    expect(screen.getByText('Phiếu thu/chi tự động')).toBeInTheDocument();
    expect(screen.getByText('desc Created')).toBeInTheDocument();
    expect(screen.getAllByText('An')).toHaveLength(3);
  });

  it('falls back to an unknown actor', () => {
    render(<ActivityHistory {...baseProps} activities={[{ ...activity('Updated'), actorName: null }]} />);
    expect(screen.getByText('Người dùng không xác định')).toBeInTheDocument();
  });
});
