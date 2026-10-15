import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { act, renderHook } from '@testing-library/react';
import { useUnpickedPartnerGuard } from './use-unpicked-partner-guard';

describe('useUnpickedPartnerGuard', () => {
  let input: HTMLInputElement;

  beforeEach(() => {
    input = document.createElement('input');
    input.id = 'code';
    document.body.appendChild(input);
  });

  afterEach(() => {
    input.remove();
  });

  it('lets the form through while nothing is typed', () => {
    const { result } = renderHook(() => useUnpickedPartnerGuard('code'));

    expect(result.current.error).toBeUndefined();
    expect(result.current.assertPicked()).toBe(true);
  });

  it('blocks with a message and focuses the code input while text is unpicked', () => {
    const { result } = renderHook(() => useUnpickedPartnerGuard('code'));

    act(() => result.current.onUnpickedChange(true));
    let ok = true;
    act(() => {
      ok = result.current.assertPicked();
    });

    expect(ok).toBe(false);
    expect(result.current.error).toBe('Vui lòng chọn đối tượng từ danh sách');
    expect(document.activeElement).toBe(input);
  });

  it('clears the error when the text is gone', () => {
    const { result } = renderHook(() => useUnpickedPartnerGuard('code'));
    act(() => result.current.onUnpickedChange(true));
    act(() => {
      result.current.assertPicked();
    });

    act(() => result.current.onUnpickedChange(false));

    expect(result.current.error).toBeUndefined();
    expect(result.current.assertPicked()).toBe(true);
  });

  it('names the customer in the message when asked to', () => {
    const { result } = renderHook(() => useUnpickedPartnerGuard('code', 'khách hàng'));
    act(() => result.current.onUnpickedChange(true));

    act(() => {
      result.current.assertPicked();
    });

    expect(result.current.error).toBe('Vui lòng chọn khách hàng từ danh sách');
  });
});
