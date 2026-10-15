import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { useUiStore } from '@/stores/ui-store';
import { useHelpShortcut } from './use-help-shortcut';

function press(init: KeyboardEventInit): KeyboardEvent {
  const e = new KeyboardEvent('keydown', { cancelable: true, bubbles: true, ...init });
  act(() => {
    window.dispatchEvent(e);
  });
  return e;
}

describe('useHelpShortcut', () => {
  const added: HTMLElement[] = [];
  function append(html: string) {
    const wrapper = document.createElement('div');
    wrapper.innerHTML = html;
    document.body.appendChild(wrapper);
    added.push(wrapper);
  }

  beforeEach(() => {
    useUiStore.setState({ helpOpen: false });
  });
  afterEach(() => {
    added.splice(0).forEach((el) => el.remove());
  });

  it('toggles the panel on F1 and prevents the browser help', () => {
    renderHook(() => useHelpShortcut());
    const e = press({ key: 'F1' });
    expect(useUiStore.getState().helpOpen).toBe(true);
    expect(e.defaultPrevented).toBe(true);
    press({ key: 'F1' });
    expect(useUiStore.getState().helpOpen).toBe(false);
  });

  it.each([{ ctrlKey: true }, { altKey: true }, { shiftKey: true }, { metaKey: true }, { repeat: true }])(
    'ignores F1 with %o',
    (init) => {
      renderHook(() => useHelpShortcut());
      press({ key: 'F1', ...init });
      expect(useUiStore.getState().helpOpen).toBe(false);
    },
  );

  it.each(['dialog', 'alertdialog'])('ignores F1 while a modal %s is open', (role) => {
    renderHook(() => useHelpShortcut());
    append(`<div role="${role}"></div>`);
    press({ key: 'F1' });
    expect(useUiStore.getState().helpOpen).toBe(false);
  });

  it('still toggles while only a popover is open', () => {
    renderHook(() => useHelpShortcut());
    append('<div data-radix-popper-content-wrapper><div role="dialog"></div></div>');
    press({ key: 'F1' });
    expect(useUiStore.getState().helpOpen).toBe(true);
  });

  it('still toggles when a focused element stops the keydown from bubbling', () => {
    renderHook(() => useHelpShortcut());
    const child = document.createElement('div');
    child.addEventListener('keydown', (e) => e.stopPropagation());
    document.body.appendChild(child);
    added.push(child);
    act(() => {
      child.dispatchEvent(new KeyboardEvent('keydown', { key: 'F1', bubbles: true, cancelable: true }));
    });
    expect(useUiStore.getState().helpOpen).toBe(true);
  });

  it('ignores other keys and stops after unmount', () => {
    const { unmount } = renderHook(() => useHelpShortcut());
    press({ key: 'F2' });
    expect(useUiStore.getState().helpOpen).toBe(false);
    unmount();
    press({ key: 'F1' });
    expect(useUiStore.getState().helpOpen).toBe(false);
  });
});
