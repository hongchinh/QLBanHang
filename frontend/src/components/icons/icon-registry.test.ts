import { describe, expect, it } from 'vitest';
import { Copy, Download, FileText, Loader2, Plus, Share2, Trash2, Undo2 } from 'lucide-react';
import { ICONS, ICON_TONES, iconDef, type IconName } from './icon-registry';

describe('icon registry', () => {
  it('every entry has a lucide icon and a known tone', () => {
    for (const [name, def] of Object.entries(ICONS)) {
      expect(def.icon, name).toBeTruthy();
      if ('tone' in def) expect(ICON_TONES, name).toContain(def.tone);
    }
  });
  it.each<[IconName, unknown, string | undefined]>([
    ['add', Plus, 'create'],
    ['delete', Trash2, 'danger'],
    ['discardChanges', Undo2, 'muted'],
    ['exportPdf', FileText, 'print'],
    ['quotation', FileText, undefined],
    ['copy', Copy, undefined],
    ['download', Download, undefined],
    ['share', Share2, undefined],
  ])('%s maps to its drawing and tone', (name, icon, tone) => {
    expect(iconDef(name).icon).toBe(icon);
    expect(iconDef(name).tone).toBe(tone);
  });
  it('loading spins', () => {
    expect(iconDef('loading')).toEqual({ icon: Loader2, spin: true });
  });
});
