// Brainstorm 261015-2033, D3: text typed into the product cell prefills the quick add. No whitespace and no
// Vietnamese diacritics reads as a code; anything else reads as a name (the server then generates the code).
export function splitTypedProductText(text: string): { code?: string; name?: string } {
  const trimmed = text.trim();
  if (trimmed === '') return {};
  const hasSpace = /\s/.test(trimmed);
  // \p{M} after NFD also catches text that arrives already decomposed (NFD !== text would miss it).
  const hasDiacritics = /[đĐ]/.test(trimmed) || /\p{M}/u.test(trimmed.normalize('NFD'));
  return hasSpace || hasDiacritics ? { name: trimmed } : { code: trimmed };
}
