import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { BackLink, useBackTarget, useFromHere } from './back-link';

function BackTargetProbe({ fallback }: { fallback?: string }) {
  const target = useBackTarget(fallback);
  return <span data-testid="out">{target === undefined ? 'undefined' : target}</span>;
}

function FromHereProbe() {
  return <span data-testid="out">{JSON.stringify(useFromHere())}</span>;
}

function renderAt(ui: React.ReactElement, entry: { pathname: string; search?: string; state?: unknown } = { pathname: '/x' }) {
  return render(<MemoryRouter initialEntries={[entry]}>{ui}</MemoryRouter>);
}

describe('useBackTarget', () => {
  it('returns state.from when it is a local path', () => {
    renderAt(<BackTargetProbe fallback="/fallback" />, {
      pathname: '/x',
      state: { from: '/reports/debt-balance?side=Payable' },
    });
    expect(screen.getByTestId('out')).toHaveTextContent('/reports/debt-balance?side=Payable');
  });

  it('returns the fallback without state', () => {
    renderAt(<BackTargetProbe fallback="/fallback" />);
    expect(screen.getByTestId('out')).toHaveTextContent('/fallback');
  });

  it('returns undefined without state and without fallback', () => {
    renderAt(<BackTargetProbe />);
    expect(screen.getByTestId('out')).toHaveTextContent('undefined');
  });

  it.each([
    ['an external url', 'https://evil.example'],
    ['a protocol-relative url', '//evil.example'],
    ['a non-string', 42],
  ])('returns the fallback for %s', (_name, from) => {
    renderAt(<BackTargetProbe fallback="/fallback" />, { pathname: '/x', state: { from } });
    expect(screen.getByTestId('out')).toHaveTextContent('/fallback');
  });
});

describe('useFromHere', () => {
  it('returns the current path with its query', () => {
    renderAt(<FromHereProbe />, { pathname: '/reports/debt-balance', search: '?side=Payable' });
    expect(screen.getByTestId('out')).toHaveTextContent(
      JSON.stringify({ from: '/reports/debt-balance?side=Payable' }),
    );
  });
});

describe('BackLink', () => {
  it('renders a labelled link to the parent', () => {
    renderAt(<BackLink to="/settings" label="Cấu hình" />);
    const link = screen.getByRole('link', { name: 'Quay lại Cấu hình' });
    expect(link).toHaveAttribute('href', '/settings');
    expect(link).toHaveAttribute('title', 'Quay lại Cấu hình');
  });
});
