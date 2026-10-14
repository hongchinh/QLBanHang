import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { useExecutiveParams } from './use-executive-params';

function setup(url: string) {
  function wrapper({ children }: { children: React.ReactNode }) {
    return <MemoryRouter initialEntries={[url]}>{children}</MemoryRouter>;
  }
  const { result } = renderHook(
    () => {
      const params = useExecutiveParams();
      const location = useLocation();
      return { params, location };
    },
    { wrapper },
  );
  return {
    params: () => result.current.params,
    search: () => new URLSearchParams(result.current.location.search),
  };
}

describe('useExecutiveParams', () => {
  beforeEach(() => {
    // Tuesday 13 October 2026.
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 13, 10, 0, 0));
  });
  afterEach(() => vi.useRealTimers());

  it('defaults to the current month, the overview tab and the company scope', () => {
    const { params, search } = setup('/dashboard/executive');

    expect(params().from).toBe('2026-10-01');
    expect(params().to).toBe('2026-10-13');
    expect(params().tab).toBe('overview');
    expect(params().scopeParam).toBe('company');
    expect(search().toString()).toBe('');
  });

  it('reads the period, the tab and the scope from the URL', () => {
    const { params } = setup('/dashboard/executive?from=2026-09-01&to=2026-09-30&tab=cash&scope=branch');

    expect(params().from).toBe('2026-09-01');
    expect(params().to).toBe('2026-09-30');
    expect(params().tab).toBe('cash');
    expect(params().scopeParam).toBe('branch');
  });

  it.each(['overview', 'sales', 'cash', 'debt', 'inventory'])('tab=%s is a tab', (tab) => {
    expect(setup(`/dashboard/executive?tab=${tab}`).params().tab).toBe(tab);
  });

  it.each(['nope', 'Cash', 'cross', 'toString', ''])('tab=%s falls back to the overview', (tab) => {
    expect(setup(`/dashboard/executive?tab=${tab}`).params().tab).toBe('overview');
  });

  it.each(['company', 'Branch', 'all', ''])('scope=%s is the company scope', (scope) => {
    expect(setup(`/dashboard/executive?scope=${scope}`).params().scopeParam).toBe('company');
  });

  it('setTab writes the tab and leaves "overview" out of the URL', () => {
    const { params, search } = setup('/dashboard/executive?from=2026-09-01&to=2026-09-30&scope=branch');

    act(() => params().setTab('debt'));
    expect(params().tab).toBe('debt');
    expect(search().get('tab')).toBe('debt');
    // The other parameters stay.
    expect(search().get('from')).toBe('2026-09-01');
    expect(search().get('to')).toBe('2026-09-30');
    expect(search().get('scope')).toBe('branch');

    act(() => params().setTab('overview'));
    expect(params().tab).toBe('overview');
    expect(search().has('tab')).toBe(false);
    expect(search().get('scope')).toBe('branch');
  });

  it('setScopeParam writes "branch" and leaves "company" out of the URL', () => {
    const { params, search } = setup('/dashboard/executive?tab=cash');

    act(() => params().setScopeParam('branch'));
    expect(params().scopeParam).toBe('branch');
    expect(search().get('scope')).toBe('branch');
    expect(search().get('tab')).toBe('cash');

    act(() => params().setScopeParam('company'));
    expect(params().scopeParam).toBe('company');
    expect(search().has('scope')).toBe(false);
    expect(search().get('tab')).toBe('cash');
  });

  it('setPreset writes the range of the preset', () => {
    const { params, search } = setup('/dashboard/executive?tab=sales');

    act(() => params().setPreset('this-year'));
    expect(search().get('from')).toBe('2026-01-01');
    expect(search().get('to')).toBe('2026-10-13');
    expect(params().from).toBe('2026-01-01');
    expect(search().get('tab')).toBe('sales');

    act(() => params().setPreset('this-week'));
    expect(search().get('from')).toBe('2026-10-12');
    expect(search().get('to')).toBe('2026-10-13');
  });

  it('setRange writes the period', () => {
    const { params, search } = setup('/dashboard/executive?scope=branch');

    act(() => params().setRange('2026-08-01', '2026-08-31'));

    expect(params().from).toBe('2026-08-01');
    expect(params().to).toBe('2026-08-31');
    expect(search().get('scope')).toBe('branch');
  });
});
