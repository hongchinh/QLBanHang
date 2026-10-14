"""Classify every lucide icon used in frontend/src against the Notch set and weigh by JSX usage.

Usage: python -I coverage.py <frontend/src>
Classes: match (same meaning), stretch (Notch icon exists but meaning shifts or collides),
utility (chevrons, spinners, ticks: should stay plain), missing (needs a new Notch icon).
"""
import collections
import os
import re
import sys

C = {
    # lucide name: (class, notch id or '', note)
    "Plus": ("match", "plus", ""), "CirclePlus": ("match", "plus", ""),
    "Trash2": ("match", "trash", ""), "Search": ("match", "search", ""), "X": ("match", "close", ""),
    "Pencil": ("match", "edit", ""), "FileText": ("match", "document", "Báo giá, PDF, logo"),
    "CheckCircle2": ("match", "approve", "Xác nhận báo giá"), "LayoutDashboard": ("match", "dashboard", ""),
    "Settings": ("stretch", "settings", "Hệ thống; bánh răng, Notch chỉ có thanh trượt"), "Users2": ("match", "team", "Người dùng"),
    "Wallet": ("match", "wallet", "Thu chi"), "ReceiptText": ("match", "invoice", ""),
    "ArrowUpFromLine": ("match", "upload", "Xuất kho"), "BarChart3": ("match", "reports", ""),
    "CreditCard": ("match", "card", "HTTT"), "ShieldCheck": ("match", "key", "Phân quyền"),
    "ShoppingCart": ("match", "cart", "Bán hàng"), "TrendingUp": ("match", "trend", "Doanh thu"),
    "UserRound": ("match", "user", ""), "Users": ("match", "customer", "Khách hàng"),
    "Warehouse": ("match", "warehouse", "Kho"), "Bell": ("match", "bell", ""), "Clock": ("match", "clock", ""),
    "CalendarDays": ("match", "calendar", ""), "Lock": ("match", "lock", ""),
    "SlidersHorizontal": ("match", "settings", "Cấu hình; Notch `settings` vẽ thanh trượt"),
    "Settings2": ("stretch", "settings", "trùng với SlidersHorizontal"),
    "UserCog": ("stretch", "user", "trùng với UserRound"), "UserPlus": ("stretch", "team", "trùng với Users2"),
    "BadgeCheck": ("stretch", "approve", "trùng với CheckCircle2"),
    "HandCoins": ("stretch", "invoice", "Phiếu thu, trùng với ReceiptText"),
    "AlarmClock": ("stretch", "clock", "Nợ quá hạn, trùng với Clock"),
    "History": ("stretch", "clock", "Nợ đầu kỳ, trùng với Clock"),
    "ArrowDownToLine": ("stretch", "stockin", "chỉ có trong artboard đề xuất"),
    "ClipboardList": ("stretch", "table", "Tồn đầu kỳ"), "Library": ("stretch", "folder", "Danh mục; Notch gọi là Dự án"),
    "Link2": ("stretch", "link", "Đối trừ; Notch gọi là Tích hợp"),
    "ListChecks": ("stretch", "tasks", "Lý do NX"), "Scale": ("stretch", "contract", "Công nợ; Notch gọi là Hợp đồng"),
    "Package": ("stretch", "warehouse", "Hàng hóa; glyph warehouse của Notch là hình hộp"),
    "Boxes": ("stretch", "warehouse", "Tồn kho, trùng"), "Container": ("stretch", "warehouse", "Kho hàng, trùng"),
    "Presentation": ("stretch", "dashboard", "Điều hành, trùng"),
    "Loader2": ("utility", "", "spinner"), "ArrowLeft": ("utility", "", "quay lại"),
    "ChevronDown": ("utility", "", ""), "ChevronRight": ("utility", "", ""),
    "MoreHorizontal": ("utility", "", ""), "MoreVertical": ("utility", "", ""), "Menu": ("utility", "", ""),
    "PanelLeftClose": ("utility", "", ""), "PanelLeftOpen": ("utility", "", ""), "Circle": ("utility", "", "radio"),
    "ArrowUp": ("utility", "", "sort"), "ArrowDown": ("utility", "", "sort"), "Check": ("utility", "", "dấu tick trong select"),
}
MISSING = """Ban Save RotateCcw AlertTriangle FileSpreadsheet Copy Printer Send LayoutList QrCode Star ArrowRightLeft
Eraser Sparkles Calculator Link2Off ArrowLeftRight BadgeDollarSign Banknote BookOpen BookText CarFront Combine Files
FileCheck Gauge Hourglass Landmark ListOrdered PiggyBank ScrollText Sigma Tag Target Truck SearchX Undo2 Building2 Hash
Keyboard""".split()
for n in MISSING:
    C[n] = ("missing", "", "")

def scan(src):
    files = collections.Counter()
    uses = collections.Counter()
    for root, _, fs in os.walk(src):
        for f in fs:
            if not f.endswith((".ts", ".tsx")) or ".test." in f:
                continue
            s = open(os.path.join(root, f), encoding="utf-8").read()
            for m in re.finditer(r"import\s*\{([^}]*)\}\s*from\s*['\"]lucide-react['\"]", s, re.S):
                for n in m.group(1).split(","):
                    n = n.strip().split(" as ")[0].strip()
                    if not n or n.startswith("type ") or n == "LucideIcon":
                        continue
                    files[n] += 1
                    uses[n] += max(1, len(re.findall(r"<" + n + r"\b", s)) + len(re.findall(r"icon:\s*" + n + r"\b", s)))

    return files, uses


if __name__ == "__main__":
    files, uses = scan(sys.argv[1])
    unknown =     [n for n in files if n not in C]
    print("unclassified:", unknown)
    by = collections.defaultdict(list)
    for n in files:
        by[C[n][0]].append(n)
    tot_u = sum(uses.values())
    for k in ("match", "stretch", "utility", "missing"):
        u = sum(uses[n] for n in by[k])
        print(f"{k:<8} icons={len(by[k]):>3}  uses={u:>4} ({100*u/tot_u:.0f}%)  " + ", ".join(sorted(by[k], key=lambda n: -uses[n])))
    print("total icons", len(files), "uses", tot_u)
    used_notch = {v[1] for n, v in C.items() if n in files and v[1]}
    print("Notch ids used by a mapping:", len(used_notch))
    print("Notch ids with no current use:", sorted(set("""home dashboard calendar search settings bell help reports trend pie table database kanban document user team
    customer hr message mail cart warehouse invoice card wallet contract plus close edit trash upload filter sort approve tasks lock key link
    clock folder""".split()) - used_notch))
