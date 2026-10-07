import { create } from 'zustand';

interface BranchState {
  workingBranchId: string | null;
  setWorkingBranch: (userId: string, branchId: string) => void;
  restore: (userId: string, allowedIds: string[], defaultId: string) => string;
  clear: () => void;
}

const storageKey = (userId: string) => `working_branch_${userId}`;

// Every localStorage access is guarded: private mode / blocked storage throws.
function readStored(userId: string): string | null {
  try {
    return localStorage.getItem(storageKey(userId));
  } catch {
    return null;
  }
}

function writeStored(userId: string, branchId: string): void {
  try {
    localStorage.setItem(storageKey(userId), branchId);
  } catch {
    // ignore storage errors
  }
}

function removeStored(userId: string): void {
  try {
    localStorage.removeItem(storageKey(userId));
  } catch {
    // ignore storage errors
  }
}

// Working branch sent as X-Branch-Id on every API call. The choice is kept per
// user in localStorage so it survives reloads; state itself lives in memory and
// is cleared on logout so the next user never sends the previous user's branch.
export const useBranchStore = create<BranchState>((set) => ({
  workingBranchId: null,
  setWorkingBranch: (userId, branchId) => {
    writeStored(userId, branchId);
    set({ workingBranchId: branchId });
  },
  restore: (userId, allowedIds, defaultId) => {
    const stored = readStored(userId);
    let id = defaultId;
    if (stored && allowedIds.includes(stored)) {
      id = stored;
    } else if (stored) {
      removeStored(userId);
    }
    set({ workingBranchId: id });
    return id;
  },
  clear: () => set({ workingBranchId: null }),
}));
