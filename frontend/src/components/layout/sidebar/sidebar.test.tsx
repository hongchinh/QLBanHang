import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import { useUiStore } from '@/stores/ui-store';
import { renderSidebar } from './sidebar-test-utils';

beforeEach(() => {
  localStorage.clear();
  useUiStore.setState({ sidebarCollapsed: false, sidebarGroupOpen: {} });
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
      .toEqual(['Doanh thu', 'Kho', 'Quỹ', 'Công nợ']);
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
