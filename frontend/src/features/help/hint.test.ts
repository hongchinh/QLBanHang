import { describe, expect, it } from 'vitest';
import { stockOutDoc } from '@/components/help/test-fixtures';
import { shouldShowHint } from './hint';

const base = { userId: 'u1', doc: stockOutDoc, helpOpen: false, seen: {}, off: {} };

describe('shouldShowHint', () => {
  it('shows for a user with a doc, a closed panel, an unseen key and hints not turned off', () => {
    expect(shouldShowHint(base)).toBe(true);
  });

  it('hides without a user', () => {
    expect(shouldShowHint({ ...base, userId: undefined })).toBe(false);
  });

  it('hides without a doc', () => {
    expect(shouldShowHint({ ...base, doc: null })).toBe(false);
  });

  it('hides while the panel is open', () => {
    expect(shouldShowHint({ ...base, helpOpen: true })).toBe(false);
  });

  it('hides once the key was seen by this user', () => {
    expect(shouldShowHint({ ...base, seen: { u1: [stockOutDoc.key] } })).toBe(false);
  });

  it('hides when this user turned the hints off', () => {
    expect(shouldShowHint({ ...base, off: { u1: true } })).toBe(false);
  });

  it('ignores what another user has seen or turned off', () => {
    expect(shouldShowHint({ ...base, seen: { u2: [stockOutDoc.key] }, off: { u2: true } })).toBe(true);
  });
});
