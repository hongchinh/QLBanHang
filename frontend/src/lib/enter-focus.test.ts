import { afterEach, describe, expect, it } from 'vitest';
import { focusById, focusNextInOrder } from './enter-focus';

function mount(html: string) { document.body.innerHTML = html; }
afterEach(() => { document.body.innerHTML = ''; });

describe('focusNextInOrder', () => {
  it('moves to the next enabled field, skipping disabled and missing ones', () => {
    mount('<input id="a"><input id="b" disabled><input id="c">');
    expect(focusNextInOrder(['a', 'b', 'x', 'c'], 'a', 1)).toBe('moved');
    expect(document.activeElement?.id).toBe('c');
  });
  it('moves backwards', () => {
    mount('<input id="a"><input id="b" disabled><input id="c">');
    expect(focusNextInOrder(['a', 'b', 'c'], 'c', -1)).toBe('moved');
    expect(document.activeElement?.id).toBe('a');
  });
  it('reports the end of the list without moving', () => {
    mount('<input id="a"><input id="b">');
    (document.getElementById('b') as HTMLElement).focus();
    expect(focusNextInOrder(['a', 'b'], 'b', 1)).toBe('end');
    expect(focusNextInOrder(['a', 'b'], 'a', -1)).toBe('end');
    expect(document.activeElement?.id).toBe('b');
  });
  it('reports an id that is not in the list', () => {
    mount('<input id="a">');
    expect(focusNextInOrder(['a'], 'zzz', 1)).toBe('unknown');
  });
});

describe('focusById', () => {
  it('selects the text', () => {
    mount('<input id="a" value="abc">');
    expect(focusById('a', 'select')).toBe(true);
    const el = document.getElementById('a') as HTMLInputElement;
    expect([el.selectionStart, el.selectionEnd]).toEqual([0, 3]);
  });
  it('puts the caret at the end', () => {
    mount('<input id="a" value="abc">');
    focusById('a', 'end');
    const el = document.getElementById('a') as HTMLInputElement;
    expect([el.selectionStart, el.selectionEnd]).toEqual([3, 3]);
  });
  it('only focuses by default', () => {
    mount('<input id="a" value="abc">');
    expect(focusById('a')).toBe(true);
    expect(document.activeElement?.id).toBe('a');
  });
  it('does nothing for a missing or disabled element', () => {
    mount('<input id="a" disabled>');
    expect(focusById('a')).toBe(false);
    expect(focusById('nope')).toBe(false);
  });
  it('does not throw for inputs without a selection range', () => {
    mount('<input id="d" type="datetime-local">');
    expect(() => focusById('d', 'end')).not.toThrow();
  });
});
