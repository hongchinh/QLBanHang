"""Build measure.html (geometry audit, results dumped into the DOM) and sheet.html (pixel render sheet).

Usage: python -I build_pages.py <icons.json> <out_dir>
"""
import json
import sys

icons = json.load(open(sys.argv[1], encoding="utf-8"))
out = sys.argv[2]
data = json.dumps(icons, ensure_ascii=False)

measure = """<!doctype html><html><head><meta charset="utf-8"></head><body>
<pre id="out">pending</pre>
<script>
const ICONS = __DATA__;
const NS = 'http://www.w3.org/2000/svg';
const svg = document.createElementNS(NS, 'svg');
svg.setAttribute('viewBox', '0 0 24 24'); svg.setAttribute('width', '240'); svg.setAttribute('height', '240');
document.body.appendChild(svg);
// Signed distance from a point to a rounded rect (negative = inside).
function sd(px, py, x, y, w, h, r) {
  const cx = x + w / 2, cy = y + h / 2, hx = w / 2 - r, hy = h / 2 - r;
  const qx = Math.abs(px - cx) - hx, qy = Math.abs(py - cy) - hy;
  const outside = Math.hypot(Math.max(qx, 0), Math.max(qy, 0));
  const inside = Math.min(Math.max(qx, qy), 0);
  return outside + inside - r;
}
// Size -> stroke width in viewBox units, as specified by the style guide.
const SIZES = [[16, 2], [20, 1.85], [24, 1.75], [32, 1.6]];
const rows = [];
for (const ic of ICONS) {
  const p = document.createElementNS(NS, 'path');
  p.setAttribute('d', ic.d); svg.appendChild(p);
  const L = p.getTotalLength();
  let best = 1e9, bx = 0, by = 0;
  for (let s = 0; s <= L; s += 0.005) {
    const pt = p.getPointAtLength(s);
    const d = sd(pt.x, pt.y, ic.ax, ic.ay, 4, 4, 1);
    if (d < best) { best = d; bx = pt.x; by = pt.y; }
  }
  const bb = p.getBBox();
  const gaps = {};
  for (const [px, sw] of SIZES) gaps[px] = +(((best - sw / 2) * px / 24)).toFixed(3);
  rows.push({
    id: ic.id, cat: ic.cat, len: +L.toFixed(1),
    centerGap: +best.toFixed(3), near: [+bx.toFixed(2), +by.toFixed(2)],
    edgeGapPx: gaps,
    bbox: [+bb.x.toFixed(2), +bb.y.toFixed(2), +(bb.x + bb.width).toFixed(2), +(bb.y + bb.height).toFixed(2)],
    notch: [ic.ax, ic.ay, ic.ax + 4, ic.ay + 4]
  });
  p.remove();
}
document.getElementById('out').textContent = JSON.stringify(rows);
</script></body></html>"""

sheet = """<!doctype html><html><head><meta charset="utf-8"><style>
body{margin:0;background:#fff;font:11px/1.2 monospace;color:#15171C}
table{border-collapse:collapse} td{padding:4px 6px;vertical-align:middle;text-align:center}
td.n{text-align:left;width:86px} .dark td{background:#15171C;color:#F3F2EE}
</style></head><body><table id="t"></table>
<script>
const ICONS = __DATA__;
const NS = 'http://www.w3.org/2000/svg';
const t = document.getElementById('t');
function icon(ic, px, sw, ink, notch) {
  return `<svg width="${px}" height="${px}" viewBox="0 0 24 24" fill="none" stroke="${ink}" stroke-width="${sw}" stroke-linecap="round" stroke-linejoin="round"><path d="${ic.d}"/><rect x="${ic.ax}" y="${ic.ay}" width="4" height="4" rx="1" fill="${notch}" stroke="none"/></svg>`;
}
const per = 14; // icons per row
const cols = [[16, 2], [20, 1.85], [24, 1.75]];
let html = '';
for (const [px, sw] of cols) {
  for (let i = 0; i < ICONS.length; i += per) {
    html += `<tr><td class="n">${px}px</td>` + ICONS.slice(i, i + per).map(ic => `<td>${icon(ic, px, sw, '#15171C', '#FF5A1F')}<br>${ic.id}</td>`).join('') + '</tr>';
  }
}
// State row: same icon (cart) at 16 and 20 px with each notch state.
const cart = ICONS.find(i => i.id === 'cart');
const states = [['mặc định', '#15171C', '#9A9EA6'], ['hover', '#15171C', '#15171C'], ['đang chọn', '#15171C', '#FF5A1F'], ['cảnh báo', '#15171C', '#D92D20']];
html += '<tr><td class="n">states</td>' + states.map(([n, ink, no]) => `<td>${icon(cart, 16, 2, ink, no)} ${icon(cart, 20, 1.85, ink, no)}<br>${n}</td>`).join('') + '</tr>';
html += '<tr class="dark"><td class="n">sidebar</td>' + [['tắt', '#A9ADB5', '#4A4E56'], ['bật', '#FFFFFF', '#FF5A1F']].map(([n, ink, no]) => `<td>${icon(cart, 18, 1.85, ink, no)}<br>${n}</td>`).join('') + '</tr>';
t.innerHTML = html;
</script></body></html>"""

open(f"{out}/measure.html", "w", encoding="utf-8").write(measure.replace("__DATA__", data))
open(f"{out}/sheet.html", "w", encoding="utf-8").write(sheet.replace("__DATA__", data))
print("ok")
