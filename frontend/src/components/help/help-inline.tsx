import { parseInline } from '@/features/help/parse-help';

export function HelpInline({ text }: { text: string }) {
  return (
    <>
      {parseInline(text).map((node, i) => {
        if (node.kind === 'strong') return <strong key={i} className="font-semibold">{node.text}</strong>;
        if (node.kind === 'kbd') {
          return <kbd key={i} className="rounded border bg-muted px-1 py-0.5 font-mono text-[11px]">{node.text}</kbd>;
        }
        return node.text;
      })}
    </>
  );
}
