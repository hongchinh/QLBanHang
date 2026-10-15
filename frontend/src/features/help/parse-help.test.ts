import { describe, expect, it } from 'vitest';
import { narrationText, parseHelpDoc, parseInline, stepHash } from './parse-help';

const VALID = [
  '---',
  'key: stock-out.form',
  'title: Phiếu xuất kho',
  'routes: [/stock-out/new, /stock-out/:id]',
  'sources: [frontend/src/pages/stock-vouchers/**]',
  'related: [receipts.form]',
  'updated: 2026-10-15',
  '---',
  '## Dùng để',
  'Ghi nhận hàng xuất khỏi kho. Phiếu xuất bán trừ tồn kho.',
  '',
  '## Các bước',
  '1. {#reason} Chọn **Lý do xuất** và **Kho**.',
  '2. {#partner} Gõ mã khách ở **Mã đối tượng**.',
  '3. {#save} Bấm **Lưu tạm** (`Ctrl S`).',
  '',
  '## Lưu ý',
  '- Phải lưu trước khi in.',
  '',
  '## Phím tắt',
  '- `Ctrl S` Lưu',
].join('\n');

const DOC = {
  key: 'stock-out.form',
  title: 'Phiếu xuất kho',
  routes: ['/stock-out/new', '/stock-out/:id'],
  sources: ['frontend/src/pages/stock-vouchers/**'],
  related: ['receipts.form'],
  updated: '2026-10-15',
  purpose: 'Ghi nhận hàng xuất khỏi kho. Phiếu xuất bán trừ tồn kho.',
  steps: [
    { id: 'reason', text: 'Chọn **Lý do xuất** và **Kho**.' },
    { id: 'partner', text: 'Gõ mã khách ở **Mã đối tượng**.' },
    { id: 'save', text: 'Bấm **Lưu tạm** (`Ctrl S`).' },
  ],
  notes: ['Phải lưu trước khi in.'],
  shortcuts: [{ keys: 'Ctrl S', label: 'Lưu' }],
};

function expectError(raw: string, fragment: string, fileKey = 'stock-out.form') {
  const result = parseHelpDoc(raw, fileKey);
  expect(result.doc).toBeNull();
  expect(result.errors.some((e) => e.includes(fragment))).toBe(true);
  for (const e of result.errors) expect(e.startsWith(`${fileKey}: `)).toBe(true);
}

const step = (n: number, id: string) => `${n}. {#${id}} Bước ${id}.`;

describe('parseHelpDoc', () => {
  it('parses a valid file', () => {
    expect(parseHelpDoc(VALID, 'stock-out.form')).toEqual({ doc: DOC, errors: [] });
  });

  it('accepts CRLF line endings', () => {
    expect(parseHelpDoc(VALID.replace(/\n/g, '\r\n'), 'stock-out.form')).toEqual({ doc: DOC, errors: [] });
  });

  it('defaults related to an empty list', () => {
    const raw = VALID.replace('related: [receipts.form]\n', '');
    expect(parseHelpDoc(raw, 'stock-out.form')).toEqual({ doc: { ...DOC, related: [] }, errors: [] });
  });

  it('rejects a file without frontmatter', () => {
    expectError(VALID.split('\n').slice(8).join('\n'), 'frontmatter');
  });

  it('rejects a missing required field', () => {
    expectError(VALID.replace('title: Phiếu xuất kho\n', ''), '"title"');
  });

  it('rejects a malformed updated date', () => {
    expectError(VALID.replace('updated: 2026-10-15', 'updated: 15/10/2026'), 'updated');
  });

  it('rejects an unknown frontmatter field', () => {
    expectError(VALID.replace('updated: 2026-10-15', 'updated: 2026-10-15\nowner: me'), 'unknown field "owner"');
  });

  it('rejects a key that differs from the file key', () => {
    expectError(VALID, 'key', 'stock-in.form');
  });

  it('rejects a missing required section', () => {
    expectError(VALID.replace(/## Các bước\n(.*\n)*?\n/, ''), 'Các bước');
  });

  it('rejects sections out of order', () => {
    const raw = VALID.replace(
      /## Các bước\n((?:.*\n)*?)\n## Lưu ý\n(.*)\n\n/,
      '## Lưu ý\n$2\n\n## Các bước\n$1\n',
    );
    expectError(raw, 'order');
  });

  it('rejects an unknown section', () => {
    expectError(`${VALID}\n\n## Ghi chú\n- x`, 'unknown section');
  });

  it('rejects text before the first section', () => {
    expectError(VALID.replace('---\n## Dùng để', '---\nLời mở đầu\n## Dùng để'), 'before');
  });

  it('rejects a purpose with more than two sentences', () => {
    expectError(VALID.replace('Ghi nhận hàng xuất khỏi kho. Phiếu xuất bán trừ tồn kho.', 'A. B. C.'), 'sentences');
  });

  it('rejects fewer than three steps', () => {
    const raw = VALID.replace(/3\. \{#save\}.*\n/, '');
    expectError(raw, '3–7');
  });

  it('rejects more than seven steps', () => {
    const steps = Array.from({ length: 8 }, (_, i) => step(i + 1, `s${i + 1}`)).join('\n');
    const raw = VALID.replace(/1\. \{#reason\}(.*\n)*?3\. \{#save\}.*\n/, `${steps}\n`);
    expectError(raw, '3–7');
  });

  it('rejects a duplicate step id', () => {
    expectError(VALID.replace('{#partner}', '{#reason}'), 'duplicate step id');
  });

  it('rejects wrong step numbering', () => {
    expectError(VALID.replace('2. {#partner}', '3. {#partner}'), 'numbering');
  });

  it('rejects a step without an id', () => {
    expectError(VALID.replace('2. {#partner} ', '2. '), 'step id');
  });

  it('rejects a Markdown link in a note', () => {
    expectError(VALID.replace('Phải lưu trước khi in.', 'Xem [xem](http://x) trước.'), 'link');
  });

  it('rejects a malformed shortcut line', () => {
    expectError(VALID.replace('- `Ctrl S` Lưu', '- Ctrl S Lưu'), 'shortcut');
  });

  it('rejects unbalanced bold markers', () => {
    expectError(VALID.replace('Chọn **Lý do xuất** và **Kho**.', 'Chọn **Lý do xuất và Kho.'), 'unbalanced');
  });

  it('rejects unbalanced backticks', () => {
    expectError(VALID.replace('(`Ctrl S`)', '(`Ctrl S)'), 'backtick');
  });

  it('tolerates trailing whitespace on the closing frontmatter line', () => {
    expect(parseHelpDoc(VALID.replace('\n---\n## Dùng', '\n--- \n## Dùng'), 'stock-out.form')).toEqual({
      doc: DOC,
      errors: [],
    });
  });

  it('recognises headings saved in NFD form', () => {
    const raw = VALID.replace('## Dùng để', '## Du\u0300ng \u0111e\u0302\u0309');
    expect(raw).not.toBe(VALID);
    expect(parseHelpDoc(raw, 'stock-out.form')).toEqual({ doc: DOC, errors: [] });
  });

  it('reports a wrapped step line without a numbering cascade', () => {
    const raw = VALID.replace('3. {#save}', 'dòng bị ngắt\n3. {#save}');
    const result = parseHelpDoc(raw, 'stock-out.form');
    expect(result.doc).toBeNull();
    expect(result.errors.some((e) => e.includes('malformed step line'))).toBe(true);
    expect(result.errors.some((e) => e.includes('numbering'))).toBe(false);
  });

  it('rejects a key that is not lowercase kebab-case', () => {
    expectError(VALID.replace('key: stock-out.form', 'key: Stock-Out.form'), 'valid key', 'Stock-Out.form');
  });

  it('rejects a route that does not start with a slash', () => {
    expectError(VALID.replace('/stock-out/new', 'stock-out/new'), 'must start with "/"');
  });

  it('rejects an unclosed frontmatter', () => {
    expectError(VALID.replace('\n---\n## Dùng', '\n## Dùng'), 'not closed');
  });

  it('rejects empty sources', () => {
    expectError(VALID.replace('sources: [frontend/src/pages/stock-vouchers/**]', 'sources: []'), '"sources"');
  });

  it('rejects a duplicate section', () => {
    expectError(`${VALID}\n\n## Lưu ý\n- x`, 'duplicate section');
  });
});

describe('parseInline', () => {
  it('splits bold and key runs', () => {
    expect(parseInline('Chọn **Lý do xuất** rồi `Ctrl S`.')).toEqual([
      { kind: 'text', text: 'Chọn ' },
      { kind: 'strong', text: 'Lý do xuất' },
      { kind: 'text', text: ' rồi ' },
      { kind: 'kbd', text: 'Ctrl S' },
      { kind: 'text', text: '.' },
    ]);
  });

  it('keeps an unterminated marker as text', () => {
    expect(parseInline('a ** b')).toEqual([{ kind: 'text', text: 'a ** b' }]);
  });
});

describe('narrationText', () => {
  it('drops the id and the markers and collapses spaces', () => {
    expect(narrationText('{#save}  Bấm **Lưu**   (`Ctrl S`) ')).toBe('Bấm Lưu (Ctrl S)');
  });

  it('normalizes to NFC', () => {
    expect(narrationText('Lu\u0301u')).toBe('L\u00fau');
  });
});

describe('stepHash', () => {
  it('is FNV-1a 32-bit of the narration text', () => {
    expect(stepHash('a')).toBe('e40c292c');
    expect(stepHash('**a**')).toBe('e40c292c');
  });

  it('changes when the wording changes', () => {
    expect(stepHash('Bấm Lưu')).not.toBe(stepHash('Bấm Lưu.'));
  });

  it('is 8 lowercase hex characters', () => {
    expect(stepHash('x')).toMatch(/^[0-9a-f]{8}$/);
  });
});
