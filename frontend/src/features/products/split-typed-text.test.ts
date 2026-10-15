import { describe, expect, it } from 'vitest';
import { splitTypedProductText } from './split-typed-text';

describe('splitTypedProductText', () => {
  it.each([
    ['TL-50', { code: 'TL-50' }],
    ['  tl50  ', { code: 'tl50' }],
    ['SP_01.A/2', { code: 'SP_01.A/2' }],
    ['Tôn lạnh 0.4', { name: 'Tôn lạnh 0.4' }],
    ['tôn', { name: 'tôn' }], // diacritics without a space
    ['ton lanh', { name: 'ton lanh' }], // a space without diacritics
    ['Đá', { name: 'Đá' }], // đ/Đ count as Vietnamese letters
    ['to\u0302n', { name: 'to\u0302n' }], // decomposed input (Unikey "Unicode tổ hợp", macOS)
    ['đa', { name: 'đa' }],
    ['', {}],
    ['   ', {}],
  ])('%j → %j', (text, expected) => {
    expect(splitTypedProductText(text)).toEqual(expected);
  });
});
