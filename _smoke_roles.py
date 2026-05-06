"""Smoke-test sign-in for Employee + Client roles."""
import re, urllib.parse, http.cookiejar, urllib.request, json

BASE = "http://localhost:5050"

def login(user, pw):
    cj = http.cookiejar.CookieJar()
    o  = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))
    html = o.open(BASE + "/Login").read().decode("utf-8", "replace")
    tok  = re.search(r'name="__RequestVerificationToken"[^>]*value="([^"]+)"', html).group(1)
    body = urllib.parse.urlencode({
        "Username": user, "Password": pw, "__RequestVerificationToken": tok
    }).encode()
    req = urllib.request.Request(BASE + "/Login", data=body,
        headers={"Content-Type": "application/x-www-form-urlencoded"})
    resp = o.open(req)
    final_url = resp.geturl()
    body_text = resp.read().decode("utf-8", "replace")
    err = re.search(r'<p class="text-rose[^"]*">([^<]+)</p>', body_text)
    print(f"   login {user}: final={final_url} err={err.group(1) if err else 'none'}")
    return o

def fetch(o, path):
    return o.open(BASE + path).read().decode("utf-8", "replace")

for user, pw, expected in [("dana",  "admin1234",  "/Employee/Home"),
                            ("acme",  "admin1234",  "/Client/Portal")]:
    o = login(user, pw)
    home = fetch(o, expected)
    has_login = "name=\"Username\"" in home
    print(f"{'FAIL' if has_login else 'OK  '} {user} -> {expected} (size {len(home)})")

    # Bonus: client should see invoices, employee should see projects
    if user == "dana":
        print("    sees 'projects':",  "rojects"  in home or "פרויק" in home)
    if user == "acme":
        print("    sees 'invoices':",  "nvoices"  in home or "שבוני" in home)
