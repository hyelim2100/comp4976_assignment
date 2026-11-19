"""What each admin account can see in MEMS.Admin (server-prerendered HTML).

Run with the Aspire AppHost started:  python tests/e2e/admin-scope.py

Logs in through the real login form (antiforgery + cookie), fetches pages and checks that no
other tenant's municipality or candidate names appear anywhere in the returned HTML.
"""
import html
import http.cookiejar
import json
import re
import ssl
import sys
import urllib.error
import urllib.parse
import urllib.request

import os
ADMIN = os.environ.get("ADMIN_URL", "https://localhost:7101/admin/")
API = os.environ.get("API_URL", "https://localhost:7201/api/")
CTX = ssl.create_default_context()
CTX.check_hostname = False
CTX.verify_mode = ssl.CERT_NONE


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, *args, **kwargs):
        return None


def api(path):
    with urllib.request.urlopen(API + path, context=CTX) as r:
        return json.load(r)


class Session:
    def __init__(self):
        self.jar = http.cookiejar.CookieJar()
        self.opener = urllib.request.build_opener(
            urllib.request.HTTPSHandler(context=CTX), urllib.request.HTTPCookieProcessor(self.jar))
        self.raw = urllib.request.build_opener(
            urllib.request.HTTPSHandler(context=CTX), urllib.request.HTTPCookieProcessor(self.jar), NoRedirect())

    def get(self, path, follow=True):
        try:
            with (self.opener if follow else self.raw).open(ADMIN + path) as r:
                return r.status, r.geturl(), r.read().decode("utf-8", "replace")
        except urllib.error.HTTPError as e:
            return e.code, e.headers.get("Location", ""), e.read().decode("utf-8", "replace")

    def login(self, user, password="P@$$w0rd"):
        _, _, page = self.get("Account/Login")
        form = re.search(r"<form[^>]*>(.*?)</form>", page, re.S).group(1)
        fields = {m.group(1): html.unescape(m.group(2)) for m in re.finditer(r'<input[^>]*type="hidden"[^>]*name="([^"]+)"[^>]*value="([^"]*)"', form)}
        fields.update({"Input.UserName": user, "Input.Password": password})
        data = urllib.parse.urlencode(fields).encode()
        with self.opener.open(urllib.request.Request(ADMIN + "Account/Login", data=data)) as r:
            body = r.read().decode("utf-8", "replace")
        return "Invalid user name or password" not in body


def text(page):
    return html.unescape(re.sub(r"<[^>]+>", " ", page))


def cells(page, cls):
    return sorted({html.unescape(c.strip()) for c in re.findall(rf'class="{cls}"[^>]*>([^<]*)<', page)})


def count_of(page, element_id):
    m = re.search(rf'id="{element_id}"[^>]*>(\d+)<', page)
    return int(m.group(1)) if m else None


def options(page, select_id):
    m = re.search(rf'<select[^>]*id="{select_id}"[^>]*>(.*?)</select>', page, re.S)
    if not m:
        return None
    labels = re.findall(r'<optgroup label="([^"]+)"', m.group(1)) or re.findall(r"<option[^>]*>([^<]+)</option>", m.group(1))
    return [html.unescape(l.strip()) for l in labels]


# ---- reference data from the public API ----
directory = api("municipalities")
by_slug = {m["slug"]: m for m in directory}
candidates = {m["slug"]: api(f'{m["slug"]}/candidates') for m in directory}
positions = {m["slug"]: api(f'{m["slug"]}/positions') for m in directory}


def leaks(pages, own_slug):
    """Other tenants' municipality / candidate names found in any page."""
    found = set()
    blob = "\n".join(text(p) for p in pages)
    for slug, m in by_slug.items():
        if slug == own_slug:
            continue
        if re.search(rf"\b{re.escape(m['name'])}\b", blob):
            found.add(m["name"])
        for c in candidates[slug]:
            if c["name"] in blob:
                found.add(c["name"])
    return sorted(found)


def scope_report(user, own_slug):
    s = Session()
    assert s.login(user), f"login failed for {user}"
    other = next(sl for sl in by_slug if sl != own_slug) if own_slug else None
    pages = {}
    pages["dashboard"] = s.get("")[2]
    pages["municipalities"] = s.get("municipalities")[2]
    pages["positions"] = s.get("positions")[2]
    pages["candidates"] = s.get("candidates")[2]
    pages["candidate-create"] = s.get("candidates/create")[2]
    pages["position-create"] = s.get("positions/create")[2]
    row = {
        "user": user,
        "dashboard": (count_of(pages["dashboard"], "total-municipalities"), count_of(pages["dashboard"], "total-positions"), count_of(pages["dashboard"], "total-candidates")),
        "scope": re.search(r'id="dashboard-scope"[^>]*>([^<]*)<', pages["dashboard"]).group(1),
        "municipality list": re.findall(r'<a href="municipalities/\d+">([^<]+)</a>', pages["municipalities"]),
        "positions: municipalities": cells(pages["positions"], "municipality-cell"),
        "candidates: municipalities": cells(pages["candidates"], "municipality-cell"),
        "candidate count": len(re.findall(r'<td><a href="candidates/\d+">', pages["candidates"])),
        "position dropdown groups": options(pages["candidate-create"], "c-position"),
        "candidate filter: municipality dropdown": options(pages["candidates"], "filter-municipality"),
    }
    if own_slug:
        oc = candidates[other][0]
        op = positions[other][0]
        om = by_slug[other]
        direct = {
            f"candidates/{oc['id']} ({other})": s.get(f"candidates/{oc['id']}")[2],
            f"candidates/{oc['id']}/edit": s.get(f"candidates/{oc['id']}/edit")[2],
            f"candidates/{oc['id']}/delete": s.get(f"candidates/{oc['id']}/delete")[2],
            f"positions/{op['id']}/edit ({other})": s.get(f"positions/{op['id']}/edit")[2],
            f"municipalities/{om['id']} ({other})": s.get(f"municipalities/{om['id']}")[2],
            f"municipalities/{om['id']}/edit": s.get(f"municipalities/{om['id']}/edit")[2],
        }
        row["direct URLs"] = {k: ("not found" if "not found" in v else "VISIBLE!") for k, v in direct.items()}
        pages.update(direct)
        su = {
            "users": s.get("users")[2],
            "municipalities/create": s.get("municipalities/create")[2],
            f"municipalities/{om['id']}/delete": s.get(f"municipalities/{om['id']}/delete")[2],
        }
        row["super-admin pages"] = {k: ("access denied" if "Access denied" in v or "only available to a Super Admin" in v else "OPEN!") for k, v in su.items()}
        pages.update(su)
        logo = om["logoImageId"]
        row["other tenant logo /images"] = s.get(f"images/{logo}")[0]
        code, location, _ = s.get("select-tenant?municipalityId=1", follow=False)
        row["select-tenant (super only)"] = f"{code} -> {location.split('/admin/')[-1]}"
        row["leaked other-tenant names (all pages)"] = leaks(pages.values(), own_slug)
        row["guide/ballot words in admin HTML"] = sorted({w.lower() for p in pages.values() for w in re.findall(r"\b(voting guide|add to guide|ballot|my guide)\b", text(p), re.I)})
    return row, s


rows = []
for user, slug in [("pm", "pitt-meadows"), ("burnabyadmin", "burnaby")]:
    r, _ = scope_report(user, slug)
    rows.append(r)

# Super Admin: "All municipalities", then working tenant = Burnaby
r_all, s_super = scope_report("Super", None)
r_all["user"] = "Super (All municipalities)"
rows.append(r_all)
s_super.get(f"select-tenant?municipalityId={by_slug['burnaby']['id']}&returnUrl=")
p = {k: s_super.get(k)[2] for k in ["", "positions", "candidates", "candidates/create", "municipalities"]}
rows.append({
    "user": "Super (working in Burnaby)",
    "dashboard": (count_of(p[""], "total-municipalities"), count_of(p[""], "total-positions"), count_of(p[""], "total-candidates")),
    "scope": re.search(r'id="dashboard-scope"[^>]*>([^<]*)<', p[""]).group(1),
    "municipality list": re.findall(r'<a href="municipalities/\d+">([^<]+)</a>', p["municipalities"]),
    "positions: municipalities": cells(p["positions"], "municipality-cell"),
    "candidates: municipalities": cells(p["candidates"], "municipality-cell"),
    "candidate count": len(re.findall(r'<td><a href="candidates/\d+">', p["candidates"])),
    "position dropdown groups": options(p["candidates/create"], "c-position"),
})

print(json.dumps(rows, indent=1, ensure_ascii=False))
