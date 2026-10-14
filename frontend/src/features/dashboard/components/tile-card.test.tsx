import { useEffect } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, within } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { ApiCallError } from '@/lib/api-client';
import { useAuthStore } from '@/stores/auth-store';
import { TileCard } from './tile-card';
import type { TileQuery } from './tile-card';

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}

const query = (over: Partial<TileQuery> = {}): TileQuery => ({
  isLoading: false,
  isError: false,
  error: null,
  data: { rows: [1] },
  refetch: vi.fn(),
  ...over,
});

function renderTile(props: Partial<React.ComponentProps<typeof TileCard>> = {}) {
  return render(
    <MemoryRouter>
      <TileCard title="Dòng tiền thu – chi" {...props}>
        <p>nội dung</p>
      </TileCard>
    </MemoryRouter>,
  );
}

describe('TileCard', () => {
  afterEach(() => useAuthStore.setState({ user: null }));

  it('shows the title and the children', () => {
    renderTile();

    expect(screen.getByRole('heading', { name: 'Dòng tiền thu – chi' })).toBeInTheDocument();
    expect(screen.getByText('nội dung')).toBeInTheDocument();
  });

  it('is a region named by its title, with its class', () => {
    renderTile({ className: 'lg:col-span-2' });

    const tile = screen.getByRole('region', { name: 'Dòng tiền thu – chi' });
    expect(within(tile).getByText('nội dung')).toBeInTheDocument();
    expect(tile).toHaveClass('lg:col-span-2');
  });

  it('shows the children of a query that has loaded', () => {
    renderTile({ query: query() });

    expect(screen.getByText('nội dung')).toBeInTheDocument();
    expect(document.querySelector('[aria-busy]')).toBeNull();
    expect(document.querySelector('.opacity-60')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Thử lại' })).not.toBeInTheDocument();
  });

  it('shows a skeleton instead of the children while the query loads, and the tile is busy', () => {
    renderTile({ query: query({ isLoading: true, data: undefined }) });

    const tile = screen.getByRole('region', { name: 'Dòng tiền thu – chi' });
    // On the tile, which has a name; the skeleton itself is an empty box.
    expect(tile).toHaveAttribute('aria-busy', 'true');
    const skeleton = tile.querySelector('.animate-pulse');
    expect(skeleton).not.toBeNull();
    expect(skeleton).not.toHaveAttribute('aria-busy');
    expect(screen.queryByText('nội dung')).not.toBeInTheDocument();
    // The title stays.
    expect(screen.getByRole('heading', { name: 'Dòng tiền thu – chi' })).toBeInTheDocument();
  });

  describe('while the data on screen belongs to the previous query key', () => {
    it('keeps the children on screen, dimmed, and the tile is busy', () => {
      renderTile({ query: query({ isPlaceholderData: true }) });

      const tile = screen.getByRole('region', { name: 'Dòng tiền thu – chi' });
      expect(tile).toHaveAttribute('aria-busy', 'true');
      expect(tile.querySelector('.animate-pulse')).toBeNull();
      expect(screen.getByText('nội dung').closest('.opacity-60')).not.toBeNull();
      // The title is not dimmed.
      expect(screen.getByRole('heading', { name: 'Dòng tiền thu – chi' }).closest('.opacity-60')).toBeNull();
    });

    it('is neither dimmed nor busy once the data of the new key is there', () => {
      const { rerender } = renderTile({ query: query({ isPlaceholderData: true }) });

      rerender(
        <MemoryRouter>
          <TileCard title="Dòng tiền thu – chi" query={query({ isPlaceholderData: false })}>
            <p>nội dung</p>
          </TileCard>
        </MemoryRouter>,
      );

      expect(screen.getByRole('region', { name: 'Dòng tiền thu – chi' })).not.toHaveAttribute('aria-busy');
      expect(document.querySelector('.opacity-60')).toBeNull();
    });

    it('does not unmount the children when the key changes and when its data arrives', () => {
      const mounted = vi.fn();
      const unmounted = vi.fn();
      function Control() {
        useEffect(() => {
          mounted();
          return unmounted;
        }, []);
        return <button type="button">Tuần</button>;
      }
      const tile = (tileQuery: TileQuery) => (
        <MemoryRouter>
          <TileCard title="Dòng tiền thu – chi" query={tileQuery}>
            <Control />
          </TileCard>
        </MemoryRouter>
      );

      const { rerender } = render(tile(query()));
      rerender(tile(query({ isPlaceholderData: true })));
      rerender(tile(query({ isPlaceholderData: false })));

      expect(screen.getByRole('button', { name: 'Tuần' })).toBeInTheDocument();
      expect(mounted).toHaveBeenCalledTimes(1);
      expect(unmounted).not.toHaveBeenCalled();
    });

    it('shows the empty text dimmed too', () => {
      renderTile({ query: query({ isPlaceholderData: true }), isEmpty: true });

      expect(screen.getByText('Chưa có dữ liệu.').closest('.opacity-60')).not.toBeNull();
    });
  });

  it('shows the message of a failed query without data, and retries', () => {
    const refetch = vi.fn();
    renderTile({
      query: query({
        isError: true,
        data: undefined,
        error: new ApiCallError({ code: 'FORBIDDEN', message: 'Bạn không có quyền xem số liệu này.' }),
        refetch,
      }),
    });

    expect(screen.getByText('Bạn không có quyền xem số liệu này.')).toBeInTheDocument();
    expect(screen.queryByText('nội dung')).not.toBeInTheDocument();
    // Announced politely: several tiles can fail at the same time.
    expect(screen.getByRole('status')).toHaveTextContent('Bạn không có quyền xem số liệu này.');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(screen.getByRole('region', { name: 'Dòng tiền thu – chi' })).not.toHaveAttribute('aria-busy');

    fireEvent.click(screen.getByRole('button', { name: 'Thử lại' }));
    expect(refetch).toHaveBeenCalledTimes(1);
    // The click event is not handed to refetch as its options.
    expect(refetch).toHaveBeenCalledWith();
  });

  it('shows a general message when the failed query carries no error', () => {
    renderTile({ query: query({ isError: true, data: undefined, error: null }) });

    expect(screen.getByText('Không tải được dữ liệu.')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Thử lại' })).toBeInTheDocument();
  });

  it('keeps the last data on screen when a background refetch fails', () => {
    renderTile({ query: query({ isError: true, error: new Error('timeout'), data: { rows: [1] } }) });

    expect(screen.getByText('nội dung')).toBeInTheDocument();
    expect(screen.queryByText('timeout')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Thử lại' })).not.toBeInTheDocument();
  });

  it('shows the empty text instead of the children', () => {
    const { unmount } = renderTile({ query: query(), isEmpty: true });
    expect(screen.getByText('Chưa có dữ liệu.')).toBeInTheDocument();
    expect(screen.queryByText('nội dung')).not.toBeInTheDocument();
    unmount();

    renderTile({ isEmpty: true, emptyText: 'Hôm nay chưa có chứng từ.' });
    expect(screen.getByText('Hôm nay chưa có chứng từ.')).toBeInTheDocument();
    expect(screen.queryByText('Chưa có dữ liệu.')).not.toBeInTheDocument();
  });

  it('loading and a failure without data come before the empty text', () => {
    const { unmount } = renderTile({ query: query({ isLoading: true, data: undefined }), isEmpty: true });
    expect(document.querySelector('[aria-busy="true"]')).not.toBeNull();
    expect(screen.queryByText('Chưa có dữ liệu.')).not.toBeInTheDocument();
    unmount();

    renderTile({ query: query({ isError: true, data: undefined }), isEmpty: true });
    expect(screen.getByRole('button', { name: 'Thử lại' })).toBeInTheDocument();
    expect(screen.queryByText('Chưa có dữ liệu.')).not.toBeInTheDocument();
  });

  describe('action', () => {
    it('is a link when the user may open the route', () => {
      signIn(['reports.cash']);
      renderTile({ action: { label: 'Sổ quỹ →', to: '/reports/cash-book', title: 'Báo cáo mở theo chi nhánh đang làm việc (CN01)' } });

      const link = screen.getByRole('link', { name: 'Sổ quỹ →' });
      expect(link).toHaveAttribute('href', '/reports/cash-book');
      expect(link).toHaveAttribute('title', 'Báo cáo mở theo chi nhánh đang làm việc (CN01)');
    });

    it('is not rendered when the user may not open the route', () => {
      signIn(['reports.debt']);
      renderTile({ action: { label: 'Sổ quỹ →', to: '/reports/cash-book' } });

      expect(screen.queryByRole('link')).not.toBeInTheDocument();
      expect(screen.queryByText('Sổ quỹ →')).not.toBeInTheDocument();
    });

    it('checks the path of a link with a query string', () => {
      const action = { label: 'Nợ quá hạn →', to: '/reports/debt-overdue?side=Receivable&at=2026-10-13' };
      signIn(['reports.cash']);
      const { unmount } = renderTile({ action });
      expect(screen.queryByText('Nợ quá hạn →')).not.toBeInTheDocument();
      unmount();

      signIn(['reports.debt']);
      renderTile({ action });
      expect(screen.getByRole('link', { name: 'Nợ quá hạn →' })).toHaveAttribute(
        'href',
        '/reports/debt-overdue?side=Receivable&at=2026-10-13',
      );
    });

    it('is a button that calls onClick', () => {
      const onClick = vi.fn();
      renderTile({ action: { label: 'Chi tiết →', onClick } });

      expect(screen.queryByRole('link')).not.toBeInTheDocument();
      fireEvent.click(screen.getByRole('button', { name: 'Chi tiết →' }));
      expect(onClick).toHaveBeenCalledTimes(1);
    });

    it('stays while the tile loads', () => {
      const onClick = vi.fn();
      renderTile({ action: { label: 'Chi tiết →', onClick }, query: query({ isLoading: true, data: undefined }) });

      expect(screen.getByRole('button', { name: 'Chi tiết →' })).toBeInTheDocument();
    });
  });
});
