import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from 'axios';
import { useBranchStore } from '@/stores/branch-store';
import api, {
  ApiCallError,
  apiGet,
  formatApiErrorDetails,
  getApiError,
  getErrorMessage,
} from './api-client';

describe('ApiCallError', () => {
  it('preserves code, message, details', () => {
    const err = new ApiCallError({
      code: 'VALIDATION',
      message: 'Invalid input',
      details: { name: ['Required'] },
    });
    expect(err.code).toBe('VALIDATION');
    expect(err.message).toBe('Invalid input');
    expect(err.details).toEqual({ name: ['Required'] });
    expect(err).toBeInstanceOf(Error);
  });
});

describe('getErrorMessage', () => {
  it('returns ApiCallError message', () => {
    const err = new ApiCallError({ code: 'X', message: 'API down' });
    expect(getErrorMessage(err)).toBe('API down');
  });

  it('returns native Error message', () => {
    expect(getErrorMessage(new Error('boom'))).toBe('boom');
  });

  it('returns fallback for unknown shape', () => {
    expect(getErrorMessage({ weird: true })).toBe('Đã xảy ra lỗi không mong muốn.');
  });

  it('returns fallback for null', () => {
    expect(getErrorMessage(null)).toBe('Đã xảy ra lỗi không mong muốn.');
  });
});

function axiosErrorWith(status: number, data: unknown): AxiosError {
  const config = { headers: new AxiosHeaders(), url: '/x' } as InternalAxiosRequestConfig;
  return new AxiosError('Request failed', 'ERR_BAD_REQUEST', config, null, {
    status,
    statusText: '',
    headers: {},
    config,
    data,
  });
}

describe('X-Branch-Id header', () => {
  const originalAdapter = api.defaults.adapter;
  let captured: InternalAxiosRequestConfig | undefined;

  beforeEach(() => {
    captured = undefined;
    useBranchStore.getState().clear();
    api.defaults.adapter = async (config) => {
      captured = config;
      return {
        data: { success: true, data: null, timestamp: '' },
        status: 200,
        statusText: 'OK',
        headers: {},
        config,
      };
    };
  });

  afterEach(() => {
    api.defaults.adapter = originalAdapter;
    useBranchStore.getState().clear();
  });

  it('adds X-Branch-Id when a working branch is set', async () => {
    useBranchStore.setState({ workingBranchId: 'branch-2' });

    await apiGet('/x');

    expect(captured?.headers['X-Branch-Id']).toBe('branch-2');
  });

  it('omits X-Branch-Id when none', async () => {
    await apiGet('/x');

    expect(captured).toBeDefined();
    expect(captured?.headers['X-Branch-Id']).toBeUndefined();
  });
});

describe('getApiError', () => {
  it('getApiError reads code and details from an axios 422 response', () => {
    const err = axiosErrorWith(422, {
      success: false,
      error: {
        code: 'NEGATIVE_STOCK_WARNING',
        message: 'Không đủ tồn kho',
        details: { 'SP01@KHO01': ['SP01 tại KHO01 thiếu 5'] },
      },
      timestamp: '',
    });

    expect(getApiError(err)).toEqual({
      code: 'NEGATIVE_STOCK_WARNING',
      message: 'Không đủ tồn kho',
      details: { 'SP01@KHO01': ['SP01 tại KHO01 thiếu 5'] },
      status: 422,
    });
  });

  it('getApiError returns ApiCallError data', () => {
    const err = new ApiCallError({
      code: 'VALIDATION',
      message: 'Lý do là bắt buộc',
      details: { reasonId: ['Lý do là bắt buộc'] },
    });

    expect(getApiError(err)).toEqual({
      code: 'VALIDATION',
      message: 'Lý do là bắt buộc',
      details: { reasonId: ['Lý do là bắt buộc'] },
    });
  });

  it('getApiError parses ValidationProblemDetails and camelCases keys', () => {
    const err = axiosErrorWith(400, {
      type: 'https://tools.ietf.org/html/rfc9110#section-15.5.1',
      title: 'One or more validation errors occurred.',
      status: 400,
      errors: {
        'Lines[0].Quantity': ['The Quantity field is required.'],
        '$.voucherAt': ['Invalid date.'],
      },
    });

    expect(getApiError(err)).toEqual({
      code: 'VALIDATION',
      message: 'One or more validation errors occurred.',
      details: {
        'lines[0].quantity': ['The Quantity field is required.'],
        voucherAt: ['Invalid date.'],
      },
      status: 400,
    });
  });

  it('returns undefined for anything else', () => {
    expect(getApiError(new Error('boom'))).toBeUndefined();
    expect(getApiError(axiosErrorWith(500, 'Internal Server Error'))).toBeUndefined();
    expect(getApiError(null)).toBeUndefined();
  });
});

describe('formatApiErrorDetails', () => {
  it('formatApiErrorDetails joins the detail messages', () => {
    const err = new ApiCallError({
      code: 'VALIDATION',
      message: 'Số lượng phải lớn hơn 0',
      details: {
        'lines[0].quantity': ['Số lượng phải lớn hơn 0'],
        warehouseId: ['Kho là bắt buộc', 'Kho không hoạt động'],
      },
    });

    expect(formatApiErrorDetails(err)).toBe(
      'Số lượng phải lớn hơn 0; Kho là bắt buộc; Kho không hoạt động',
    );
  });

  it('falls back to the error message when there is no API error', () => {
    expect(formatApiErrorDetails(new Error('boom'))).toBe('boom');
  });
});

describe('refresh failure', () => {
  const originalAdapter = api.defaults.adapter;

  beforeEach(() => {
    api.defaults.adapter = async (config) => {
      throw new AxiosError('Unauthorized', 'ERR_BAD_REQUEST', config, null, {
        status: 401,
        statusText: 'Unauthorized',
        headers: {},
        config,
        data: { success: false, error: { code: 'UNAUTHORIZED', message: 'Unauthorized' }, timestamp: '' },
      });
    };
  });

  afterEach(() => {
    api.defaults.adapter = originalAdapter;
    useBranchStore.getState().clear();
    vi.unstubAllGlobals();
  });

  it('refresh failure clears the branch store', async () => {
    useBranchStore.setState({ workingBranchId: 'branch-2' });

    await expect(apiGet('/x')).rejects.toBeInstanceOf(AxiosError);

    expect(useBranchStore.getState().workingBranchId).toBeNull();
  });

  it('refresh failure deletes the api cache', async () => {
    const deleteCache = vi.fn().mockResolvedValue(true);
    vi.stubGlobal('caches', { delete: deleteCache });

    await expect(apiGet('/x')).rejects.toBeInstanceOf(AxiosError);

    expect(deleteCache).toHaveBeenCalledWith('api-cache');
  });
});
