export interface Branch {
  id: string;
  code: string;
  name: string;
  address?: string | null;
  // DateOnly `yyyy-MM-dd`; null = no period lock.
  lockedUntil?: string | null;
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
}

export interface UpdateBranchRequest {
  name: string;
  address?: string | null;
}
