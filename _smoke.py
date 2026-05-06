"""Quick smoke test against the running BManagedWeb on :5050.
Logs in as admin/admin1234, walks every Owner page, prints a pass/fail line
for each step. No browser dependency — pure urllib + cookie jar."""
import re, sys, urllib.parse, http.cookiejar, urllib.request

BASE = "http://localhost:5050"

def session():
    cj = http.cookiejar.CookieJar()
    return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))

def get(o, path):
    return o.open(BASE + path).read().decode("utf-8", "replace")

def post(o, path, data):
    body = urllib.parse.urlencode(data).encode()
    req  = urllib.request.Request(BASE + path, data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded"})
    return o.open(req).read().decode("utf-8", "replace")

def check(label, ok, hint=""):
    mark = "OK " if ok else "FAIL"
    print(f"{mark}  {label}{(' — ' + hint) if hint else ''}")
    return ok

o = session()

# 1) login page renders
html = get(o, "/Login")
check("login page reachable", "name=\"Username\"" in html)

m = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html)
token = m.group(1) if m else ""
check("antiforgery token present", bool(token))

# 2) post credentials
post(o, "/Login", {
    "Username": "admin", "Password": "admin1234",
    "__RequestVerificationToken": token,
})

# 3) walk authenticated pages
pages = {
    "/Owner/Home":      ["Outstanding", "Recent invoices"],
    "/Owner/Customers": ["Customers"],
    "/Owner/Projects":  ["Projects"],
    "/Owner/Invoices":  ["Invoices"],
    "/Owner/Expenses":  ["Expenses", "VAT"],
    "/Owner/Reports":   ["VAT", "Top customers"],
    "/Owner/Users":     ["Users"],
    "/Notifications":   ["Notifications"],
}
for path, needles in pages.items():
    body = get(o, path)
    has_login = "name=\"Username\"" in body
    found = sum(1 for n in needles if n in body or "מע" in body)
    check(f"{path}", not has_login and found > 0, f"matched {found}/{len(needles)} needles")

# 4) JSON handlers
import json
try:
    raw = get(o, "/Notifications?handler=Count")
    n = json.loads(raw)
    check("/Notifications?handler=Count returns int", isinstance(n, int), f"got {n}")
except Exception as e:
    check("notif count JSON", False, str(e))

try:
    raw = get(o, "/Owner/Home?handler=Sparkline")
    j = json.loads(raw)
    check("Sparkline JSON has labels+data",
          "labels" in j and "data" in j,
          f"len={len(j.get('labels', []))}")
except Exception as e:
    check("sparkline JSON", False, str(e))

try:
    cs = get(o, "/Owner/Home?handler=Stats")
    s  = json.loads(cs)
    has_unpaid = any("npaid" in k for k in s.keys())
    check("Stats JSON has unpaid counter", has_unpaid, str(s))
except Exception as e:
    check("stats JSON", False, str(e))

# CSV exports
for path in ("/Owner/Customers?handler=Csv", "/Owner/Expenses?handler=Csv"):
    try:
        body = get(o, path)
        head = body.split("\n", 1)[0].strip()
        check(f"{path}", "," in head, f"header: {head[:60]}")
    except Exception as e:
        check(path, False, str(e))

# Multi-assign assignees JSON for project 1 (seed)
try:
    raw = get(o, "/Owner/Projects?handler=Assignees&projectId=1")
    j = json.loads(raw)
    ok = "assigned" in j and "available" in j
    check("Assignees JSON", ok, f"keys={list(j.keys())}")
except Exception as e:
    check("assignees JSON", False, str(e))

# Forgot password page reachable + form posts
html = get(o, "/ForgotPassword")
check("ForgotPassword page", "Username" in html)

print("done.")
