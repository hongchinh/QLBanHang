import type { HelpDoc, HelpStep, HelpShortcut, InlineNode } from './types';

// This file is also run by Node directly (type stripping): keep it erasable
// TypeScript with `import type` only.

const FIELDS = ['key', 'title', 'routes', 'sources', 'related', 'updated'];
const SECTIONS = ['Dùng để', 'Các bước', 'Lưu ý', 'Phím tắt'];
const REQUIRED_SECTIONS = ['Dùng để', 'Các bước'];

const KEY_RE = /^[a-z0-9]+(?:-[a-z0-9]+)*(?:\.(?:list|form))?$/;
const DATE_RE = /^\d{4}-\d{2}-\d{2}$/;
const STEP_ID_RE = /^[a-z][a-z0-9-]*$/;
const LINK_RE = /\[[^\]]*\]\([^)]*\)/;

function countOf(text: string, re: RegExp): number {
  return (text.match(re) ?? []).length;
}

export function parseHelpDoc(raw: string, fileKey: string): { doc: HelpDoc | null; errors: string[] } {
  const errors: string[] = [];
  const fail = (message: string) => errors.push(`${fileKey}: ${message}`);

  const checkInline = (where: string, text: string) => {
    if (LINK_RE.test(text)) fail(`${where} contains a Markdown link`);
    if (countOf(text, /\*\*/g) % 2 !== 0) fail(`${where} has unbalanced ** markers`);
    if (countOf(text, /`/g) % 2 !== 0) fail(`${where} has unbalanced backtick markers`);
  };

  const lines = raw.replace(/\r/g, '').split('\n');

  // Frontmatter
  const fields: Record<string, string> = {};
  let body: string[] = lines;
  if (lines[0]?.trim() !== '---') {
    fail('missing frontmatter (the file must start with a "---" line)');
  } else {
    const close = lines.indexOf('---', 1);
    if (close === -1) {
      fail('frontmatter is not closed (missing the second "---" line)');
      body = [];
    } else {
      body = lines.slice(close + 1);
      for (const line of lines.slice(1, close)) {
        if (line.trim() === '') continue;
        const m = /^([A-Za-z]+):\s*(.*)$/.exec(line);
        if (!m) {
          fail(`malformed frontmatter line "${line}"`);
        } else if (!FIELDS.includes(m[1])) {
          fail(`unknown field "${m[1]}" in frontmatter`);
        } else {
          fields[m[1]] = m[2].trim();
        }
      }
    }
  }

  const scalar = (name: string): string => {
    const value = fields[name] ?? '';
    if (value === '') fail(`missing required field "${name}"`);
    return value;
  };
  const list = (name: string, required: boolean): string[] => {
    const value = fields[name];
    if (value === undefined || value === '') {
      if (required) fail(`missing required field "${name}"`);
      return [];
    }
    if (!value.startsWith('[') || !value.endsWith(']')) {
      fail(`field "${name}" must be a list like [a, b]`);
      return [];
    }
    const items = value
      .slice(1, -1)
      .split(',')
      .map((s) => s.trim())
      .filter((s) => s !== '');
    if (required && items.length === 0) fail(`field "${name}" must not be empty`);
    return items;
  };

  const key = scalar('key');
  if (key !== '') {
    if (key !== fileKey) fail(`key "${key}" does not match the file name`);
    if (!KEY_RE.test(key)) fail(`key "${key}" is not a valid key`);
  }
  const title = scalar('title');
  const routes = list('routes', true);
  for (const route of routes) {
    if (!route.startsWith('/')) fail(`route "${route}" must start with "/"`);
  }
  const sources = list('sources', true);
  const related = list('related', false);
  const updated = scalar('updated');
  if (updated !== '' && !DATE_RE.test(updated)) fail(`updated "${updated}" must be YYYY-MM-DD`);

  // Sections
  const sections: Record<string, string[]> = {};
  let current: string[] | null = null;
  let lastIndex = -1;
  for (const line of body) {
    const heading = /^## (.+?)\s*$/.exec(line);
    if (heading) {
      const name = heading[1];
      const index = SECTIONS.indexOf(name);
      if (index === -1) {
        fail(`unknown section "## ${name}"`);
        current = null;
      } else if (name in sections) {
        fail(`duplicate section "## ${name}"`);
        current = null;
      } else {
        if (index < lastIndex) fail(`section "## ${name}" is out of order`);
        lastIndex = Math.max(lastIndex, index);
        current = [];
        sections[name] = current;
      }
    } else if (current) {
      current.push(line);
    } else if (line.trim() !== '' && Object.keys(sections).length === 0) {
      fail(`text before the first section: "${line.trim()}"`);
    }
  }
  for (const name of REQUIRED_SECTIONS) {
    if (!(name in sections)) fail(`missing section "## ${name}"`);
  }
  const content = (name: string): string[] =>
    (sections[name] ?? []).map((l) => l.trim()).filter((l) => l !== '');

  // Dùng để
  const purpose = content('Dùng để').join(' ');
  if ('Dùng để' in sections) {
    if (purpose === '') {
      fail('"Dùng để" must not be empty');
    } else {
      const sentences = countOf(purpose, /[.!?](?=\s|$)/g);
      if (sentences < 1 || sentences > 2) fail(`"Dùng để" must have 1 or 2 sentences (found ${sentences})`);
      checkInline('"Dùng để"', purpose);
    }
  }

  // Các bước
  const steps: HelpStep[] = [];
  if ('Các bước' in sections) {
    const stepLines = content('Các bước');
    if (stepLines.length < 3 || stepLines.length > 7) {
      fail(`"Các bước" must have 3–7 steps (found ${stepLines.length})`);
    }
    const seen = new Set<string>();
    stepLines.forEach((line, i) => {
      const m = /^(\d+)\.\s+(.*)$/.exec(line);
      if (!m) {
        fail(`malformed step line "${line}" (expected "N. {#id} text")`);
        return;
      }
      if (Number(m[1]) !== i + 1) fail(`step numbering: expected ${i + 1} but found ${m[1]}`);
      const rest = /^\{#([^}\s]*)\}\s*(.*)$/.exec(m[2]);
      if (!rest) {
        fail(`step ${i + 1} is missing its step id ("{#id}")`);
        return;
      }
      const [, id, text] = rest;
      if (!STEP_ID_RE.test(id)) fail(`invalid step id "${id}"`);
      if (seen.has(id)) fail(`duplicate step id "${id}"`);
      seen.add(id);
      if (text === '') fail(`step "${id}" has no text`);
      checkInline(`step "${id}"`, text);
      steps.push({ id, text });
    });
  }

  // Lưu ý
  const notes: string[] = [];
  if ('Lưu ý' in sections) {
    const noteLines = content('Lưu ý');
    if (noteLines.length === 0) fail('"Lưu ý" must have at least one note');
    for (const line of noteLines) {
      const m = /^- (.+)$/.exec(line);
      if (!m) {
        fail(`malformed note line "${line}" (expected "- text")`);
        continue;
      }
      checkInline('note', m[1]);
      notes.push(m[1]);
    }
  }

  // Phím tắt
  const shortcuts: HelpShortcut[] = [];
  if ('Phím tắt' in sections) {
    const shortcutLines = content('Phím tắt');
    if (shortcutLines.length === 0) fail('"Phím tắt" must have at least one shortcut');
    for (const line of shortcutLines) {
      const m = /^- `([^`]+)`\s+(.+)$/.exec(line);
      if (!m) {
        fail(`malformed shortcut line "${line}" (expected a backticked key then a label)`);
        continue;
      }
      checkInline('shortcut label', m[2]);
      shortcuts.push({ keys: m[1], label: m[2] });
    }
  }

  if (errors.length > 0) return { doc: null, errors };
  return {
    doc: { key, title, routes, sources, related, updated, purpose, steps, notes, shortcuts },
    errors,
  };
}

export function parseInline(text: string): InlineNode[] {
  const nodes: InlineNode[] = [];
  let plain = '';
  const flush = () => {
    if (plain !== '') nodes.push({ kind: 'text', text: plain });
    plain = '';
  };
  let i = 0;
  while (i < text.length) {
    const marker = text.startsWith('**', i) ? '**' : text[i] === '`' ? '`' : null;
    const end = marker === null ? -1 : text.indexOf(marker, i + marker.length);
    if (marker === null) {
      plain += text[i];
      i += 1;
    } else if (end === -1) {
      plain += marker; // an opener without its closer stays literal text
      i += marker.length;
    } else {
      flush();
      nodes.push({ kind: marker === '**' ? 'strong' : 'kbd', text: text.slice(i + marker.length, end) });
      i = end + marker.length;
    }
  }
  flush();
  return nodes;
}

// The words that are spoken for a step: no id, no markers, one space between words.
export function narrationText(text: string): string {
  return text
    .replace(/^\s*\{#[^}]*\}/, '')
    .replace(/\*\*|`/g, '')
    .replace(/\s+/g, ' ')
    .trim()
    .normalize('NFC');
}

// FNV-1a 32-bit over the UTF-8 narration text, as 8 lowercase hex characters.
export function stepHash(text: string): string {
  let hash = 0x811c9dc5;
  for (const byte of new TextEncoder().encode(narrationText(text))) {
    hash = Math.imul(hash ^ byte, 0x01000193) >>> 0;
  }
  return hash.toString(16).padStart(8, '0');
}
