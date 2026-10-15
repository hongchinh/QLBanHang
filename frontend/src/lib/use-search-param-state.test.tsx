import { fireEvent, render, screen } from '@testing-library/react';
import { MemoryRouter, useLocation } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { useSearchParamNumber, useSearchParamString } from './use-search-param-state';

function Probe() {
  const [a, setA] = useSearchParamString('a');
  const [page, setPage] = useSearchParamNumber('page');
  const location = useLocation();
  return (
    <div>
      <button onClick={() => setA('2')}>set-a</button>
      <button onClick={() => setPage(3)}>set-page</button>
      <span data-testid="value">{a}</span>
      <span data-testid="page">{page}</span>
      <span data-testid="search">{location.search}</span>
      <span data-testid="state">{JSON.stringify(location.state)}</span>
    </div>
  );
}

function renderProbe() {
  return render(
    <MemoryRouter initialEntries={[{ pathname: '/x', search: '?a=1', state: { from: '/p?q=1' } }]}>
      <Probe />
    </MemoryRouter>,
  );
}

describe('search param hooks', () => {
  it('useSearchParamString updates the query and keeps the history state', () => {
    renderProbe();
    fireEvent.click(screen.getByText('set-a'));
    expect(screen.getByTestId('search')).toHaveTextContent('?a=2');
    expect(screen.getByTestId('state')).toHaveTextContent('{"from":"/p?q=1"}');
  });

  it('useSearchParamNumber updates the query and keeps the history state', () => {
    renderProbe();
    fireEvent.click(screen.getByText('set-page'));
    expect(screen.getByTestId('search')).toHaveTextContent('?a=1&page=3');
    expect(screen.getByTestId('state')).toHaveTextContent('{"from":"/p?q=1"}');
  });
});
