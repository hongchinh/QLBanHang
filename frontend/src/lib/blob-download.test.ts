import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AxiosError, type AxiosResponse } from 'axios';

const get = vi.fn();
const post = vi.fn();
vi.mock('@/lib/api-client', () => ({
  default: { get: (...a: unknown[]) => get(...a), post: (...a: unknown[]) => post(...a) },
}));

import { requestBlob, saveBlob } from './blob-download';

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

describe('requestBlob', () => {
  it('GETs with the params, a blob response and the long timeout', async () => {
    const blob = new Blob(['x']);
    get.mockResolvedValue({ data: blob });

    const result = await requestBlob({ method: 'get', url: '/x', params: { format: 'pdf' } });

    expect(result).toBe(blob);
    expect(get).toHaveBeenCalledWith('/x', { params: { format: 'pdf' }, responseType: 'blob', timeout: 120000 });
  });

  it('POSTs the body', async () => {
    const blob = new Blob(['x']);
    post.mockResolvedValue({ data: blob });

    const result = await requestBlob({
      method: 'post',
      url: '/x',
      params: { format: 'xlsx' },
      body: { ids: ['a'] },
    });

    expect(result).toBe(blob);
    expect(post).toHaveBeenCalledWith(
      '/x',
      { ids: ['a'] },
      { params: { format: 'xlsx' }, responseType: 'blob', timeout: 120000 },
    );
  });

  it('turns a JSON error blob back into JSON', async () => {
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

    const error = await requestBlob({ method: 'get', url: '/x' }).catch((e: unknown) => e);

    expect((error as AxiosError).response?.data).toEqual(body);
  });
});

describe('saveBlob', () => {
  it('clicks a link with the file name', () => {
    saveBlob(new Blob(['x']), 'PT00001.xlsx');

    expect(downloads).toEqual(['PT00001.xlsx']);
    expect(URL.revokeObjectURL).toHaveBeenCalledWith('blob:x');
  });
});
