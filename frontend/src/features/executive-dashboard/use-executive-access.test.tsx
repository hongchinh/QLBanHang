import { afterEach, describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useAuthStore } from '@/stores/auth-store';
import { useExecutiveAccess } from './use-executive-access';

function signIn(permissions: string[]) {
  useAuthStore.setState({
    user: { id: 'u1', username: 'u', email: 'u@x', fullName: 'U', roles: [], permissions },
  });
}
const access = () => renderHook(() => useExecutiveAccess()).result.current;
const DOMAINS = ['sales', 'cash', 'debt', 'inventory'] as const;

describe('useExecutiveAccess', () => {
  afterEach(() => useAuthStore.setState({ user: null }));

  it('opens a domain with its report permission and lists the open ones after the overview', () => {
    signIn(['dashboard.executive', 'reports.profit', 'reports.debt']);

    expect(access()).toEqual({
      sales: true,
      cash: false,
      debt: true,
      inventory: false,
      tabs: ['overview', 'sales', 'debt'],
    });
  });

  it.each([
    ['reports.profit', 'sales'],
    ['reports.cash', 'cash'],
    ['reports.debt', 'debt'],
    ['reports.inventory', 'inventory'],
  ])('%s opens %s only', (permission, domain) => {
    signIn([permission]);
    const result = access();

    for (const d of DOMAINS) expect(result[d]).toBe(d === domain);
    expect(result.tabs).toEqual(['overview', domain]);
  });

  it('keeps the order sales, cash, debt, inventory whatever the order of the permissions', () => {
    signIn(['reports.inventory', 'reports.debt', 'reports.cash', 'reports.profit']);

    expect(access().tabs).toEqual(['overview', 'sales', 'cash', 'debt', 'inventory']);
  });

  it('other permissions open no domain', () => {
    signIn(['dashboard.executive', 'reports.revenue', 'quotations.view_all', 'inventory.view_cost', 'branches.access_all']);

    expect(access()).toEqual({ sales: false, cash: false, debt: false, inventory: false, tabs: ['overview'] });
  });

  it('opens nothing without a user', () => {
    useAuthStore.setState({ user: null });

    expect(access()).toEqual({ sales: false, cash: false, debt: false, inventory: false, tabs: ['overview'] });
  });

  it('follows the permissions of the signed-in user and keeps its result between renders', () => {
    signIn(['reports.cash']);
    const { result, rerender } = renderHook(() => useExecutiveAccess());
    const first = result.current;
    expect(first.tabs).toEqual(['overview', 'cash']);

    rerender();
    expect(result.current).toBe(first);

    act(() => signIn(['reports.cash', 'reports.inventory']));
    expect(result.current.tabs).toEqual(['overview', 'cash', 'inventory']);
    expect(result.current.inventory).toBe(true);
  });
});
