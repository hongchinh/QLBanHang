import axios, {
  type AxiosError,
  type AxiosInstance,
  type AxiosRequestConfig,
  type InternalAxiosRequestConfig,
} from 'axios';
import { useAuthStore } from '@/stores/auth-store';
import { useBranchStore } from '@/stores/branch-store';
import { queryClient } from '@/lib/query-client';

export interface ApiError {
  code: string;
  message: string;
  details?: Record<string, string[]>;
}

export interface ApiResponse<T> {
  success: boolean;
  data?: T;
  error?: ApiError;
  timestamp: string;
}

// Endpoints that must never trigger the refresh-on-401 dance: a 401 from these
// IS the auth state. Refreshing would loop or mask the real failure.
const NO_REFRESH_PATHS = ['/auth/login', '/auth/refresh', '/auth/logout'];

interface RetriableConfig extends InternalAxiosRequestConfig {
  _retried?: boolean;
}

// VITE_API_BASE_URL is the canonical name (Railway / standard convention).
// VITE_API_BASE is the legacy name; kept for backwards compatibility.
// Falls back to '/api' so the Vite dev proxy keeps working in `npm run dev`.
const apiBaseUrl =
  import.meta.env.VITE_API_BASE_URL ?? import.meta.env.VITE_API_BASE ?? '/api';

const api: AxiosInstance = axios.create({
  baseURL: apiBaseUrl,
  timeout: 30_000,
  // Required so the browser sends the HttpOnly refresh cookie on /auth/* calls.
  withCredentials: true,
  headers: { 'Content-Type': 'application/json' },
});

api.interceptors.request.use((config) => {
  const token = useAuthStore.getState().accessToken;
  if (token) {
    config.headers = config.headers ?? {};
    config.headers.Authorization = `Bearer ${token}`;
  }
  const branchId = useBranchStore.getState().workingBranchId;
  if (branchId) config.headers['X-Branch-Id'] = branchId;
  return config;
});

// Single-flight refresh: if many requests fail with 401 at once, only one
// /auth/refresh fires and the others wait for its result.
let refreshInFlight: Promise<string | null> | null = null;

async function attemptRefresh(): Promise<string | null> {
  if (refreshInFlight) return refreshInFlight;
  refreshInFlight = (async () => {
    try {
      const res = await api.post<ApiResponse<{ accessToken: string; accessTokenExpiresAt: string }>>(
        '/auth/refresh',
        {},
      );
      const payload = res.data;
      if (!payload.success || !payload.data) return null;
      const { accessToken, accessTokenExpiresAt } = payload.data;
      useAuthStore.getState().setToken(accessToken, accessTokenExpiresAt);
      return accessToken;
    } catch {
      return null;
    } finally {
      // Release the lock on the next tick so concurrent callers can read the result.
      setTimeout(() => {
        refreshInFlight = null;
      }, 0);
    }
  })();
  return refreshInFlight;
}

api.interceptors.response.use(
  (response) => response,
  async (error: AxiosError<ApiResponse<unknown>>) => {
    const original = error.config as RetriableConfig | undefined;
    const status = error.response?.status;
    const url = original?.url ?? '';

    const shouldTryRefresh =
      status === 401 &&
      original &&
      !original._retried &&
      !NO_REFRESH_PATHS.some((p) => url.includes(p));

    if (shouldTryRefresh) {
      const newToken = await attemptRefresh();
      if (newToken) {
        original._retried = true;
        original.headers = original.headers ?? {};
        original.headers.Authorization = `Bearer ${newToken}`;
        return api.request(original);
      }
      // Refresh failed → session is dead. Clear state; ProtectedRoute redirects.
      useAuthStore.getState().logout();
      useBranchStore.getState().clear();
      queryClient.clear();
      if ('caches' in window) void caches.delete('api-cache');
    }

    return Promise.reject(error);
  },
);

export async function apiGet<T>(url: string, params?: unknown, config?: AxiosRequestConfig): Promise<T> {
  const res = await api.get<ApiResponse<T>>(url, { ...config, params });
  return unwrap(res.data);
}

export async function apiPost<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
  const res = await api.post<ApiResponse<T>>(url, data, config);
  return unwrap(res.data);
}

export async function apiPut<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
  const res = await api.put<ApiResponse<T>>(url, data, config);
  return unwrap(res.data);
}

export async function apiPatch<T>(url: string, data?: unknown, config?: AxiosRequestConfig): Promise<T> {
  const res = await api.patch<ApiResponse<T>>(url, data, config);
  return unwrap(res.data);
}

export async function apiDelete<T = void>(url: string, config?: AxiosRequestConfig): Promise<T> {
  const res = await api.delete<ApiResponse<T>>(url, config);
  return unwrap(res.data);
}

function unwrap<T>(payload: ApiResponse<T>): T {
  if (!payload.success) {
    throw new ApiCallError(payload.error ?? { code: 'UNKNOWN', message: 'Unknown error' });
  }
  return payload.data as T;
}

export class ApiCallError extends Error {
  code: string;
  details?: Record<string, string[]>;

  constructor(error: ApiError) {
    super(error.message);
    this.code = error.code;
    this.details = error.details;
  }
}

export function getErrorMessage(error: unknown): string {
  if (error instanceof ApiCallError) return error.message;
  if (axios.isAxiosError(error)) {
    const data = error.response?.data as ApiResponse<unknown> | undefined;
    if (data?.error?.message) return data.error.message;
    return error.message;
  }
  if (error instanceof Error) return error.message;
  return 'Đã xảy ra lỗi không mong muốn.';
}

// The client gave up waiting (axios `timeout`); the server may still be processing the request.
export function isRequestTimeout(error: unknown): boolean {
  return axios.isAxiosError(error) && (error.code === 'ECONNABORTED' || error.code === 'ETIMEDOUT');
}

export interface ApiErrorShape {
  code: string;
  message: string;
  details?: Record<string, string[]>;
  status?: number;
}

interface ValidationProblemDetails {
  title?: string;
  status?: number;
  errors: Record<string, string[]>;
}

function isObject(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}

function isApiErrorBody(value: unknown): value is { error: ApiError } {
  return isObject(value) && isObject(value.error) && typeof value.error.code === 'string';
}

function isValidationProblem(value: unknown): value is ValidationProblemDetails {
  return isObject(value) && isObject(value.errors);
}

// 'Lines[0].Quantity' → 'lines[0].quantity'; '$.voucherAt' → 'voucherAt'.
function toCamelPath(key: string): string {
  return key
    .replace(/^\$\.?/, '')
    .split('.')
    .map((segment) => segment.charAt(0).toLowerCase() + segment.slice(1))
    .join('.');
}

// Reads ApiCallError (2xx with success:false), an AxiosError carrying
// response.data.error (non-2xx ApiResponse) and ASP.NET ValidationProblemDetails
// (400 binding errors: { title, errors }, keys normalized to camelCase paths).
// Returns undefined for anything else.
export function getApiError(error: unknown): ApiErrorShape | undefined {
  if (error instanceof ApiCallError) {
    return { code: error.code, message: error.message, details: error.details };
  }
  if (!axios.isAxiosError(error)) return undefined;

  const status = error.response?.status;
  const data: unknown = error.response?.data;
  if (isApiErrorBody(data)) {
    const { code, message, details } = data.error;
    return { code, message, details, status };
  }
  if (isValidationProblem(data)) {
    const details: Record<string, string[]> = {};
    for (const [key, messages] of Object.entries(data.errors)) {
      details[toCamelPath(key)] = messages;
    }
    return {
      code: 'VALIDATION',
      message: data.title ?? 'Dữ liệu không hợp lệ.',
      details,
      status: data.status ?? status,
    };
  }
  return undefined;
}

// "message" plus the joined detail messages (duplicates dropped), for toasts.
export function formatApiErrorDetails(error: unknown): string {
  const apiError = getApiError(error);
  if (!apiError) return getErrorMessage(error);
  const messages = [apiError.message, ...Object.values(apiError.details ?? {}).flat()];
  return [...new Set(messages)].join('; ');
}

export default api;
