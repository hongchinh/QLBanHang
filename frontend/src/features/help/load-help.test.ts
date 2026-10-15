import { renderHook, waitFor } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { parseHelpFiles } from './content-index';
import { loadHelpIndex, resetHelpIndexCacheForTests, useHelpIndex, useHelpLoadFailed } from './load-help';

const VALID = [
  '---', 'key: stock-out.form', 'title: Phiếu xuất kho', 'routes: [/stock-out/new, /stock-out/:id]',
  'sources: [frontend/src/pages/stock-vouchers/**]', 'related: [receipts.form]', 'updated: 2026-10-15', '---',
  '## Dùng để', 'Ghi nhận hàng xuất khỏi kho. Phiếu xuất bán trừ tồn kho.', '',
  '## Các bước',
  '1. {#reason} Chọn **Lý do xuất** và **Kho**.',
  '2. {#partner} Gõ mã khách ở **Mã đối tượng**.',
  '3. {#save} Bấm **Lưu tạm** (`Ctrl S`).', '',
  '## Lưu ý', '- Phải lưu trước khi in.', '',
  '## Phím tắt', '- `Ctrl S` Lưu',
].join('\n');

describe('parseHelpFiles', () => {
  afterEach(() => vi.restoreAllMocks());

  it('keeps valid files and logs the invalid ones', () => {
    const error = vi.spyOn(console, 'error').mockImplementation(() => {});
    const docs = parseHelpFiles({ './content/stock-out.form.md': VALID, './content/bad.md': 'nope' });
    expect(docs).toHaveLength(1);
    expect(docs[0].key).toBe('stock-out.form');
    expect(error).toHaveBeenCalledTimes(1);
    expect(String(error.mock.calls[0][0])).toContain('bad');
  });
});

describe('loadHelpIndex', () => {
  beforeEach(() => resetHelpIndexCacheForTests());

  it('resolves an index of the real help files and caches the promise', async () => {
    const first = loadHelpIndex();
    const index = await first;
    expect(index.resolve('/stock-out/new')?.key).toBe('stock-out.form');
    expect(loadHelpIndex()).toBe(first);
  });

  it('useHelpIndex is null first, then the index', async () => {
    const { result } = renderHook(() => useHelpIndex());
    expect(result.current).toBeNull();
    await waitFor(() => expect(result.current).not.toBeNull());
    expect(renderHook(() => useHelpLoadFailed()).result.current).toBe(false);
  });
});
