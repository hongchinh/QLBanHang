export interface HelpStep { id: string; text: string }            // text keeps the inline markers (**…**, `…`)
export interface HelpShortcut { keys: string; label: string }      // "- `Ctrl S` Lưu" → { keys: 'Ctrl S', label: 'Lưu' }
export interface HelpDoc {
  key: string; title: string; routes: string[]; sources: string[]; related: string[];
  updated: string;                                                 // YYYY-MM-DD
  purpose: string; steps: HelpStep[]; notes: string[]; shortcuts: HelpShortcut[];
}
export type InlineNode = { kind: 'text' | 'strong' | 'kbd'; text: string };
export interface HelpVideoStep { at: number; hash: string }        // seconds from the start; stepHash at recording
export interface HelpVideo {
  youtubeId: string; title: string; duration: number;              // seconds
  recordedAt: string;                                              // YYYY-MM-DD
  steps: Record<string, HelpVideoStep>;                            // by step id
}
