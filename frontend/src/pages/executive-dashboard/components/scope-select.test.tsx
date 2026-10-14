import { beforeEach, describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Branch } from '@/features/branches/types';
import type { useExecutiveScope } from '@/features/executive-dashboard/use-executive-scope';
import { ScopeSelect } from './scope-select';

type Scope = ReturnType<typeof useExecutiveScope>;

const B1: Branch = { id: 'b1', code: 'CN01', name: 'Hà Nội' };
const B2: Branch = { id: 'b2', code: 'CN02', name: 'Hưng Yên' };
const selectCompany = vi.fn();
const selectBranch = vi.fn();
const state: { scope: Scope } = { scope: undefined as unknown as Scope };
vi.mock('@/features/executive-dashboard/use-executive-scope', () => ({
  useExecutiveScope: () => state.scope,
}));

const scope = (over: Partial<Scope> = {}): Scope => ({
  canSwitch: true,
  mode: 'company',
  branchId: undefined,
  workingBranch: B1,
  branches: [B1, B2],
  selectCompany,
  selectBranch,
  unknown: false,
  retry: vi.fn(),
  ...over,
});

describe('ScopeSelect', () => {
  beforeEach(() => {
    selectCompany.mockReset();
    selectBranch.mockReset();
    state.scope = scope();
  });

  it('renders nothing for a user who cannot switch', () => {
    state.scope = scope({ canSwitch: false, mode: 'branch', branches: [B1] });

    const { container } = render(<ScopeSelect />);

    expect(container).toBeEmptyDOMElement();
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument();
  });

  it('lists the whole company, then each branch as "code — name"', async () => {
    const user = userEvent.setup();
    render(<ScopeSelect />);

    await user.click(screen.getByRole('combobox'));

    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual([
      'Toàn công ty',
      'CN01 — Hà Nội',
      'CN02 — Hưng Yên',
    ]);
  });

  it('appends "(demo)" to a demo branch only', async () => {
    const demo: Branch = { id: 'b3', code: 'DEMO1', name: 'CN demo', isDemo: true };
    state.scope = scope({ branches: [B1, demo] });
    const user = userEvent.setup();
    render(<ScopeSelect />);

    await user.click(screen.getByRole('combobox'));

    expect((await screen.findAllByRole('option')).map((o) => o.textContent)).toEqual([
      'Toàn công ty',
      'CN01 — Hà Nội',
      'DEMO1 — CN demo (demo)',
    ]);
  });

  it('shows "Toàn công ty" in company mode and the working branch in branch mode', () => {
    const { unmount } = render(<ScopeSelect />);
    expect(screen.getByRole('combobox')).toHaveTextContent('Toàn công ty');
    unmount();

    state.scope = scope({ mode: 'branch', branchId: 'b2', workingBranch: B2 });
    render(<ScopeSelect />);
    expect(screen.getByRole('combobox')).toHaveTextContent('CN02 — Hưng Yên');
  });

  it('choosing a branch selects it', async () => {
    const user = userEvent.setup();
    render(<ScopeSelect />);

    await user.click(screen.getByRole('combobox'));
    await user.click(await screen.findByRole('option', { name: 'CN02 — Hưng Yên' }));

    expect(selectBranch).toHaveBeenCalledTimes(1);
    expect(selectBranch).toHaveBeenCalledWith('b2');
    expect(selectCompany).not.toHaveBeenCalled();
  });

  it('choosing the working branch from company mode selects it too', async () => {
    const user = userEvent.setup();
    render(<ScopeSelect />);

    await user.click(screen.getByRole('combobox'));
    await user.click(await screen.findByRole('option', { name: 'CN01 — Hà Nội' }));

    expect(selectBranch).toHaveBeenCalledWith('b1');
  });

  it('choosing "Toàn công ty" selects the company', async () => {
    state.scope = scope({ mode: 'branch', branchId: 'b1' });
    const user = userEvent.setup();
    render(<ScopeSelect />);

    await user.click(screen.getByRole('combobox'));
    await user.click(await screen.findByRole('option', { name: 'Toàn công ty' }));

    expect(selectCompany).toHaveBeenCalledTimes(1);
    expect(selectBranch).not.toHaveBeenCalled();
  });
});
