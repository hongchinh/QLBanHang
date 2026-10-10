import type { PagedResult } from '@/features/customers/types';

// The credit limit of one customer in the working branch (CreditLimitDto).
export interface CreditLimit {
  customerId: string;
  customerCode: string;
  customerName: string;
  creditDays?: number | null;
  amount: number;
}

export type CreditLimitListResult = PagedResult<CreditLimit>;

export interface CreditLimitListParams {
  search?: string;
  page: number;
  pageSize: number;
}

export interface UpsertCreditLimitRequest {
  amount: number;
}
