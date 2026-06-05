<div align="center">

# B-Managed

**All-in-one management system for freelancers and small businesses.**

CRM · Projects · Invoices · Expenses · VAT · Reports — one ledger, two clients, zero monthly fees.

[![.NET](https://img.shields.io/badge/.NET-Framework_4.7.2_%2F_8-512BD4?style=flat&logo=dotnet)](#)
[![WCF](https://img.shields.io/badge/WCF-BasicHttpBinding-1f6feb?style=flat)](#)
[![WPF](https://img.shields.io/badge/WPF-XAML-blueviolet?style=flat)](#)
[![ASP.NET](https://img.shields.io/badge/ASP.NET-Razor_Pages-0a86d8?style=flat)](#)
[![MS Access](https://img.shields.io/badge/MS_Access-OleDb-A4373A?style=flat)](#)
[![Tailwind](https://img.shields.io/badge/Tailwind_CSS-CDN-06B6D4?style=flat&logo=tailwindcss)](#)
[![PdfSharp](https://img.shields.io/badge/PdfSharp-1.50-EF4444?style=flat)](#)
[![Status](https://img.shields.io/badge/build-passing-10B981?style=flat)](#)

![architecture](arch-flow.png)

</div>

---

## Why B-Managed

Small businesses (1–10 people) need one place to see customers, projects, invoices, expenses, VAT due, and notifications — without paying QuickBooks rent. B-Managed is that place. Run it on your own machine, your own .accdb, your own port. Two front-ends share one WCF service so the desktop you use at home and the browser your client opens stay in sync.

* 50+ WCF operations
* 13 normalised Access tables
* 36 UI pages (15 WPF + 18 Razor + 3 MAUI)
* 3 roles, 2 currencies, 2 languages
* PDF export, CSV export, receipt upload, multi-employee assignment
* Real-time notifications via DispatcherTimer (WPF) + setInterval (Web)

---

## Architecture

```
                ┌─ BManagedClient (WPF, .NET 4.7.2) ──┐
   role login ──┤                                     ├── BasicHttpBinding ──┐
                └─ BManagedWeb (Razor Pages, .NET 8) ─┘                      │
                                                                             ▼
                                                              ┌─ WCF Service1 (port 8733)
                                                              │   ├ BusinessLogic (PDF, VAT, FX)
                                                              │   └ ViewDB (parameterised OleDb)
                                                              └────────── BManaged.accdb (13 tables)
```

See `arch-flow.png`, `wpf-flow.png`, `web-flow.png` for the full project map.

---

## Features

<table>
<tr>
<td valign="top" width="33%">

### Owner
* Customers — search, modal edit, CSV
* Projects — multi-employee assign, status flow
* Invoices — auto-numbered, line items, VAT auto-calc, **PDF export**
* Expenses — **Auto-VAT 17/117**, receipt upload, CSV
* Reports — VAT summary, top customers (INNER JOIN + GROUP BY), profit chart, **Mark VAT paid**
* Manage Users — approve, promote, reset
* Notifications inbox + live badge

</td>
<td valign="top" width="33%">

### Employee
* Dashboard — assigned projects, my expenses, unread count
* My Projects detail page (description, customer, due)
* Log own expenses
* Settings (change profile / password)
* Notifications

</td>
<td valign="top" width="33%">

### Client
* Portal — outstanding balance, paid / unpaid counters
* Invoice list with status pills
* Download invoice PDF
* Settings

</td>
</tr>
</table>

---

## Highlights

* **Multi-employee assignment** — `ProjectAssignments` table auto-created on first run, no migration needed.
* **Auto-VAT 17/117** — Israeli formula baked in. Type a gross amount, VAT field fills automatically.
* **Mark VAT paid** — one-click record of the periodic settlement to Israel Tax Authority.
* **PDF invoices** via PdfSharp.
* **Receipt upload** up to 5 MB with sanitised filenames stored under `/Receipts`.
* **6-month profit sparkline** on Owner dashboard (Chart.js + JSON handler).
* **Forgot password flow** — fans out a notification to all Owners; reset password is `reset1234`; logging in with it auto-redirects to Settings.
* **Multi-currency** — ILS + USD via `ExchangeRates` table + `CurrencyConverter`.
* **Bilingual** — Hebrew + English with RTL via `L.T(en, he)` helper.
* **Soft Structuralism design system** in `App.xaml` — eyebrow pills, double-bezel cards, pill buttons, Plus Jakarta Sans + Clash Display.

---

## Tech stack

| Layer | Tech |
|-------|------|
| Database | MS Access `.accdb` via Microsoft.ACE.OLEDB.12.0 |
| Server | C# WCF (.NET Framework 4.7.2), BasicHttpBinding |
| Clients | WPF (.NET Framework 4.7.2) + ASP.NET Razor Pages (.NET 8) |
| Auth | PBKDF2 + 16-byte salt + 10 000 iterations + SlowEquals |
| PDF | PdfSharp 1.50.5147 |
| Charts | Chart.js 4.4 (web) |
| CSS | Tailwind via CDN + custom tokens |
| Fonts | Plus Jakarta Sans + Clash Display (web), Segoe UI Variable (WPF) |

---

## Quick start

**Prereqs:** Visual Studio 2022, .NET Framework 4.7.2 + .NET 8, Microsoft Access Database Engine 2016 (32-bit), PowerShell.

```powershell
# 1. Clone
git clone https://github.com/eliRap1/yudb.git
cd yudb

# 2. Initialise the database (creates BManaged.accdb + seeds admin/dana/acme)
.\_init_db.ps1

# 3. Open BManaged.sln in Visual Studio
#    Right-click WcfServiceLibrary1 → Set as Startup Project
#    F5 — WCF Test Client opens at http://localhost:8733/

# 4. Right-click BManagedClient (WPF) → Debug → Start New Instance
#    Login: admin / admin1234

# 5. (optional) Web — open BManagedWeb/BManagedWeb in a second VS instance
#    F5 — http://localhost:5050/
```

---

## Test credentials

| Role | Username | Password |
|------|----------|----------|
| Owner | `admin` | `admin1234` |
| Employee | `dana` | `admin1234` |
| Client | `acme` | `admin1234` |

After Owner uses **Reset PW**, the temp password is `reset1234` and login routes the user straight to Settings.

---

## Folder structure

```
yudb/
├── WcfServiceLibrary1/
│   ├── Model/                 ← Base + Customer/Project/Invoice/Expense/User/Notification + Reports DTOs
│   ├── ViewDB/                ← BaseDB + per-table DBs + ProjectAssignmentDB (auto-schema)
│   │   └── Database/BManaged.accdb
│   ├── BusinessLogic/         ← InvoicePdfBuilder, VatCalculator, CurrencyConverter
│   ├── WcfServiceLibrary1/    ← IService1, Service1, App.config (port 8733)
│   └── ConsoleHost/           ← optional executable host (no admin / no VS WCF tools needed)
├── BManagedClient/            ← WPF (.NET Framework 4.7.2)
│   └── BManagedClient/
│       ├── App.xaml           ← Soft Structuralism design tokens (palette + typography + cards)
│       ├── *.xaml + *.xaml.cs ← 15 pages
│       └── Connected Services/BMsrv/Reference.cs   (svcutil-generated)
├── BManagedWeb/               ← ASP.NET Razor Pages (.NET 8)
│   └── BManagedWeb/
│       ├── Pages/             ← Login, SignUp, ForgotPassword, Owner/, Employee/, Client/, Notifications, Lang
│       ├── Helpers/L.cs       ← i18n helper (en/he)
│       └── Connected Services/bsrv/Reference.cs    (hand-written sync)
├── BManagedMaui/              ← .NET MAUI mobile companion (.NET 10, Android/iOS/macCatalyst)
│   └── BManagedMaui/
│       ├── Pages/             ← LoginPage, OwnerHomePage (dashboard), InvoicesPage  (3 screens)
│       ├── Services/          ← AppState (session) + ServiceHelper (WCF client) + UiHelpers
│       └── Connected Services/BMsrv/Reference.cs   (hand-written, same Service1 contract)
├── packages/PdfSharp.1.50.5147/  ← bundled NuGet for offline build
├── _init_db.ps1               ← seeds admin/dana/acme + 2 customers + 2 projects + invoice + expenses
├── _make_flows.py             ← regenerates arch-flow / wpf-flow / web-flow PNGs
├── _md_to_docx_v6.py          ← builds the Hebrew project book DOCX (RTL)
├── ספר_פרויקט_B_Managed_FINAL_v6.md   ← Hebrew project book (23 chapters)
├── ספר_פרויקט_B_Managed_FINAL_v6b.docx ← rendered DOCX with embedded flow PNGs
├── arch-flow.png  · wpf-flow.png  · web-flow.png
└── README.md
```

---

## Project map

<table>
<tr>
<td><strong>Architecture</strong><br><img src="arch-flow.png" width="100%"></td>
</tr>
<tr>
<td><strong>WPF page navigation</strong><br><img src="wpf-flow.png" width="100%"></td>
</tr>
<tr>
<td><strong>Web page navigation</strong><br><img src="web-flow.png" width="100%"></td>
</tr>
</table>

---

## Documentation

* **`ספר_פרויקט_B_Managed_FINAL_v6.md`** — full Hebrew project book, 23 chapters, ~80 KB markdown.
* **`ספר_פרויקט_B_Managed_FINAL_v6b.docx`** — rendered RTL DOCX with the three flow PNGs embedded.
* Each chapter cites file paths + line ranges so you can jump to the code.

---

## Rubric coverage (Israeli matriculation, 5-unit "Web Services, Async Programming and Databases")

| # | Mandatory item | Status |
|---|----------------|--------|
| 1 | Full information-system interface, multi-table reads/writes | ✅ 50 WCF ops / 13 tables |
| 2 | DB-backed clients with multiple control types | ✅ TextBox, ComboBox, ListView, GridView, ListBox, RadioButton, etc. |
| 3 | Normalised schema + link tables | ✅ InvoiceLines, ProjectAssignments, ExchangeRates, Notifications |
| 4 | 2/3 client kinds | ✅ WPF (desktop) + ASP.NET Razor Pages (web) |
| 5 | Smart data — INNER JOIN + GROUP BY + UPDATE | ✅ 4 reports with INNER JOIN + GROUP BY + SUM |
| 6 | OOP + inheritance | ✅ Base→Customer/Project/.../User; BaseDB→all DBs; AllUsers : List<User> |
| 7 | Multiple permission levels | ✅ Owner, Employee, Client |
| 8 | UI gated by permission | ✅ Server-side guards on every page |

| Extension item | Status |
|----------------|--------|
| 9A — File transfer (PDF + receipts) | ✅ `GenerateInvoicePdf` + `UploadReceipt` |
| 9B — One-way encryption (PBKDF2) | ✅ `SecurityHelper` |
| 9C — Multi-tenant data | ✅ `ProjectAssignments` many-to-many |
| 10 — Async UI / Polling / IValueConverter / Validation / Multi-currency / Notifications / i18n | ✅ all wired |

---

## Roadmap

- [ ] Audit log table + viewer
- [ ] Background job for overdue-invoice email reminders
- [ ] Bank-statement CSV import for expenses auto-match
- [ ] Two-factor auth (TOTP)
- [ ] Multi-tenant (one server, multiple Owners)
- [x] Mobile (.NET MAUI) — owner companion app, 3 screens (Login · Dashboard · Invoices)

---

## License

Educational project — Mateh Ze, Ashdod. Used for the 5-unit matriculation in *Web Services, Async Programming and Databases*. Reuse for personal/study purposes welcome; commercial redistribution requires permission.

---

<div align="center">

Built with C#, WCF, WPF, ASP.NET, MS Access, and a lot of Hebrew project-book pages.

</div>
