"""Regenerate flow PNGs reflecting current architecture (port 8733, 15 WPF pages,
18 web pages, multi-assign, ForgotPassword, EmployeeProjects).

Soft Structuralism palette."""
import os, math
from PIL import Image, ImageDraw, ImageFont

OUT = os.path.dirname(os.path.abspath(__file__))
W, H = 1700, 1000
PAPER = (250, 250, 247)
INK = (11, 11, 15)
INK_SOFT = (110, 110, 130)
ACCENT = (59, 91, 255)
MINT = (16, 185, 129)
AMBER = (245, 158, 11)
ROSE = (239, 68, 68)
GREY = (180, 180, 180)


def font(size, bold=False):
    paths = [
        r"C:\Windows\Fonts\segoeuib.ttf" if bold else r"C:\Windows\Fonts\segoeui.ttf",
        r"C:\Windows\Fonts\arialbd.ttf"  if bold else r"C:\Windows\Fonts\arial.ttf",
    ]
    for p in paths:
        if os.path.exists(p):
            try: return ImageFont.truetype(p, size)
            except Exception: pass
    return ImageFont.load_default()


def card(draw, x, y, w, h, label, color=ACCENT, sub=""):
    r = 18
    draw.rounded_rectangle((x, y, x + w, y + h), r, fill=PAPER, outline=color, width=2)
    draw.rounded_rectangle((x + 4, y + 4, x + w - 4, y + h - 4), r - 4, outline=(230, 230, 230), width=1)
    draw.text((x + 16, y + 14), label, font=font(20, bold=True), fill=INK)
    if sub:
        draw.text((x + 16, y + 42), sub, font=font(13), fill=INK_SOFT)


def arrow(draw, x1, y1, x2, y2, color=ACCENT, label=""):
    draw.line((x1, y1, x2, y2), fill=color, width=2)
    ang = math.atan2(y2 - y1, x2 - x1)
    L = 11
    p1 = (x2 - L * math.cos(ang - math.pi / 7), y2 - L * math.sin(ang - math.pi / 7))
    p2 = (x2 - L * math.cos(ang + math.pi / 7), y2 - L * math.sin(ang + math.pi / 7))
    draw.polygon((x2, y2, p1[0], p1[1], p2[0], p2[1]), fill=color)
    if label:
        f = font(11)
        mx, my = (x1 + x2) // 2, (y1 + y2) // 2 - 8
        bbox = draw.textbbox((mx, my), label, font=f)
        draw.rectangle((bbox[0] - 4, bbox[1] - 2, bbox[2] + 4, bbox[3] + 2), fill=PAPER)
        draw.text((mx, my), label, font=f, fill=INK_SOFT)


def title(draw, t, sub):
    draw.text((40, 30), t, font=font(36, bold=True), fill=INK)
    draw.text((40, 76), sub, font=font(16), fill=INK_SOFT)


# ============================ ARCH ===========================
img = Image.new("RGB", (W, H), PAPER); d = ImageDraw.Draw(img)
title(d, "B-Managed — Architecture",
      "WCF on :8733 · Access (.accdb) · 2 clients · multi-currency · 13 tables")

card(d, 100,  200, 400, 100, "BManagedClient (WPF)", MINT,
     "15 pages · DispatcherTimer · BMsrv proxy")
card(d, 1200, 200, 400, 100, "BManagedWeb (Razor)", MINT,
     "18 pages · setInterval · bsrv proxy")

card(d, 650,  200, 400, 100, "WCF Service1",            ACCENT,
     "BasicHttpBinding · ~50 ops · :8733")
card(d, 650,  370, 400,  90, "BusinessLogic",           AMBER,
     "PdfSharp · CurrencyConverter · VatCalc")
card(d, 650,  500, 400, 110, "ViewDB",                  ACCENT,
     "BaseDB · UserDB · CustomerDB · ProjectDB · InvoiceDB · ExpenseDB")
card(d, 650,  650, 400,  90, "ProjectAssignmentDB",     AMBER,
     "auto EnsureSchema · many-to-many")
card(d, 650,  780, 400, 100, "BManaged.accdb",          ROSE,
     "13 tables · seeded admin/dana/acme · ILS↔USD")

arrow(d, 500,  250,  650, 250, MINT, "WCF SOAP")
arrow(d, 1200, 250, 1050, 250, MINT, "WCF SOAP")
arrow(d, 850,  300,  850, 370, ACCENT, "Service1 → BL")
arrow(d, 850,  460,  850, 500, AMBER, "BL → DB")
arrow(d, 850,  610,  850, 650, ACCENT, "ViewDB → assignments")
arrow(d, 850,  740,  850, 780, ROSE, "OleDb (parameterised)")
img.save(os.path.join(OUT, "arch-flow.png"))

# ============================ WPF ===========================
img = Image.new("RGB", (W, H), PAPER); d = ImageDraw.Draw(img)
title(d, "B-Managed WPF — page navigation",
      "Frame-based · role guard at every page · DispatcherTimer polling")

card(d, 750,  130, 240, 70, "LogIn", ACCENT, "PBKDF2 verify")
card(d, 360,  240, 240, 60, "SignUp", AMBER, "")
card(d, 1140, 240, 240, 60, "ForgotPassword", AMBER, "notifies all Owners")
arrow(d, 870, 200, 480, 240, AMBER, "/SignUp")
arrow(d, 870, 200, 1260, 240, AMBER, "/Forgot")

card(d, 200, 360, 280, 70, "OwnerHome", MINT, "DispatcherTimer 15s + badge")
card(d, 700, 360, 280, 70, "EmployeeHome", AMBER, "DispatcherTimer 15s")
card(d, 1200, 360, 280, 70, "ClientHome", ROSE, "DispatcherTimer 30s")

arrow(d, 870, 200, 340, 360, MINT, "Owner")
arrow(d, 870, 200, 840, 360, AMBER, "Employee")
arrow(d, 870, 200, 1340, 360, ROSE, "Client")

# Owner spokes (8 pages)
y = 530
labels = ["Customers", "Projects", "Invoices", "Expenses",
          "Reports", "ManageUsers", "Settings", "Notifications"]
for i, lbl in enumerate(labels):
    x = 40 + i * 205
    card(d, x, y, 195, 60, lbl, ACCENT)
    arrow(d, 340, 430, x + 90, y, ACCENT)

# Employee spokes
card(d, 700, 530, 280, 60, "EmployeeProjects", AMBER, "detail panel")
card(d, 700, 610, 280, 60, "Expenses (own)",  AMBER, "")
card(d, 700, 690, 280, 60, "Settings",        AMBER, "change password")
card(d, 700, 770, 280, 60, "Notifications",   AMBER, "")
arrow(d, 840, 430, 840, 530, AMBER)

# Client spokes
card(d, 1200, 530, 280, 60, "Settings",       ROSE)
card(d, 1200, 610, 280, 60, "Logout",         ROSE)
arrow(d, 1340, 430, 1340, 530, ROSE)

# Cross-link: notification dbl-click jumps to ManageUsers
arrow(d, 1480, 590, 1100, 590, INK_SOFT, "ResetRequest dbl-click")
img.save(os.path.join(OUT, "wpf-flow.png"))

# ============================ WEB ===========================
img = Image.new("RGB", (W, H), PAPER); d = ImageDraw.Draw(img)
title(d, "B-Managed Web — Razor Pages",
      "Session-based role guard · setInterval polling · L.T(en, he) i18n")

card(d, 750,  130, 240, 70, "/Login", ACCENT, "session role")
card(d, 360,  240, 240, 60, "/SignUp", AMBER, "")
card(d, 1140, 240, 240, 60, "/ForgotPassword", AMBER, "")

arrow(d, 870, 200, 480, 240, AMBER)
arrow(d, 870, 200, 1260, 240, AMBER)

card(d, 200, 360, 280, 70, "/Owner/Home", MINT, "sparkline + bento")
card(d, 700, 360, 280, 70, "/Employee/Home", AMBER)
card(d, 1200, 360, 280, 70, "/Client/Portal", ROSE)

arrow(d, 870, 200, 340, 360, MINT)
arrow(d, 870, 200, 840, 360, AMBER)
arrow(d, 870, 200, 1340, 360, ROSE)

owner = [("Customers", "modal + CSV"), ("Projects", "multi-assign"),
         ("Invoices", "PDF"), ("Expenses", "Auto-VAT + receipt + CSV"),
         ("Reports", "VAT pay + charts"), ("Users", "approve / reset")]
for i, (lbl, sub) in enumerate(owner):
    x = 40 + i * 270
    card(d, x, 530, 260, 70, "/Owner/" + lbl, ACCENT, sub)
    arrow(d, 340, 430, x + 130, 530, ACCENT)

card(d, 700, 660, 280, 60, "/Employee/Projects", AMBER)
card(d, 700, 740, 280, 60, "/Employee/Expenses", AMBER)
arrow(d, 840, 430, 840, 660, AMBER)

card(d, 1200, 660, 280, 60, "/Client/InvoiceView", ROSE)
arrow(d, 1340, 430, 1340, 660, ROSE)

card(d, 1200, 740, 280, 60, "/Notifications", ACCENT, "badge polling")
card(d, 1200, 820, 280, 60, "/Lang?target=he", AMBER, "i18n toggle")
img.save(os.path.join(OUT, "web-flow.png"))

print("regenerated arch-flow.png, wpf-flow.png, web-flow.png")
