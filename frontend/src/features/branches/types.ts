export type SampleDataStatus = 'None' | 'Running' | 'Ready' | 'Failed';

export interface Branch {
  id: string;
  code: string;
  name: string;
  address?: string | null;
  // DateOnly `yyyy-MM-dd`; null = no period lock.
  lockedUntil?: string | null;
  // DateOnly `yyyy-MM-dd`: opening stock, opening debt and fund opening balances use it (P8).
  openingDate?: string | null;
  // Demo branch filled with sample data for training; status/progress/error are set by the background worker.
  isDemo?: boolean;
  sampleDataStatus?: SampleDataStatus;
  // 0-100 while Running.
  sampleDataProgress?: number;
  sampleDataError?: string | null;
}

export interface MyBranches {
  defaultBranchId: string;
  workingBranchId: string;
  canSwitch: boolean;
  // All branches when canSwitch, otherwise only the default branch.
  branches: Branch[];
}

export interface CreateBranchRequest {
  code: string;
  name: string;
  address?: string | null;
  withSampleData?: boolean;
}

export interface UpdateBranchRequest {
  name: string;
  address?: string | null;
}

export interface DemoBranchDeletionPreview {
  documentCount: number;
  reassignedUserCount: number;
}

export interface DemoBranchDeletionResult {
  reassignedUserCount: number;
  keptSampleProductCount: number;
  keptSamplePartnerCount: number;
}
