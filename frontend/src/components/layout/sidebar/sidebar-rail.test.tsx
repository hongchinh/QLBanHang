import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it } from 'vitest';
import { useAuthStore } from '@/stores/auth-store';
import { useUiStore } from '@/stores/ui-store';
import { renderSidebar } from './sidebar-test-utils';

const TEST_USER = { id: 'u1', username: 'u1', email: 'u1@x.vn', fullName: 'User 1', roles: [], permissions: [] };

beforeEach(() => {
  localStorage.clear();
  useUiStore.setState({ sidebarCollapsed: true, sidebarGroupOpen: {}, pinnedNavPaths: {} });
  useAuthStore.setState({ user: TEST_USER });
});

const groupButton = (name: string) => screen.getByRole('button', { name });

describe('SidebarRail', () => {
  it('shows one block per group and no search box', () => {
    renderSidebar('/');
    for (const name of ['Bán hàng', 'Kho', 'Thu chi', 'Công nợ', 'Báo cáo', 'Danh mục', 'Hệ thống']) {
      expect(groupButton(name)).toBeInTheDocument();
    }
    expect(screen.getByRole('link', { name: 'Tổng quan' })).toBeInTheDocument();
    expect(screen.queryByRole('searchbox')).not.toBeInTheDocument();
    expect(screen.queryByRole('link', { name: 'Phiếu thu' })).not.toBeInTheDocument();
  });

  it('click opens the group flyout with its items and sections', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.click(groupButton('Báo cáo'));
    const flyout = screen.getByRole('dialog', { name: 'Báo cáo' });
    expect(groupButton('Báo cáo')).toHaveAttribute('aria-expanded', 'true');
    expect(within(flyout).getByRole('link', { name: 'Tuổi nợ' })).toBeInTheDocument();
    expect(within(flyout).getAllByRole('heading', { level: 4 }).map((h) => h.textContent))
      .toEqual(['Doanh thu', 'Kho', 'Quỹ', 'Công nợ']);
    expect(within(flyout).getByRole('button', { name: 'Ghim Tuổi nợ' })).toBeInTheDocument();
  });

  it('Esc closes the flyout and returns focus to the icon', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.click(groupButton('Kho'));
    await user.keyboard('{Escape}');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(groupButton('Kho')).toHaveFocus();
  });

  it('opens from the keyboard', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    groupButton('Thu chi').focus();
    await user.keyboard('{Enter}');
    expect(within(screen.getByRole('dialog')).getByRole('link', { name: 'Phiếu thu' })).toBeInTheDocument();
  });

  it('focuses the first link on open, so Enter navigates instead of pinning', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    groupButton('Kho').focus();
    await user.keyboard('{Enter}');
    const first = within(screen.getByRole('dialog')).getAllByRole('link')[0];
    expect(first).toHaveFocus();
  });

  it('keeps one flyout open at a time', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.click(groupButton('Kho'));
    await user.click(groupButton('Thu chi'));
    expect(screen.getAllByRole('dialog')).toHaveLength(1);
    expect(within(screen.getByRole('dialog')).getByRole('link', { name: 'Phiếu thu' })).toBeInTheDocument();
  });

  it('choosing an item navigates and closes the flyout', async () => {
    const user = userEvent.setup();
    const { pathname } = renderSidebar('/');
    await user.click(groupButton('Danh mục'));
    await user.click(within(screen.getByRole('dialog')).getByRole('link', { name: 'Hàng hóa' }));
    expect(pathname()).toBe('/products');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('choosing the item of the current page closes the flyout and returns focus to the icon', async () => {
    const user = userEvent.setup();
    renderSidebar('/products');
    await user.click(groupButton('Danh mục'));
    await user.click(within(screen.getByRole('dialog')).getByRole('link', { name: 'Hàng hóa' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(groupButton('Danh mục')).toHaveFocus();
  });

  it('closes on a route change', async () => {
    const user = userEvent.setup();
    const { navigate } = renderSidebar('/');
    await user.click(groupButton('Kho'));
    navigate('/receipts');
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('marks the group of the current page', () => {
    renderSidebar('/products');
    expect(groupButton('Danh mục')).toHaveAttribute('data-active', 'true');
    expect(groupButton('Kho')).not.toHaveAttribute('data-active');
  });
});

describe('SidebarRail search and pins', () => {
  it('🔍 expands the sidebar, persists it and focuses the search box', async () => {
    const user = userEvent.setup();
    renderSidebar('/');
    await user.click(screen.getByRole('button', { name: 'Tìm chức năng' }));
    expect(useUiStore.getState().sidebarCollapsed).toBe(false);
    expect(screen.getByRole('searchbox', { name: 'Tìm chức năng' })).toHaveFocus();
  });

  it('★ is hidden without pins', () => {
    renderSidebar('/');
    expect(screen.queryByRole('button', { name: 'Mục đã ghim' })).not.toBeInTheDocument();
  });

  it('★ opens the pinned items flyout', async () => {
    const user = userEvent.setup();
    useUiStore.setState({ pinnedNavPaths: { u1: ['/reports/debt-aging', '/receipts'] } });
    renderSidebar('/');
    await user.click(screen.getByRole('button', { name: 'Mục đã ghim' }));
    const flyout = screen.getByRole('dialog');
    expect(within(flyout).getAllByRole('link').map((a) => a.textContent)).toEqual(['Tuổi nợ', 'Phiếu thu']);
    expect(within(flyout).queryByRole('heading', { level: 4 })).not.toBeInTheDocument();
  });

  it('unpinning inside the ★ flyout moves focus to the neighbouring pin', async () => {
    const user = userEvent.setup();
    useUiStore.setState({ pinnedNavPaths: { u1: ['/receipts', '/reports/debt-aging'] } });
    renderSidebar('/');
    await user.click(screen.getByRole('button', { name: 'Mục đã ghim' }));
    const flyout = screen.getByRole('dialog');
    within(flyout).getByRole('button', { name: 'Ghim Phiếu thu' }).focus();
    await user.keyboard('{Enter}');
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(within(flyout).queryByRole('link', { name: 'Phiếu thu' })).not.toBeInTheDocument();
    expect(within(flyout).getByRole('link', { name: 'Tuổi nợ' })).toHaveFocus();
  });

  it('unpinning the last pin closes the ★ flyout and focuses the search button', async () => {
    const user = userEvent.setup();
    useUiStore.setState({ pinnedNavPaths: { u1: ['/receipts'] } });
    renderSidebar('/');
    await user.click(screen.getByRole('button', { name: 'Mục đã ghim' }));
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Ghim Phiếu thu' }));
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Mục đã ghim' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Tìm chức năng' })).toHaveFocus();
  });
});
