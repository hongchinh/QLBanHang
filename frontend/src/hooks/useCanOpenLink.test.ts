import { afterEach, describe, expect, it } from 'vitest';
import { renderHook } from '@testing-library/react';
import { useAuthStore } from '@/stores/auth-store';
import { useCanOpenLink } from './useCanOpenLink';

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}
const canOpen = (to: string) => renderHook(() => useCanOpenLink()).result.current(to);

describe('useCanOpenLink', () => {
  afterEach(() => useAuthStore.setState({ user: null }));

  it('checks the route of a plain path', () => {
    signIn(['reports.cash']);

    expect(canOpen('/reports/cash-book')).toBe(true);
    expect(canOpen('/reports/debt-overdue')).toBe(false);
  });

  it('checks only the path of a link with a query string', () => {
    signIn(['reports.cash']);
    expect(canOpen('/reports/debt-overdue?side=Receivable&at=2026-10-13')).toBe(false);
    expect(canOpen('/reports/cash-book?kind=Cash')).toBe(true);

    signIn(['reports.debt']);
    expect(canOpen('/reports/debt-overdue?side=Receivable&at=2026-10-13')).toBe(true);
    expect(canOpen('/reports/cash-book?kind=Cash')).toBe(false);
  });

  it('checks only the path of a link with a hash', () => {
    signIn(['reports.cash']);

    expect(canOpen('/reports/debt-overdue#totals')).toBe(false);
    expect(canOpen('/reports/debt-overdue?side=Payable#totals')).toBe(false);
    expect(canOpen('/reports/cash-book#totals')).toBe(true);
  });

  it('allows a route without a rule', () => {
    signIn([]);

    expect(canOpen('/settings?tab=me')).toBe(true);
  });
});
