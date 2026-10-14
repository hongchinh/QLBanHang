import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { useAuthStore } from '@/stores/auth-store';
import { StatTile } from './stat-tile';

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

function renderTile(props: Partial<React.ComponentProps<typeof StatTile>> = {}) {
  return render(
    <MemoryRouter>
      <StatTile label="Phải thu" value="3,10 tỷ" {...props} />
    </MemoryRouter>,
  );
}

const OVERDUE_LINK = '/reports/debt-overdue?side=Receivable&at=2026-10-13';

describe('StatTile', () => {
  afterEach(() => useAuthStore.setState({ user: null }));

  it('shows the label and the value in tabular numbers', () => {
    renderTile();

    expect(screen.getByText('Phải thu')).toBeInTheDocument();
    expect(screen.getByText('3,10 tỷ')).toHaveClass('tabular-nums');
  });

  it('shows "—" for a value it does not have', () => {
    renderTile({ value: null });

    expect(screen.getByText('—')).toHaveClass('tabular-nums');
  });

  describe('delta', () => {
    it('a rise is ▲ in emerald with a decimal comma', () => {
      renderTile({ delta: 12.5 });

      expect(screen.getByText('▲ 12,5%').className).toContain('emerald');
    });

    it('a fall is ▼ in rose, without the sign', () => {
      renderTile({ delta: -3.2 });

      expect(screen.getByText('▼ 3,2%').className).toContain('rose');
      expect(screen.queryByText(/-3/)).not.toBeInTheDocument();
    });

    it('no change is ▲ 0%, like the delta of the quotation dashboards', () => {
      renderTile({ delta: 0 });

      expect(screen.getByText('▲ 0%').className).toContain('emerald');
    });

    it('null is "—" in the muted colour', () => {
      renderTile({ delta: null });

      const dashes = screen.getAllByText('—');
      expect(dashes).toHaveLength(1);
      expect(dashes[0].className).toContain('muted');
      expect(screen.queryByText(/[▲▼]/)).not.toBeInTheDocument();
    });

    it('is left out when the prop is not given', () => {
      renderTile({ hint: 'so với kỳ trước' });

      expect(screen.queryByText(/[▲▼]/)).not.toBeInTheDocument();
      expect(screen.queryByText('—')).not.toBeInTheDocument();
    });
  });

  it('shows the badge next to the label', () => {
    renderTile({ label: 'Lãi gộp', badge: 'tạm tính' });

    const badge = screen.getByText('tạm tính');
    expect(badge.className).toContain('amber');
    expect(badge.parentElement).toContainElement(screen.getByText('Lãi gộp'));
  });

  it.each([
    [undefined, 'muted'],
    ['default', 'muted'],
    ['good', 'emerald'],
    ['warn', 'amber'],
    ['bad', 'rose'],
  ] as const)('hint tone %s is %s', (hintTone, colour) => {
    renderTile({ hint: 'Quá hạn 420 tr', hintTone });

    expect(screen.getByText('Quá hạn 420 tr').className).toContain(colour);
  });

  describe('link', () => {
    it('the whole tile is a link with the title when the user may open the route', () => {
      signIn(['reports.debt']);
      renderTile({ to: OVERDUE_LINK, title: 'Báo cáo mở theo chi nhánh đang làm việc (CN01)', hint: 'Quá hạn 420 tr' });

      const link = screen.getByRole('link');
      expect(link).toHaveAttribute('href', OVERDUE_LINK);
      expect(link).toHaveAttribute('title', 'Báo cáo mở theo chi nhánh đang làm việc (CN01)');
      expect(link).toHaveTextContent('Phải thu');
      expect(link).toHaveTextContent('3,10 tỷ');
      expect(link).toHaveTextContent('Quá hạn 420 tr');
    });

    it('is not a link when the user may not open the route', () => {
      signIn(['reports.cash']);
      renderTile({ to: OVERDUE_LINK, title: 'Báo cáo mở theo chi nhánh đang làm việc (CN01)' });

      expect(screen.queryByRole('link')).not.toBeInTheDocument();
      expect(screen.getByText('3,10 tỷ')).toBeInTheDocument();
      expect(document.querySelector('[title]')).toBeNull();
    });

    it('is not a link without "to"', () => {
      signIn(['reports.debt']);
      renderTile();

      expect(screen.queryByRole('link')).not.toBeInTheDocument();
    });
  });

  it('loading shows a skeleton in place of the value', () => {
    renderTile({ loading: true });

    expect(screen.queryByText('3,10 tỷ')).not.toBeInTheDocument();
    expect(document.querySelector('.animate-pulse')).not.toBeNull();
    expect(screen.getByText('Phải thu')).toBeInTheDocument();
  });

  describe('aria-busy', () => {
    it('is on the tile while it loads, not on the skeleton', () => {
      const { container } = renderTile({ loading: true });

      const tile = container.firstElementChild as HTMLElement;
      expect(tile).toHaveAttribute('aria-busy', 'true');
      expect(tile).toHaveTextContent('Phải thu');
      expect(document.querySelectorAll('[aria-busy]')).toHaveLength(1);
    });

    it('is on the link when the loading tile is one', () => {
      signIn(['reports.debt']);
      renderTile({ loading: true, to: OVERDUE_LINK });

      expect(screen.getByRole('link')).toHaveAttribute('aria-busy', 'true');
    });

    it('is absent once the tile has loaded', () => {
      renderTile({ loading: false });

      expect(document.querySelector('[aria-busy]')).toBeNull();
    });
  });

  describe('error', () => {
    it('shows "—" and a retry button in place of the hint', () => {
      const onRetry = vi.fn();
      renderTile({ error: true, hint: 'Quá hạn 420 tr', onRetry });

      expect(screen.getByText('—')).toHaveClass('tabular-nums');
      expect(screen.queryByText('3,10 tỷ')).not.toBeInTheDocument();
      expect(screen.queryByText('Quá hạn 420 tr')).not.toBeInTheDocument();

      fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
      expect(onRetry).toHaveBeenCalledTimes(1);
      // The click event is not handed on (onRetry is a query's refetch, which takes options).
      expect(onRetry).toHaveBeenCalledWith();
    });

    it('has no retry button without an error', () => {
      renderTile({ onRetry: vi.fn() });

      expect(screen.queryByRole('button', { name: 'Thử lại' })).not.toBeInTheDocument();
    });

    it('is not a link, so the retry button is not inside one', () => {
      signIn(['reports.debt']);
      renderTile({ error: true, to: OVERDUE_LINK, onRetry: vi.fn() });

      expect(screen.queryByRole('link')).not.toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Thử lại' })).toBeInTheDocument();
    });
  });
});
