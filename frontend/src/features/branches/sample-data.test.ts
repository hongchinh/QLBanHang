import { describe, expect, it } from 'vitest';
import type { Branch } from './types';
import { branchesRefetchInterval, hasRunningSampleData } from './sample-data';

const branch = (sampleDataStatus: Branch['sampleDataStatus']) =>
  ({ sampleDataStatus }) as Branch;

describe('hasRunningSampleData', () => {
  it('is false without data', () => {
    expect(hasRunningSampleData(undefined)).toBe(false);
  });

  it('is false when nothing is running', () => {
    expect(hasRunningSampleData([branch('Ready'), branch('None')])).toBe(false);
  });

  it('is true when one branch is running', () => {
    expect(hasRunningSampleData([branch('Ready'), branch('Running')])).toBe(true);
  });
});

describe('branchesRefetchInterval', () => {
  it('polls every 2 seconds while a branch is running', () => {
    expect(branchesRefetchInterval([branch('Running')])).toBe(2000);
  });

  it('stops polling otherwise', () => {
    expect(branchesRefetchInterval([branch('Ready')])).toBe(false);
    expect(branchesRefetchInterval(undefined)).toBe(false);
    expect(branchesRefetchInterval([])).toBe(false);
    expect(branchesRefetchInterval([branch('Failed'), branch(undefined)])).toBe(false);
  });
});
