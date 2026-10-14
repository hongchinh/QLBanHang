import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';
import { renderSidebar } from './sidebar-test-utils';

const TEST_USER = { id: 'u1', username: 'u1', email: 'u1@x.vn', fullName: 'User 1', roles: [], permissions: [] };

beforeEach(() => {
  localStorage.clear();
  useUiStore.setState({ sidebarCollapsed: false, sidebarGroupOpen: {}, pinnedNavPaths: {} });
  useAuthStore.setState({ user: TEST_USER });
});

const link = (name: string) => screen.getByRole('link', { name });

describe('Sidebar highlight', () => {
  it('lights only the longest matching item', () => {
    renderSidebar('/settings/my-quotation-settings');
    expect(link('Cài đặt của tôi')).toHaveAttribute('aria-current', 'page');
    expect(link('Cấu hình hệ thống')).not.toHaveAttribute('aria-current');
  });

  it('lights the list item on a detail page', () => {
    renderSidebar('/quotations/123');
    expect(link('Báo giá')).toHaveAttribute('aria-current', 'page');
  });

  it('lights nothing on a page without an item', () => {
    renderSidebar('/admin/user-settings/u1');
    expect(screen.queryAllByRole('link').filter((a) => a.getAttribute('aria-current'))).toEqual([]);
  });
});

describe('Sidebar sections', () => {
  it('shows report sub-headings in order', () => {
    renderSidebar('/');
    const reports = screen.getByRole('list', { name: 'Báo cáo' });
    expect(within(reports).getAllByRole('heading', { level: 4 }).map((h) => h.textContent))
      .toEqual(['Tổng hợp', 'Doanh thu', 'Kho', 'Quỹ', 'Công nợ']);
  });
});

const header = (name: string) => screen.getByRole('button', { name });

describe('Sidebar groups', () => {
  it('opens every group except Danh mục and Hệ thống by default', () => {
    renderSidebar('/');
    for (const name of ['Bán hàng', 'Kho', 'Thu chi', 'Công nợ', 'Báo cáo']) {
      expect(header(name)).toHaveAttribute('aria-expanded', 'true');
    }
    expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'false');
    expect(header('Hệ thống')).toHaveAttribute('aria-expanded', 'false');
    expect(screen.queryByRole('link', { name: 'Hàng hóa' })).not.toBeInTheDocument();
  });

  it('toggles a group and stores the choice', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    const sales = header('Bán hàng');
    expect(sales).toHaveAttribute('aria-controls', expect.any(String));
    await user.click(sales);
    expect(sales).toHaveAttribute('aria-expanded', 'false');
    expect(useUiStore.getState().sidebarGroupOpen).toEqual({ sales: false });
    await user.click(sales);
    expect(sales).toHaveAttribute('aria-expanded', 'true');
    expect(useUiStore.getState().sidebarGroupOpen).toEqual({ sales: true });
  });

  it('uses the stored choice', () => {
    useUiStore.setState({ sidebarGroupOpen: { inventory: false, catalogs: true } });
    renderSidebar('/');
    expect(header('Kho')).toHaveAttribute('aria-expanded', 'false');
    expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'true');
  });

  it('opens the group of the current page temporarily, without touching the store', () => {
    const { navigate } = renderSidebar('/');
    navigate('/products');
    expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'true');
    expect(useUiStore.getState().sidebarGroupOpen).toEqual({});
    navigate('/receipts');
    expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'false');
  });

  it('closes a temporarily open group on click and stores it', async () => {
    const user = userEvent.setup();
    renderSidebar('/products');
    await user.click(header('Danh mục'));
    expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'false');
    expect(useUiStore.getState().sidebarGroupOpen).toEqual({ catalogs: false });
  });

  it('re-opens the active group when returning to a page whose group was closed by click', async () => {
    const user = userEvent.setup();
    const { navigate } = renderSidebar('/products');
    await user.click(header('Danh mục'));
    expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'false');
    navigate('/receipts');
    navigate('/products');
    expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'true');
    expect(link('Hàng hóa')).toBeInTheDocument();
    expect(useUiStore.getState().sidebarGroupOpen).toEqual({ catalogs: false });
  });

  it('keeps group state on a page without an item', () => {
    renderSidebar('/admin/user-settings/u1');
    expect(header('Danh mục')).toHaveAttribute('aria-expanded', 'false');
    expect(header('Bán hàng')).toHaveAttribute('aria-expanded', 'true');
  });
});

const searchBox = () => screen.getByRole('searchbox', { name: 'Tìm chức năng' });

describe('Sidebar search', () => {
  it('filters items, hides Tổng quan and highlights the match', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.type(searchBox(), 'doi tru');
    expect(screen.getByRole('link', { name: 'Đối trừ chứng từ' })).toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Phiếu thu' })).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Tổng quan' })).not.toBeInTheDocument();
    expect(screen.getByText('Đối trừ', { selector: 'mark' })).toBeInTheDocument();
    expect(screen.getByText('1 kết quả')).toBeInTheDocument();
  });

  it('opens collapsed groups that have results and shows their count', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.type(searchBox(), 'hang hoa');
    expect(screen.getByRole('link', { name: 'Hàng hóa' })).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Nhóm hàng hóa' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Danh mục' })).not.toBeInTheDocument();
    expect(screen.getByText('Danh mục').parentElement).toHaveTextContent('Danh mục2');
    expect(useUiStore.getState().sidebarGroupOpen).toEqual({});
  });

  it('shows a section match', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.type(searchBox(), 'cong no');
    expect(screen.getByRole('link', { name: 'Tuổi nợ' })).toBeInTheDocument();
  });

  it('shows an empty state', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.type(searchBox(), 'xyz');
    expect(screen.getByText('Không tìm thấy chức năng')).toBeInTheDocument();
    expect(screen.getByText('Thử từ khóa ngắn hơn')).toBeInTheDocument();
  });

  it('treats a blank query as no search', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.type(searchBox(), '   ');
    expect(screen.getByRole('link', { name: 'Tổng quan' })).toBeInTheDocument();
  });

  it('Enter opens the first result and the box clears after navigation', async () => {
    const user = userEvent.setup();
    const { pathname } = renderSidebar('/');
    await user.type(searchBox(), 'phieu{Enter}');
    expect(pathname()).toBe('/stock-in');
    expect(searchBox()).toHaveValue('');
  });

  it('clears when another route is opened', async () => {
    const user = userEvent.setup();
    const { navigate } = renderSidebar('/');
    await user.type(searchBox(), 'thu');
    navigate('/receipts');
    expect(searchBox()).toHaveValue('');
  });
});

describe('Sidebar search keyboard', () => {
  it('↓ moves focus into the results, ↑ on the first returns to the box', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.type(searchBox(), 'phieu');
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('link', { name: 'Phiếu nhập kho' })).toHaveFocus();
    await user.keyboard('{ArrowDown}');
    expect(screen.getByRole('link', { name: 'Phiếu xuất kho' })).toHaveFocus();
    await user.keyboard('{ArrowUp}{ArrowUp}');
    expect(searchBox()).toHaveFocus();
  });

  it('Esc in the results clears the box and focuses it', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.type(searchBox(), 'phieu');
    await user.keyboard('{ArrowDown}{Escape}');
    expect(searchBox()).toHaveValue('');
    expect(searchBox()).toHaveFocus();
  });

  it('Esc in the box and the × button clear it', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.type(searchBox(), 'thu{Escape}');
    expect(searchBox()).toHaveValue('');
    await user.type(searchBox(), 'thu');
    await user.click(screen.getByRole('button', { name: 'Xóa tìm kiếm' }));
    expect(searchBox()).toHaveValue('');
    expect(searchBox()).toHaveFocus();
  });
});

describe('Sidebar pin toggle', () => {
  it('pins and unpins an item for the signed-in user', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    const star = screen.getByRole('button', { name: 'Ghim Phiếu thu' });
    expect(star).toHaveAttribute('aria-pressed', 'false');
    await user.click(star);
    expect(star).toHaveAttribute('aria-pressed', 'true');
    expect(useUiStore.getState().pinnedNavPaths).toEqual({ u1: ['/receipts'] });
    await user.click(star);
    expect(useUiStore.getState().pinnedNavPaths).toEqual({ u1: [] });
  });

  it('Tổng quan has no pin button', () => {
    renderSidebar('/');
    expect(screen.queryByRole('button', { name: 'Ghim Tổng quan' })).not.toBeInTheDocument();
  });

  it('shows no pin buttons without a user', () => {
    useAuthStore.setState({ user: null });
    renderSidebar('/');
    expect(screen.queryByRole('button', { name: /^Ghim / })).not.toBeInTheDocument();
  });
});

describe('Sidebar pin chips', () => {
  const chips = () => screen.queryByRole('list', { name: 'Mục đã ghim' });

  it('shows no chip area without pins', () => {
    renderSidebar('/');
    expect(chips()).not.toBeInTheDocument();
  });

  it('shows the user pins as chips above Tổng quan', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.click(screen.getByRole('button', { name: 'Ghim Phiếu thu' }));
    expect(within(chips()!).getByRole('link', { name: 'Phiếu thu' })).toBeInTheDocument();
    await user.click(within(chips()!).getByRole('button', { name: 'Bỏ ghim Phiếu thu' }));
    expect(chips()).not.toBeInTheDocument();
  });

  it('hides pins of another user and of invisible items', () => {
    useUiStore.setState({ pinnedNavPaths: { u2: ['/receipts'], u1: ['/gone', '/quotations'] } });
    renderSidebar('/');
    expect(within(chips()!).getAllByRole('link').map((a) => a.textContent)).toEqual(['Báo giá']);
    expect(useUiStore.getState().pinnedNavPaths.u1).toEqual(['/gone', '/quotations']);
  });

  it('hides the chips while searching', async () => {
    const user = userEvent.setup();
    useUiStore.setState({ pinnedNavPaths: { u1: ['/quotations'] } });
    renderSidebar('/');
    await user.type(searchBox(), 'thu');
    expect(chips()).not.toBeInTheDocument();
  });
});

describe('Sidebar icons', () => {
  it('renders registry icons for the Kho group and its items', () => {
    const { container } = renderSidebar('/');
    expect(header('Kho')).toHaveAttribute('aria-expanded', 'true');
    expect(container.querySelector('[data-icon="stockOut"]')).not.toBeNull();
    expect(container.querySelector('[data-icon="inventory"]')).not.toBeNull();
  });
});
