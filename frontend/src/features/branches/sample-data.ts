import type { Branch } from './types';

const POLL_MS = 2000;

export function hasRunningSampleData(branches: Branch[] | undefined): boolean {
  return !!branches?.some((b) => b.sampleDataStatus === 'Running');
}

// Poll the branch list only while a background sample-data fill is in progress.
export function branchesRefetchInterval(branches: Branch[] | undefined): number | false {
  return hasRunningSampleData(branches) ? POLL_MS : false;
}
