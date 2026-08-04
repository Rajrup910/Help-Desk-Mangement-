"""End-to-end verification of the Help Desk API business rules against a running instance."""
import json, urllib.request, urllib.error, sys
from datetime import datetime

BASE = "http://localhost:5285"
results = []

def call(method, path, body=None):
    req = urllib.request.Request(BASE + path, method=method)
    data = None
    if body is not None:
        data = json.dumps(body).encode()
        req.add_header("Content-Type", "application/json")
    try:
        with urllib.request.urlopen(req, data) as r:
            raw = r.read().decode()
            return r.status, (json.loads(raw) if raw else None)
    except urllib.error.HTTPError as e:
        raw = e.read().decode()
        try:
            return e.code, json.loads(raw)
        except Exception:
            return e.code, raw

def check(name, cond, detail=""):
    results.append((name, bool(cond), detail))

def parse(ts):
    return datetime.fromisoformat(ts.replace("Z", "+00:00"))

def new_ticket(**over):
    body = {
        "title": "Printer jams on every multi-page job",
        "description": "The shared printer on the third floor jams whenever a job is longer than two pages.",
        "priority": "Medium", "category": "Hardware", "raisedBy": "Rajrup Roy Chowdhury",
    }
    body.update(over)
    st, t = call("POST", "/api/tickets", body)
    assert st == 201, (st, t)
    return t

created_ids = []

# --- 1. SLA windows -------------------------------------------------------
for prio, hours in [("Critical", 4), ("High", 24), ("Medium", 72), ("Low", 168)]:
    t = new_ticket(priority=prio); created_ids.append(t["id"])
    delta = (parse(t["dueDate"]) - parse(t["createdAt"])).total_seconds() / 3600
    check(f"SLA window for {prio} is {hours}h", abs(delta - hours) < 0.01, f"got {delta}h")

# --- 2. New tickets always start Open ------------------------------------
t = new_ticket(); created_ids.append(t["id"])
check("New ticket starts Open", t["status"] == "Open", t["status"])
check("New ticket has no resolvedAt", t.get("resolvedAt") is None)
check("Timestamps round-trip as UTC (Z suffix)", t["createdAt"].endswith("Z"), t["createdAt"])

# --- 3. Trimming and blank normalisation ---------------------------------
t = new_ticket(title="   Monitor flickers intermittently   ",
               raisedBy="  Rajrup Roy Chowdhury  ", assignedTo="   ")
created_ids.append(t["id"])
check("Title is trimmed", t["title"] == "Monitor flickers intermittently", repr(t["title"]))
check("RaisedBy is trimmed", t["raisedBy"] == "Rajrup Roy Chowdhury", repr(t["raisedBy"]))
check("Blank assignedTo becomes null", t.get("assignedTo") is None, repr(t.get("assignedTo")))

# --- 4/5/6. Status transition side effects -------------------------------
t = new_ticket(); tid = t["id"]; created_ids.append(tid)
st, r = call("PATCH", f"/api/tickets/{tid}/status", {"status": "Resolved", "changedBy": "Test Agent"})
check("Resolve stamps resolvedAt", r.get("resolvedAt") is not None, str(r.get("resolvedAt")))
first_resolved = r["resolvedAt"]

st, r = call("PATCH", f"/api/tickets/{tid}/status", {"status": "Closed", "changedBy": "Test Agent"})
check("Resolved->Closed keeps original resolvedAt", r["resolvedAt"] == first_resolved,
      f"{first_resolved} vs {r['resolvedAt']}")

st, r = call("PATCH", f"/api/tickets/{tid}/status", {"status": "Open", "changedBy": "Test Agent"})
check("Reopening clears resolvedAt", r.get("resolvedAt") is None, str(r.get("resolvedAt")))

# --- 7. Audit trail on status change with note ---------------------------
t = new_ticket(); tid = t["id"]; created_ids.append(tid)
call("PATCH", f"/api/tickets/{tid}/status",
     {"status": "InProgress", "changedBy": "Priya Patel", "note": "Ordered a replacement roller kit."})
st, comments = call("GET", f"/api/tickets/{tid}/comments")
bodies = [c["body"] for c in comments]
check("Status change is audited with actor and note",
      any("Priya Patel changed status from Open to In Progress" in b and "roller kit" in b for b in bodies),
      str(bodies))
check("Intake system comment exists", any("Ticket raised by" in b for b in bodies))
check("SLA time in audit uses colon separator (invariant culture)",
      any(":" in b and "SLA due" in b for b in bodies),
      str([b for b in bodies if "SLA due" in b]))

# --- 8. No-op status change adds no audit noise --------------------------
before = len(comments)
call("PATCH", f"/api/tickets/{tid}/status", {"status": "InProgress", "changedBy": "Priya Patel"})
st, comments2 = call("GET", f"/api/tickets/{tid}/comments")
check("Re-applying the same status adds no comment", len(comments2) == before,
      f"{before} -> {len(comments2)}")

# --- 9/10. Update: due date derivation -----------------------------------
t = new_ticket(priority="Low"); tid = t["id"]; created_ids.append(tid)
upd = {"title": t["title"], "description": t["description"], "priority": "Critical",
       "status": "Open", "category": t["category"], "raisedBy": t["raisedBy"],
       "assignedTo": "Hardware Support", "changedBy": "Test Agent"}
st, r = call("PUT", f"/api/tickets/{tid}", upd)
delta = (parse(r["dueDate"]) - parse(t["createdAt"])).total_seconds() / 3600
check("Priority change re-derives dueDate from creation time", abs(delta - 4) < 0.01, f"{delta}h")

st, comments = call("GET", f"/api/tickets/{tid}/comments")
bodies = [c["body"] for c in comments]
check("Priority change is audited", any("changed priority from Low to Critical" in b for b in bodies))
check("Assignment change is audited", any("assigned the ticket to Hardware Support" in b for b in bodies))

upd["dueDate"] = "2026-09-01T09:00:00Z"; upd["priority"] = "Low"
st, r = call("PUT", f"/api/tickets/{tid}", upd)
check("Explicit dueDate wins over the priority default",
      parse(r["dueDate"]) == parse("2026-09-01T09:00:00Z"), r["dueDate"])

# --- 11. Not found --------------------------------------------------------
st, _ = call("GET", "/api/tickets/999999")
check("GET missing ticket returns 404", st == 404, str(st))
st, _ = call("PUT", "/api/tickets/999999", upd)
check("PUT missing ticket returns 404", st == 404, str(st))
st, _ = call("DELETE", "/api/tickets/999999")
check("DELETE missing ticket returns 404", st == 404, str(st))

# --- 12/13/14. Comments ---------------------------------------------------
t = new_ticket(); tid = t["id"]
st, c = call("POST", f"/api/tickets/{tid}/comments",
             {"author": "Rajrup", "body": "Any progress on this?"})
check("User comment is created (201)", st == 201, str(st))
user_comment_id = c["id"]

st, comments = call("GET", f"/api/tickets/{tid}/comments")
system_id = next(x["id"] for x in comments if x["isSystem"])
st, _ = call("DELETE", f"/api/tickets/{tid}/comments/{system_id}")
check("System comments cannot be deleted", st == 404, str(st))

st, _ = call("DELETE", f"/api/tickets/{tid}/comments/{user_comment_id}")
check("User comments can be deleted (204)", st == 204, str(st))

st, _ = call("DELETE", f"/api/tickets/{tid}")
check("Ticket delete returns 204", st == 204, str(st))
st, _ = call("GET", f"/api/tickets/{tid}")
check("Deleted ticket is gone", st == 404, str(st))

# --- 15. Validation -------------------------------------------------------
st, r = call("POST", "/api/tickets",
             {"title": "abc", "description": "short", "raisedBy": "X", "raisedByEmail": "bad"})
check("Invalid payload returns 400", st == 400, str(st))
errs = r.get("errors", {}) if isinstance(r, dict) else {}
for field in ["Title", "Description", "RaisedBy", "RaisedByEmail"]:
    check(f"Validation reports {field}", field in errs, str(list(errs)))

# --- 16. LIKE wildcard escaping ------------------------------------------
t = new_ticket(title="Duplicate charge with 50% off applied",
               description="The invoice shows a 50% discount line twice for the same subscription item.")
created_ids.append(t["id"])
st, page = call("GET", "/api/tickets?search=50%25&pageSize=100")
check("Search treats '%' as a literal, not a wildcard",
      page["totalCount"] == 1 and page["items"][0]["id"] == t["id"],
      f"matched {page['totalCount']}")

st, page = call("GET", "/api/tickets?search=Rajrup&pageSize=100")
check("Search matches the requester name", page["totalCount"] > 0, str(page["totalCount"]))

# --- 17/18. Sort ordering -------------------------------------------------
st, page = call("GET", "/api/tickets?sortBy=Priority&sortDir=Asc&pageSize=100")
order = [x["priority"] for x in page["items"]]
rank = {"Critical": 0, "High": 1, "Medium": 2, "Low": 3}
check("Priority sorts by severity, not alphabetically",
      order == sorted(order, key=lambda p: rank[p]), str(order[:6]))

st, page = call("GET", "/api/tickets?sortBy=Status&sortDir=Asc&pageSize=100")
order = [x["status"] for x in page["items"]]
srank = {"Open": 0, "InProgress": 1, "Resolved": 2, "Closed": 3}
check("Status sorts in workflow order",
      order == sorted(order, key=lambda s: srank[s]), str(order[:6]))

# --- 19/20. Paging --------------------------------------------------------
st, p1 = call("GET", "/api/tickets?page=1&pageSize=3&sortBy=CreatedAt&sortDir=Desc")
st, p2 = call("GET", "/api/tickets?page=2&pageSize=3&sortBy=CreatedAt&sortDir=Desc")
ids = [x["id"] for x in p1["items"]] + [x["id"] for x in p2["items"]]
check("Pages do not overlap", len(ids) == len(set(ids)), str(ids))
check("Paging metadata is coherent",
      p1["totalPages"] == -(-p1["totalCount"] // 3) and p1["hasNext"] and not p1["hasPrevious"])

st, big = call("GET", "/api/tickets?pageSize=5000")
check("pageSize is clamped to 100", big["pageSize"] == 100, str(big["pageSize"]))
st, zero = call("GET", "/api/tickets?page=-3&pageSize=0")
check("page and pageSize are floored at 1", zero["page"] == 1 and zero["pageSize"] == 1,
      f"{zero['page']}/{zero['pageSize']}")

# --- 21. Filters ----------------------------------------------------------
st, page = call("GET", "/api/tickets?status=Open&pageSize=100")
check("Status filter returns only Open",
      all(x["status"] == "Open" for x in page["items"]), "mixed statuses")

st, page = call("GET", "/api/tickets?overdueOnly=true&pageSize=100")
check("Overdue filter excludes resolved/closed",
      all(x["status"] not in ("Resolved", "Closed") for x in page["items"]), "resolved leaked in")
check("Overdue filter returns only breached SLAs",
      all(x["isOverdue"] for x in page["items"]), "non-overdue leaked in")

st, page = call("GET", "/api/tickets?assignedTo=unassigned&pageSize=100")
check("Unassigned filter works",
      all(not x.get("assignedTo") for x in page["items"]), "assigned leaked in")

# --- 22. Stats ------------------------------------------------------------
st, stats = call("GET", "/api/tickets/stats")
st, all_t = call("GET", "/api/tickets?pageSize=100")
check("Stats total matches the ticket count",
      stats["total"] == all_t["totalCount"], f"{stats['total']} vs {all_t['totalCount']}")
check("Stats status buckets sum to the total",
      stats["open"] + stats["inProgress"] + stats["resolved"] + stats["closed"] == stats["total"])
check("Stats priority buckets sum to the total", sum(stats["byPriority"].values()) == stats["total"])
check("Stats category buckets sum to the total", sum(stats["byCategory"].values()) == stats["total"])
check("Trend covers exactly 7 days", len(stats["createdLast7Days"]) == 7,
      str(len(stats["createdLast7Days"])))
check("Overdue count matches the overdue filter",
      stats["overdue"] == len([x for x in all_t["items"] if x["isOverdue"]]))

# --- 23. Health & docs ----------------------------------------------------
st, h = call("GET", "/health")
check("Health endpoint reports Healthy", h["status"] == "Healthy", str(h))

# --- cleanup --------------------------------------------------------------
for i in created_ids:
    call("DELETE", f"/api/tickets/{i}")

# --- report ---------------------------------------------------------------
passed = sum(1 for _, ok, _ in results if ok)
for name, ok, detail in results:
    if not ok:
        print(f"FAIL  {name}  [{detail}]")
print(f"\n{passed}/{len(results)} checks passed")
sys.exit(0 if passed == len(results) else 1)
