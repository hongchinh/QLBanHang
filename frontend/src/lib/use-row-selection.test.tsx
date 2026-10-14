import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { useRowSelection } from './use-row-selection';

function setup(ids: string[] = ['a', 'b', 'c'], key = 'k1') {
  return renderHook((p: { ids: string[]; key: string }) => useRowSelection(p.ids, p.key), {
    initialProps: { ids, key },
  });
}

describe('useRowSelection', () => {
  it('toggles one id and keeps page order', () => {
    const { result } = setup();
    act(() => result.current.toggle('c', true));
    act(() => result.current.toggle('a', true));
    expect(result.current.selectedIds).toEqual(['a', 'c']);
    expect(result.current.isSelected('a')).toBe(true);
    act(() => result.current.toggle('a', false));
    expect(result.current.selectedIds).toEqual(['c']);
    expect(result.current.isSelected('a')).toBe(false);
  });

  it('toggleAll selects and clears the page', () => {
    const { result } = setup();
    act(() => result.current.toggleAll(true));
    expect(result.current.selectedIds).toEqual(['a', 'b', 'c']);
    expect(result.current.allSelected).toBe(true);
    act(() => result.current.toggle('b', false));
    expect(result.current.allSelected).toBe(false);
    expect(result.current.someSelected).toBe(true);
    act(() => result.current.toggleAll(false));
    expect(result.current.selectedIds).toEqual([]);
    expect(result.current.someSelected).toBe(false);
  });

  it('an empty page is not allSelected', () => {
    const { result } = setup([]);
    expect(result.current.allSelected).toBe(false);
    expect(result.current.someSelected).toBe(false);
  });

  it('clear empties the selection', () => {
    const { result } = setup();
    act(() => result.current.toggleAll(true));
    act(() => result.current.clear());
    expect(result.current.selectedIds).toEqual([]);
  });

  it('resets when the key changes', () => {
    const { result, rerender } = setup();
    act(() => result.current.toggleAll(true));
    rerender({ ids: ['a', 'b', 'c'], key: 'k2' });
    expect(result.current.selectedIds).toEqual([]);
  });

  it('leaves out selected ids that left the page', () => {
    const { result, rerender } = setup();
    act(() => result.current.toggleAll(true));
    rerender({ ids: ['a', 'c'], key: 'k1' });
    expect(result.current.selectedIds).toEqual(['a', 'c']);
    expect(result.current.allSelected).toBe(true);
  });
});
