import { afterEach, describe, expect, it, vi } from 'vitest';
import { fireEvent } from '@testing-library/react';
import { canTakeFocus, currentFocus, giveFocusBack, ignoreKeyRepeat, isFocusLost } from './focus';

function add<K extends keyof HTMLElementTagNameMap>(tag: K, parent: HTMLElement = document.body) {
  return parent.appendChild(document.createElement(tag));
}

afterEach(() => {
  // Ends the guard of the last giveFocusBack, which listens on the document.
  fireEvent.keyUp(document.body, { key: 'Enter' });
  document.body.replaceChildren();
});

describe('isFocusLost / currentFocus', () => {
  it('nothing has the focus: lost, and no current element', () => {
    expect(isFocusLost()).toBe(true);
    expect(currentFocus()).toBeNull();
  });

  it('an element has the focus: not lost, and it is the current one', () => {
    const input = add('input');
    input.focus();

    expect(isFocusLost()).toBe(false);
    expect(currentFocus()).toBe(input);
  });

  it('the focus is lost again when that element leaves the page', () => {
    const input = add('input');
    input.focus();
    input.remove();

    expect(isFocusLost()).toBe(true);
    expect(currentFocus()).toBeNull();
  });
});

describe('canTakeFocus', () => {
  it('an enabled element on the page can', () => {
    expect(canTakeFocus(add('button'))).toBe(true);
  });

  it('nothing cannot', () => {
    expect(canTakeFocus(null)).toBe(false);
    expect(canTakeFocus(undefined)).toBe(false);
  });

  it('a disabled element cannot', () => {
    const button = add('button');
    button.disabled = true;

    expect(canTakeFocus(button)).toBe(false);
  });

  it('an element of a disabled fieldset cannot', () => {
    const fieldset = add('fieldset');
    fieldset.disabled = true;

    expect(canTakeFocus(add('input', fieldset))).toBe(false);
  });

  it('an element that left the page cannot', () => {
    const button = add('button');
    button.remove();

    expect(canTakeFocus(button)).toBe(false);
  });
});

describe('giveFocusBack', () => {
  // jsdom has no keyboard activation. A browser clicks a focused button on every Enter keydown that was not cancelled,
  // the auto-repeated ones of a held key included.
  function keyDown(el: HTMLElement, init: { key?: string; repeat?: boolean } = {}) {
    if (fireEvent.keyDown(el, { key: 'Enter', ...init })) fireEvent.click(el);
  }

  function saveButton() {
    const button = add('button');
    const onClick = vi.fn();
    const onKeyDown = vi.fn();
    button.addEventListener('click', onClick);
    button.addEventListener('keydown', onKeyDown);
    return { button, onClick, onKeyDown };
  }

  it('gives the focus to the element', () => {
    const { button } = saveButton();

    giveFocusBack(button);

    expect(document.activeElement).toBe(button);
  });

  it('the repeats of a key that is still held down do not act on the element', () => {
    const { button, onClick, onKeyDown } = saveButton();
    giveFocusBack(button);

    keyDown(button, { repeat: true });
    keyDown(button, { repeat: true });

    expect(onClick).not.toHaveBeenCalled();
    // Not even its own key handlers see them: a grid cell moves on Enter without waiting for a click.
    expect(onKeyDown).not.toHaveBeenCalled();
  });

  it('once the key was released, a press acts again, and so do its repeats', () => {
    const { button, onClick } = saveButton();
    giveFocusBack(button);
    keyDown(button, { repeat: true });

    fireEvent.keyUp(button, { key: 'Enter' });
    keyDown(button);
    keyDown(button, { repeat: true });

    expect(onClick).toHaveBeenCalledTimes(2);
  });

  // The watch ends with the release itself, not only with the next press.
  it('once the key was released, nothing on the element is swallowed any more', () => {
    const { button, onClick } = saveButton();
    giveFocusBack(button);

    fireEvent.keyUp(button, { key: 'Enter' });
    keyDown(button, { repeat: true });

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  // The release went unnoticed, e.g. the window lost the focus while the key was down.
  it('a new press acts even when no release was seen, and so do its repeats', () => {
    const { button, onClick } = saveButton();
    giveFocusBack(button);
    keyDown(button, { repeat: true });

    keyDown(button);
    keyDown(button, { repeat: true });

    expect(onClick).toHaveBeenCalledTimes(2);
  });

  it('a press of another key ends the wait as well', () => {
    const { button, onClick } = saveButton();
    giveFocusBack(button);

    fireEvent.keyDown(button, { key: 'Shift' });
    keyDown(button, { repeat: true });

    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('leaves the repeats on any other element alone', () => {
    const { button } = saveButton();
    const other = saveButton();
    giveFocusBack(button);
    other.button.focus();

    keyDown(other.button, { repeat: true });

    expect(other.onClick).toHaveBeenCalledTimes(1);
  });

  it('watches only the element the focus was given back to last', () => {
    const first = saveButton();
    const second = saveButton();
    giveFocusBack(first.button);
    giveFocusBack(second.button);

    keyDown(first.button, { repeat: true });
    keyDown(second.button, { repeat: true });

    expect(first.onClick).toHaveBeenCalledTimes(1);
    expect(second.onClick).not.toHaveBeenCalled();
  });
});

describe('ignoreKeyRepeat', () => {
  function keyDown(init: { key: string; repeat?: boolean }) {
    const button = add('button');
    button.addEventListener('keydown', ignoreKeyRepeat);
    return fireEvent.keyDown(button, init);
  }

  it('cancels the repeats of a held Enter or Space, so they do not press the button', () => {
    expect(keyDown({ key: 'Enter', repeat: true })).toBe(false);
    expect(keyDown({ key: ' ', repeat: true })).toBe(false);
  });

  it('leaves a single press alone', () => {
    expect(keyDown({ key: 'Enter' })).toBe(true);
    expect(keyDown({ key: ' ' })).toBe(true);
  });

  // A held Tab still walks through the buttons of the dialog.
  it('leaves the repeats of any other key alone', () => {
    expect(keyDown({ key: 'Tab', repeat: true })).toBe(true);
    expect(keyDown({ key: 'Escape', repeat: true })).toBe(true);
  });
});
