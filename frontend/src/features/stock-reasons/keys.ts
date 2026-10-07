import type { StockReasonListParams } from './types';

export const stockReasonKeys = {
  all: ['stock-reasons'] as const,
  lists: () => [...stockReasonKeys.all, 'list'] as const,
  list: (params?: StockReasonListParams) => [...stockReasonKeys.lists(), params ?? {}] as const,
};
