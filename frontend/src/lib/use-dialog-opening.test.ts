import { act, renderHook } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { useDialogOpening } from './use-dialog-opening';

describe('useDialogOpening', () => {
  it('show opens with the seed and the opening is current', () => {
    const { result } = renderHook(() => useDialogOpening());
    expect(result.current.open).toBe(false);
    act(() => result.current.show('x'));
    expect(result.current.open).toBe(true);
    expect(result.current.seed).toBe('x');
    expect(result.current.isCurrent()).toBe(true);
  });

  it('closing retires the isCurrent captured before the close', () => {
    const { result } = renderHook(() => useDialogOpening());
    act(() => result.current.show('x'));
    const isCurrent = result.current.isCurrent;
    act(() => result.current.onOpenChange(false));
    expect(result.current.open).toBe(false);
    expect(isCurrent()).toBe(false);
  });

  it('an onOpenChange from an earlier opening is a no-op after show is called again', () => {
    const { result } = renderHook(() => useDialogOpening());
    act(() => result.current.show('a'));
    const stale = result.current.onOpenChange;
    act(() => result.current.show('b'));
    act(() => stale(false));
    expect(result.current.open).toBe(true);
    expect(result.current.seed).toBe('b');
  });
});
