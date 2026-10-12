import { screen, within } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';
import { useUiStore } from '@/stores/ui-store';
import { renderSidebar } from './sidebar-test-utils';

beforeEach(() => {
  localStorage.clear();
  useUiStore.setState({ sidebarCollapsed: false });
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
