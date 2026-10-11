import { useState } from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { act, fireEvent, render, screen } from '@testing-library/react';
import { Outlet } from 'react-router-dom';
import { App } from './App';

vi.mock('@/hooks/useBeforeInstallPrompt', () => ({
  useBeforeInstallPrompt: () => ({ canShow: false, install: vi.fn(), dismiss: vi.fn() }),
}));
vi.mock('@/hooks/usePushNotification', () => ({
  usePushNotification: () => ({ state: 'unsupported', subscribe: vi.fn() }),
}));
vi.mock('@/routes/auth-init', () => ({ AuthInit: ({ children }: { children: React.ReactNode }) => <>{children}</> }));
vi.mock('@/routes/protected-route', () => ({
  ProtectedRoute: ({ children }: { children: React.ReactNode }) => <>{children}</>,
}));
vi.mock('@/components/layout/app-layout', () => ({ AppLayout: () => <Outlet /> }));

// A list page keeping state, like the owner filter or the column sizing of the real ones.
function StatefulList({ label }: { label: string }) {
  const [typed, setTyped] = useState('');
  return (
    <div>
      <h1>{label}</h1>
      <input aria-label="bộ lọc" value={typed} onChange={(e) => setTyped(e.target.value)} />
    </div>
  );
}
vi.mock('@/pages/cash-vouchers/cash-voucher-list-page', () => ({
  CashVoucherListPage: ({ type }: { type: string }) => <StatefulList label={`cash-${type}`} />,
}));
vi.mock('@/pages/stock-vouchers/stock-voucher-list-page', () => ({
  StockVoucherListPage: ({ type }: { type: string }) => <StatefulList label={`stock-${type}`} />,
}));

function goTo(path: string) {
  act(() => {
    window.history.pushState({}, '', path);
    window.dispatchEvent(new PopStateEvent('popstate'));
  });
}

describe('App routes', () => {
  afterEach(() => window.history.pushState({}, '', '/'));

  it.each([
    ['/receipts', '/payments', 'cash-Payment'],
    ['/stock-in', '/stock-out', 'stock-Out'],
  ])('the list of %s does not carry its state over to %s', (from, to, heading) => {
    window.history.pushState({}, '', from);
    render(<App />);
    fireEvent.change(screen.getByLabelText('bộ lọc'), { target: { value: 'đã gõ' } });
    expect(screen.getByLabelText('bộ lọc')).toHaveValue('đã gõ');

    goTo(to);

    expect(screen.getByRole('heading', { name: heading })).toBeInTheDocument();
    expect(screen.getByLabelText('bộ lọc')).toHaveValue('');
  });
});
