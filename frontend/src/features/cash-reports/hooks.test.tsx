import { beforeEach, describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { cashReportsApi } from './api';
import { useDebtBySalesperson, useDebtReconciliation } from './hooks';

vi.mock('./api', () => ({
  cashReportsApi: {
    debtBySalesperson: vi.fn(),
    debtReconciliation: vi.fn(),
  },
}));

function setup() {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function wrapper({ children }: { children: React.ReactNode }) {
    return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
  }
  return { wrapper };
}

beforeEach(() => {
  vi.mocked(cashReportsApi.debtBySalesperson).mockReset().mockResolvedValue({} as never);
  vi.mocked(cashReportsApi.debtReconciliation).mockReset().mockResolvedValue({} as never);
});

describe('useDebtReconciliation', () => {
  it('does not call the API while partnerId is empty', async () => {
    const { wrapper } = setup();
    renderHook(() => useDebtReconciliation({ partnerId: '', from: '2026-10-01', to: '2026-10-31' }), { wrapper });

    await new Promise((r) => setTimeout(r, 20));
    expect(cashReportsApi.debtReconciliation).not.toHaveBeenCalled();
  });

  it('calls the API with the params once partnerId is set', async () => {
    const { wrapper } = setup();
    renderHook(() => useDebtReconciliation({ partnerId: 'p1', from: '2026-10-01', to: '2026-10-31' }), { wrapper });

    await waitFor(() => expect(cashReportsApi.debtReconciliation).toHaveBeenCalled());
    expect(cashReportsApi.debtReconciliation).toHaveBeenCalledWith({
      partnerId: 'p1',
      from: '2026-10-01',
      to: '2026-10-31',
    });
  });
});

describe('useDebtBySalesperson', () => {
  const params = { side: 'Receivable' as const, from: '2026-10-01', to: '2026-10-31' };

  it('calls the API with its params', async () => {
    const { wrapper } = setup();
    renderHook(() => useDebtBySalesperson(params), { wrapper });

    await waitFor(() => expect(cashReportsApi.debtBySalesperson).toHaveBeenCalled());
    expect(cashReportsApi.debtBySalesperson).toHaveBeenCalledWith(params);
  });

  it('does not call the API when enabled is false', async () => {
    const { wrapper } = setup();
    renderHook(() => useDebtBySalesperson(params, { enabled: false }), { wrapper });

    await new Promise((r) => setTimeout(r, 20));
    expect(cashReportsApi.debtBySalesperson).not.toHaveBeenCalled();
  });
});
