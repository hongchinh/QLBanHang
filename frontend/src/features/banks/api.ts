import { apiGet } from '@/lib/api-client';
import type { Bank } from './types';

export const banksApi = {
  list: () => apiGet<Bank[]>('/banks'),
};
