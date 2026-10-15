"""Extract Notch icon definitions (id, vi, cat, d, ax, ay) from a Library.dc.html file as data.

Usage: python -I extract_icons.py <Library.dc.html> <RedesignDashboard.dc.html> <out.json>
"""
import json
import re
import sys

lib_src = open(sys.argv[1], encoding="utf-8").read()
dash_src = open(sys.argv[2], encoding="utf-8").read()

icon_re = re.compile(
    r"\{\s*id:\s*'([^']+)',\s*vi:\s*'([^']+)',\s*cat:\s*'([^']+)',\s*d:\s*'([^']+)',\s*ax:\s*([\d.]+),\s*ay:\s*([\d.]+)\s*\}"
)
icons = [
    {"id": m[0], "vi": m[1], "cat": m[2], "d": m[3], "ax": float(m[4]), "ay": float(m[5]), "src": "library"}
    for m in icon_re.findall(lib_src)
]

# Icons that exist only in the redesign artboards (not in the 40-icon library).
extra_re = re.compile(r"(stockin|stockout):\s*\['([^']+)',\s*([\d.]+),\s*([\d.]+)\]")
for m in extra_re.findall(dash_src):
    icons.append({"id": m[0], "vi": m[0], "cat": "redesign", "d": m[1], "ax": float(m[2]), "ay": float(m[3]), "src": "redesign"})

json.dump(icons, open(sys.argv[3], "w", encoding="utf-8"), ensure_ascii=False, indent=1)
print(len(icons), "icons;", sum(1 for i in icons if i["src"] == "library"), "from library")
