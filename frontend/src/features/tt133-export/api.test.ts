import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AxiosError, type AxiosResponse } from 'axios';

const get = vi.fn();
const post = vi.fn();
vi.mock('@/lib/api-client', () => ({
  default: { get: (...a: unknown[]) => get(...a), post: (...a: unknown[]) => post(...a) },
}));

import { downloadTt133, TT133_POST_REPORTS } from './api';

let downloads: string[] = [];

beforeEach(() => {
  get.mockReset();
  post.mockReset();
  downloads = [];
  URL.createObjectURL = vi.fn(() => 'blob:x');
  URL.revokeObjectURL = vi.fn();
  vi.spyOn(HTMLAnchorElement.prototype, 'click').mockImplementation(function (this: HTMLAnchorElement) {
    downloads.push(this.download);
  });
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('downloadTt133', () => {
  it('GETs a single-document report and clicks an anchor with the file name', async () => {
    get.mockResolvedValue({ data: new Blob(['x']) });

    await downloadTt133(
      'cash-book',
      'pdf',
      { kind: 'Cash', from: '2026-10-01', to: '2026-10-31' },
      'S04a-DNN_20261001_20261031',
    );

    expect(get).toHaveBeenCalledWith('/reports/cash-book/export', {
      params: { kind: 'Cash', from: '2026-10-01', to: '2026-10-31', format: 'pdf' },
      responseType: 'blob',
      timeout: 120000,
    });
    expect(post).not.toHaveBeenCalled();
    expect(downloads).toEqual(['S04a-DNN_20261001_20261031.pdf']);
  });

  it.each(['debt-ledger', 'stock-card', 'stock-detail'] as const)(
    'POSTs %s with the request in the body',
    async (report) => {
      expect(TT133_POST_REPORTS).toContain(report);
      post.mockResolvedValue({ data: new Blob(['x']) });
      const params = { side: 'Receivable', from: '2026-10-01', to: '2026-10-31', partnerIds: ['a', 'b'] };

      await downloadTt133(report, 'xlsx', params, 'F_1_2');

      expect(post).toHaveBeenCalledWith(`/reports/${report}/export`, params, {
        params: { format: 'xlsx' },
        responseType: 'blob',
        timeout: 120000,
      });
      expect(get).not.toHaveBeenCalled();
      expect(downloads).toEqual(['F_1_2.xlsx']);
    },
  );

  it('rethrows a blob error with the body parsed', async () => {
    const body = {
      success: false,
      error: { code: 'PDF_EXPORT_FAILED', message: 'Xuất PDF thất bại, hãy dùng Excel.' },
    };
    const response = {
      status: 503,
      statusText: '',
      headers: {},
      config: {},
      data: new Blob([JSON.stringify(body)], { type: 'application/json' }),
    } as AxiosResponse;
    get.mockRejectedValue(new AxiosError('fail', 'ERR_BAD_RESPONSE', undefined, undefined, response));

    const error = await downloadTt133('cash-book', 'pdf', { kind: 'Bank' }, 'F').catch((e: unknown) => e);

    expect((error as AxiosError).response?.data).toEqual(body);
    expect(downloads).toEqual([]);
  });
});
