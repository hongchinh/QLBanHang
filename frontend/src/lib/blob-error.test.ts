import { describe, expect, it } from 'vitest';
import { AxiosError, type AxiosResponse } from 'axios';
import { getApiError } from '@/lib/api-client';
import { unwrapBlobError } from './blob-error';

function errorWithData(data: unknown): AxiosError {
  const response = { status: 422, statusText: 'Unprocessable', headers: {}, config: {}, data } as AxiosResponse;
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', undefined, undefined, response);
}

describe('unwrapBlobError', () => {
  it('parses a JSON blob body back to the error object', async () => {
    const body = { success: false, error: { code: 'EXPORT_NO_TARGETS', message: 'Không có đối tượng nào để xuất.' } };
    const error = errorWithData(new Blob([JSON.stringify(body)], { type: 'application/json' }));

    const result = await unwrapBlobError(error);

    expect(result).toBe(error);
    expect(getApiError(result)?.code).toBe('EXPORT_NO_TARGETS');
    expect(getApiError(result)?.message).toBe('Không có đối tượng nào để xuất.');
  });

  it('returns a non-blob error unchanged', async () => {
    const plain = new Error('boom');
    expect(await unwrapBlobError(plain)).toBe(plain);

    const jsonError = errorWithData({ success: false, error: { code: 'X', message: 'm' } });
    expect(await unwrapBlobError(jsonError)).toBe(jsonError);
    expect(getApiError(jsonError)?.code).toBe('X');
  });

  it('returns a blob that is not JSON unchanged', async () => {
    const blob = new Blob(['not json'], { type: 'application/json' });
    const error = errorWithData(blob);
    expect(await unwrapBlobError(error)).toBe(error);
    expect(error.response?.data).toBe(blob);

    const pdf = new Blob(['%PDF'], { type: 'application/pdf' });
    const pdfError = errorWithData(pdf);
    expect(await unwrapBlobError(pdfError)).toBe(pdfError);
    expect(pdfError.response?.data).toBe(pdf);
  });
});
