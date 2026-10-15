import type { FundKind } from '@/features/cash-common/types';

export interface Fund {
  id: string;
  branchId: string;
  code: string;
  name: string;
  kind: FundKind;
  bankName?: string | null;
  accountNumber?: string | null;
  bankId?: string | null;
  bankShortName?: string | null;
  accountHolderName?: string | null;
  isDefault: boolean;
  // Null for users without cash.opening_balance, cash.catalogs.manage or reports.cash.
  openingAmount: number | null;
  isActive: boolean;
}

// Funds of the working branch.
export interface FundListParams {
  kind?: FundKind;
  isActive?: boolean;
}

export interface CreateFundRequest {
  code: string;
  name: string;
  kind: FundKind;
  bankName?: string | null;
  accountNumber?: string | null;
  bankId?: string | null;
  accountHolderName?: string | null;
  isDefault: boolean;
  isActive: boolean;
}

// The code cannot change after creation.
export interface UpdateFundRequest {
  name: string;
  kind: FundKind;
  bankName?: string | null;
  accountNumber?: string | null;
  bankId?: string | null;
  accountHolderName?: string | null;
  isDefault: boolean;
  isActive: boolean;
}

export interface SetFundOpeningBalanceRequest {
  amount: number;
  acknowledgeNegativeFund: boolean;
}
