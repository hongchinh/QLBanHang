import type { HelpDoc } from './types';

/** The first-visit hint shows on a screen with help that this user has not seen yet, while the panel is closed. */
export function shouldShowHint(o: {
  userId: string | undefined;
  doc: HelpDoc | null;
  helpOpen: boolean;
  seen: Record<string, string[]>;
  off: Record<string, boolean>;
}): boolean {
  if (!o.userId || !o.doc || o.helpOpen) return false;
  if (o.off[o.userId]) return false;
  return !(o.seen[o.userId] ?? []).includes(o.doc.key);
}
