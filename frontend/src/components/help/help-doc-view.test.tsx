import { fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { HelpDocView } from './help-doc-view';
import { fixtureVideo, receiptsDoc, stockOutDoc } from './test-fixtures';

function renderView(props: Partial<React.ComponentProps<typeof HelpDocView>> = {}) {
  const onOpenRelated = vi.fn();
  render(<HelpDocView doc={stockOutDoc} related={[]} onOpenRelated={onOpenRelated} {...props} />);
  return { onOpenRelated };
}

afterEach(() => vi.restoreAllMocks());

describe('HelpDocView', () => {
  it('shows the purpose, numbered steps and inline markers without a video', () => {
    renderView();
    expect(screen.getByText('Ghi nhận hàng xuất khỏi kho.')).toBeInTheDocument();
    expect(screen.getAllByRole('listitem').filter((li) => li.closest('ol'))).toHaveLength(3);
    expect(screen.getByText('Lý do xuất').tagName).toBe('STRONG');
    const stepList = screen.getAllByRole('list').find((l) => l.tagName === 'OL') as HTMLElement;
    expect(within(stepList).getByText('Ctrl S').tagName).toBe('KBD');
    expect(screen.queryByRole('link', { name: 'Xem trên YouTube' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: /Xem bước/ })).not.toBeInTheDocument();
  });

  it('shows the video block with step chips', () => {
    renderView({ video: fixtureVideo });
    const link = screen.getByRole('link', { name: 'Xem trên YouTube' });
    expect(link).toHaveAttribute('href', 'https://www.youtube.com/watch?v=vid123');
    expect(link).toHaveAttribute('target', '_blank');
    expect(link.getAttribute('rel')).toContain('noopener');
    expect(document.querySelector('img')).toHaveAttribute('src', 'https://i.ytimg.com/vi/vid123/mqdefault.jpg');
    expect(screen.getByText('2:35')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Xem bước 1 trong video (0:08)' }).getAttribute('href')).toMatch(/&t=8s$/);
    expect(screen.getByRole('link', { name: 'Xem bước 2 trong video (0:21)' }).getAttribute('href')).toMatch(/&t=21s$/);
    expect(screen.queryByRole('link', { name: /Xem bước 3/ })).not.toBeInTheDocument();
  });

  it('replaces a broken thumbnail with a text card', () => {
    renderView({ video: fixtureVideo });
    fireEvent.error(document.querySelector('img') as HTMLImageElement);
    expect(document.querySelector('img')).toBeNull();
    expect(screen.getByText('Lập phiếu xuất bán hàng')).toBeInTheDocument();
  });

  it('shows only the offline sentence when offline', () => {
    vi.spyOn(navigator, 'onLine', 'get').mockReturnValue(false);
    renderView({ video: fixtureVideo });
    expect(screen.getByText('Cần kết nối mạng để xem video')).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Xem trên YouTube' })).not.toBeInTheDocument();
  });

  it('renders the notes and shortcuts sections only when they have items', () => {
    const { unmount } = render(<HelpDocView doc={stockOutDoc} related={[]} onOpenRelated={vi.fn()} />);
    expect(screen.getByRole('heading', { name: 'Lưu ý' })).toBeInTheDocument();
    expect(screen.getByText('Phải lưu trước khi in.')).toBeInTheDocument();
    const shortcuts = screen.getByRole('heading', { name: 'Phím tắt' }).parentElement as HTMLElement;
    expect(shortcuts).toHaveClass('hidden', 'md:block');
    unmount();
    render(<HelpDocView doc={receiptsDoc} related={[]} onOpenRelated={vi.fn()} />);
    expect(screen.queryByRole('heading', { name: 'Lưu ý' })).not.toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Phím tắt' })).not.toBeInTheDocument();
  });

  it('lists related docs and opens one on click', () => {
    const { onOpenRelated } = renderView({ related: [receiptsDoc] });
    expect(screen.getByRole('heading', { name: 'Xem thêm' })).toBeInTheDocument();
    fireEvent.click(screen.getByRole('button', { name: 'Phiếu thu' }));
    expect(onOpenRelated).toHaveBeenCalledWith('receipts.form');
  });

  it('has no "Xem thêm" heading without related docs', () => {
    renderView();
    expect(screen.queryByRole('heading', { name: 'Xem thêm' })).not.toBeInTheDocument();
  });
});
