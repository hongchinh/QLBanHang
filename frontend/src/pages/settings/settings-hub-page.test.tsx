import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { SettingsHubPage } from './settings-hub-page';

let granted = new Set<string>();
vi.mock('@/stores/auth-store', () => ({
  useAuthStore: (selector: (s: { hasPermission: (p: string) => boolean }) => unknown) =>
    selector({ hasPermission: (p) => granted.has(p) }),
}));

// [title, permissions (any of them shows the card), href]
const CARDS: [string, string[], string][] = [
  ['Chi nhánh', ['branches.manage'], '/settings/branches'],
  ['Khóa sổ', ['period_lock.manage'], '/settings/period-lock'],
  ['Đánh số chứng từ', ['inventory.settings', 'cash.settings'], '/settings/numbering'],
  ['Cấu hình kho', ['inventory.settings'], '/settings/inventory'],
  ['Tính lại giá vốn', ['inventory.recalc_cost'], '/settings/recalc-cost'],
  ['Cấu hình tiền & công nợ', ['cash.settings'], '/settings/cash'],
];

function renderHub() {
  return render(
    <MemoryRouter>
      <SettingsHubPage />
    </MemoryRouter>,
  );
}

describe('SettingsHubPage', () => {
  it('shows each inventory card only with its permission', () => {
    granted = new Set();
    const { unmount } = renderHub();
    for (const [title] of CARDS) expect(screen.queryByText(title)).not.toBeInTheDocument();
    unmount();

    for (const [title, permissions, href] of CARDS) {
      for (const permission of permissions) {
        granted = new Set([permission]);
        const view = renderHub();
        expect(screen.getByText(title).closest('a')).toHaveAttribute('href', href);
        for (const [other, otherPermissions] of CARDS) {
          if (!otherPermissions.includes(permission)) expect(screen.queryByText(other)).not.toBeInTheDocument();
        }
        view.unmount();
      }
    }
  });

  it('shows the cash settings card', () => {
    granted = new Set(['cash.settings']);
    renderHub();

    expect(screen.getByText('Cấu hình tiền & công nợ').closest('a')).toHaveAttribute('href', '/settings/cash');
    expect(screen.getByText('Đánh số chứng từ').closest('a')).toHaveAttribute('href', '/settings/numbering');
    expect(screen.queryByText('Cấu hình kho')).not.toBeInTheDocument();
  });
});
