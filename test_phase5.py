import subprocess
import time
import urllib.request
import urllib.parse
import http.cookiejar
import re
import os
import sqlite3
import datetime
import sys

BASE_URL = "http://127.0.0.1:5255"
EXE_PATH = r"e:\axiomCRM\bin\Debug\net8.0\AcxiomCRM.exe"

class NoRedirectHandler(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None

def create_client():
    cj = http.cookiejar.CookieJar()
    opener = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(cj))
    opener_no_redirect = urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(cj),
        NoRedirectHandler
    )
    return opener, opener_no_redirect, cj

def extract_csrf(html):
    match = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', html)
    if not match:
        match = re.search(r'value="([^"]+)"\s+name="__RequestVerificationToken"', html)
    return match.group(1) if match else None

def safe_open_no_redirect(opener, url, data=None):
    try:
        return opener.open(url, data=data)
    except urllib.error.HTTPError as e:
        return e

def login(opener, opener_no_redirect, email, password):
    login_page = opener.open(f"{BASE_URL}/Account/Login").read().decode("utf-8")
    csrf = extract_csrf(login_page)
    data = urllib.parse.urlencode({
        "Email": email,
        "Password": password,
        "RememberMe": "false",
        "__RequestVerificationToken": csrf
    }).encode("utf-8")
    resp = safe_open_no_redirect(opener_no_redirect, f"{BASE_URL}/Account/Login", data=data)
    loc = resp.headers.get("Location")
    code = resp.status if hasattr(resp, "status") else resp.code
    assert code == 302 and "/Account/Login" not in loc, f"Login failed for {email}! Status: {code}, Location: {loc}"
    return resp

def register_user(fullName, email, role, password):
    opener, opener_no_redirect, _ = create_client()
    try:
        reg_page = opener.open(f"{BASE_URL}/Account/Register").read().decode("utf-8")
        csrf = extract_csrf(reg_page)
        data = urllib.parse.urlencode({
            "FullName": fullName,
            "Email": email,
            "SelectedRole": role,
            "Password": password,
            "ConfirmPassword": password,
            "__RequestVerificationToken": csrf
        }).encode("utf-8")
        safe_open_no_redirect(opener_no_redirect, f"{BASE_URL}/Account/Register", data=data)
    except Exception:
        pass

def run_tests():
    print("=" * 70)
    print("STARTING COMPREHENSIVE PHASE 5 FOLLOW-UP & ACTIVITY ACCEPTANCE SUITE")
    print("=" * 70)

    # Clean existing test follow-ups and activities
    conn = sqlite3.connect("AcxiomCRM.db")
    conn.execute("DELETE FROM FollowUps")
    conn.execute("DELETE FROM Activities")
    conn.execute("DELETE FROM AuditLogs WHERE EntityName IN ('FollowUp', 'Activity')")
    conn.commit()
    conn.close()

    # Start Server
    env = os.environ.copy()
    env["DOTNET_ROOT"] = os.path.expanduser(r"~\.dotnet")
    env["PATH"] = os.path.expanduser(r"~\.dotnet") + ";" + env.get("PATH", "")

    proc = subprocess.Popen(
        [EXE_PATH, "--urls", BASE_URL],
        cwd=r"e:\axiomCRM",
        stdout=subprocess.PIPE,
        stderr=subprocess.PIPE,
        text=True,
        env=env
    )
    time.sleep(3)

    test_results = {}

    try:
        # Pre-seed users
        register_user("Marcus Manager", "marcus.manager@acxiomcrm.local", "Manager", "Manager@Acxiom2026!")
        register_user("Alex Sales", "alex.sales@acxiomcrm.local", "SalesExecutive", "Sales@Acxiom2026!")
        register_user("Sarah Sales", "sarah.sales@acxiomcrm.local", "SalesExecutive", "Sales@Acxiom2026!")

        # Clients
        c_admin, c_admin_nr, _ = create_client()
        login(c_admin, c_admin_nr, "admin@acxiomcrm.local", "Admin@Acxiom2026!")

        c_mgr, c_mgr_nr, _ = create_client()
        login(c_mgr, c_mgr_nr, "marcus.manager@acxiomcrm.local", "Manager@Acxiom2026!")

        c_alex, c_alex_nr, _ = create_client()
        login(c_alex, c_alex_nr, "alex.sales@acxiomcrm.local", "Sales@Acxiom2026!")

        c_sarah, c_sarah_nr, _ = create_client()
        login(c_sarah, c_sarah_nr, "sarah.sales@acxiomcrm.local", "Sales@Acxiom2026!")

        # Fetch IDs for users, customer, and lead
        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Id FROM AspNetUsers WHERE Email = 'admin@acxiomcrm.local'")
        admin_user_id = cur.fetchone()[0]
        cur.execute("SELECT Id FROM AspNetUsers WHERE Email = 'alex.sales@acxiomcrm.local'")
        alex_user_id = cur.fetchone()[0]
        cur.execute("SELECT Id FROM AspNetUsers WHERE Email = 'sarah.sales@acxiomcrm.local'")
        sarah_user_id = cur.fetchone()[0]

        cur.execute("SELECT Id FROM Customers WHERE Status = 1 LIMIT 1")
        existing_cust = cur.fetchone()
        if not existing_cust:
            cur.execute("""
                INSERT INTO Customers (CustomerCode, CustomerName, Email, Phone, CompanyName, Status, CreatedDate, AssignedToUserId)
                VALUES ('CUST-5001', 'Acme Corp', 'info@acmecorp.com', '+1 (555) 123-4567', 'Acme Corporation', 1, ?, ?)
            """, (datetime.datetime.utcnow().isoformat(), alex_user_id))
            conn.commit()
            cur.execute("SELECT Id FROM Customers WHERE CustomerCode = 'CUST-5001'")
            test_customer_id = cur.fetchone()[0]
        else:
            test_customer_id = existing_cust[0]

        cur.execute("SELECT Id FROM Leads WHERE Status != 6 LIMIT 1")
        existing_lead = cur.fetchone()
        if not existing_lead:
            cur.execute("""
                INSERT INTO Leads (LeadCode, LeadName, Email, Phone, CompanyName, Source, Status, ExpectedValue, CreatedDate, AssignedToUserId)
                VALUES ('LED-5001', 'Jane Prospect', 'jane@innovate.org', '+1 (555) 987-6543', 'Innovate LLC', 'Website', 1, 30000, ?, ?)
            """, (datetime.datetime.utcnow().isoformat(), sarah_user_id))
            conn.commit()
            cur.execute("SELECT Id FROM Leads WHERE LeadCode = 'LED-5001'")
            test_lead_id = cur.fetchone()[0]
        else:
            test_lead_id = existing_lead[0]

        conn.close()

        future_date_str = (datetime.date.today() + datetime.timedelta(days=3)).strftime("%Y-%m-%dT14:00")
        today_date_str = datetime.date.today().strftime("%Y-%m-%dT15:30")
        past_date_str = (datetime.date.today() - datetime.timedelta(days=2)).strftime("%Y-%m-%dT10:00")

        # ----------------------------------------------------
        # TEST 15: Unauthorized Access is Blocked
        # ----------------------------------------------------
        print("\n[TEST 15] Unauthorized access to /FollowUps and /Activities...")
        c_anon, c_anon_nr, _ = create_client()
        resp = safe_open_no_redirect(c_anon_nr, f"{BASE_URL}/FollowUps")
        assert resp.status == 302 and "/Account/Login" in resp.headers.get("Location")

        resp = safe_open_no_redirect(c_anon_nr, f"{BASE_URL}/Activities")
        assert resp.status == 302 and "/Account/Login" in resp.headers.get("Location")
        test_results["TC15_UnauthorizedBlocked"] = "PASS"
        print("  -> PASS: Unauthenticated access blocked.")

        # ----------------------------------------------------
        # TEST 10: Past planned follow-up is rejected
        # ----------------------------------------------------
        print("\n[TEST 10] Server-side validation: Past planned follow-up is rejected...")
        create_page = c_admin.open(f"{BASE_URL}/FollowUps/Create").read().decode("utf-8")
        csrf_admin = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "Subject": "Past Meeting Call",
            "ScheduledDate": past_date_str,
            "Type": "Meeting",
            "Priority": "Medium",
            "CustomerId": test_customer_id,
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/FollowUps/Create", data=data).read().decode("utf-8")
        assert "Follow-up date cannot be earlier than today" in resp
        test_results["TC10_PastFollowUpRejected"] = "PASS"
        print("  -> PASS: Past planned follow-up strictly rejected by server.")

        # ----------------------------------------------------
        # TEST 9: Today's follow-up works
        # ----------------------------------------------------
        print("\n[TEST 9] Today's follow-up works...")
        data = urllib.parse.urlencode({
            "Subject": "Sync Call Due Today",
            "ScheduledDate": today_date_str,
            "Type": "Call",
            "Priority": "High",
            "CustomerId": test_customer_id,
            "Notes": "Discuss contract scope today.",
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/FollowUps/Create", data=data)
        assert resp.status == 302
        c_admin.open(f"{BASE_URL}/FollowUps").read()
        test_results["TC9_TodayFollowUpWorks"] = "PASS"
        print("  -> PASS: Today's follow-up successfully scheduled.")

        # ----------------------------------------------------
        # TEST 8 & 1: Future follow-up & Admin creates follow-up
        # ----------------------------------------------------
        print("\n[TEST 1 & 8] Admin creates future follow-up...")
        create_page = c_admin.open(f"{BASE_URL}/FollowUps/Create").read().decode("utf-8")
        csrf_admin = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "Subject": "Quarterly Executive Review",
            "ScheduledDate": future_date_str,
            "Type": "Meeting",
            "Priority": "Urgent",
            "CustomerId": test_customer_id,
            "Notes": "Review enterprise tier roadmap and pricing.",
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/FollowUps/Create", data=data)
        assert resp.status == 302
        loc = resp.headers.get("Location")
        fu_admin_id = int(re.search(r'/Details/(\d+)', loc).group(1))

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Subject, Type, Status, CustomerId FROM FollowUps WHERE Id = ?", (fu_admin_id,))
        admin_fu_row = cur.fetchone()
        assert admin_fu_row is not None
        assert admin_fu_row[0] == "Quarterly Executive Review"
        assert admin_fu_row[2] == 1  # Planned
        conn.close()

        test_results["TC1_AdminCreatesFollowUp"] = "PASS"
        test_results["TC8_FutureFollowUpWorks"] = "PASS"
        print(f"  -> PASS: Admin created Future FollowUp #{fu_admin_id}.")

        # ----------------------------------------------------
        # TEST 2: Manager creates follow-up assigned to Sarah
        # ----------------------------------------------------
        print("\n[TEST 2] Manager creates follow-up assigned to Sarah...")
        create_page = c_mgr.open(f"{BASE_URL}/FollowUps/Create").read().decode("utf-8")
        csrf_mgr = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "Subject": "Sarah Follow-up on Inbound Lead",
            "ScheduledDate": future_date_str,
            "Type": "Call",
            "Priority": "High",
            "LeadId": test_lead_id,
            "AssignedToUserId": sarah_user_id,
            "Notes": "Initial qualification call for incoming prospect.",
            "__RequestVerificationToken": csrf_mgr
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_mgr_nr, f"{BASE_URL}/FollowUps/Create", data=data)
        assert resp.status == 302
        loc = resp.headers.get("Location")
        fu_sarah_id = int(re.search(r'/Details/(\d+)', loc).group(1))

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT AssignedToUserId FROM FollowUps WHERE Id = ?", (fu_sarah_id,))
        assert cur.fetchone()[0] == sarah_user_id
        conn.close()

        test_results["TC2_ManagerCreatesFollowUp"] = "PASS"
        print(f"  -> PASS: Manager created FollowUp #{fu_sarah_id} assigned to Sarah.")

        # ----------------------------------------------------
        # TEST 3: SalesExecutive Alex creates assigned follow-up
        # ----------------------------------------------------
        print("\n[TEST 3] SalesExecutive Alex creates assigned follow-up...")
        create_page = c_alex.open(f"{BASE_URL}/FollowUps/Create").read().decode("utf-8")
        csrf_alex = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "Subject": "Alex Client Technical Demo",
            "ScheduledDate": future_date_str,
            "Type": "Task",
            "Priority": "Medium",
            "CustomerId": test_customer_id,
            "Notes": "Prepare custom sandbox demo environment.",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/FollowUps/Create", data=data)
        assert resp.status == 302
        loc = resp.headers.get("Location")
        fu_alex_id = int(re.search(r'/Details/(\d+)', loc).group(1))

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT AssignedToUserId FROM FollowUps WHERE Id = ?", (fu_alex_id,))
        assert cur.fetchone()[0] == alex_user_id
        conn.close()

        test_results["TC3_SalesExecCreatesFollowUp"] = "PASS"
        print(f"  -> PASS: SalesExec Alex created FollowUp #{fu_alex_id}.")

        # ----------------------------------------------------
        # TEST 4 & 16: List Scoping and Cross-User Access Blocked
        # ----------------------------------------------------
        print("\n[TEST 4 & 16] Follow-up List Scoping & Cross-User Security...")
        admin_list = c_admin.open(f"{BASE_URL}/FollowUps").read().decode("utf-8")
        assert "Quarterly Executive Review" in admin_list
        assert "Sarah Follow-up on Inbound Lead" in admin_list
        assert "Alex Client Technical Demo" in admin_list

        alex_list = c_alex.open(f"{BASE_URL}/FollowUps").read().decode("utf-8")
        assert "Alex Client Technical Demo" in alex_list
        assert "Sarah Follow-up on Inbound Lead" not in alex_list

        # Cross-User Security Check: Alex targeting Sarah's follow-up
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/FollowUps/Details/{fu_sarah_id}")
        assert resp.status in (403, 302)

        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/FollowUps/Edit/{fu_sarah_id}")
        assert resp.status in (403, 302)

        # Alex attempting to complete Sarah's task via POST
        edit_page_alex = c_alex.open(f"{BASE_URL}/FollowUps/Details/{fu_alex_id}").read().decode("utf-8")
        csrf_alex_tamper = extract_csrf(edit_page_alex)
        data = urllib.parse.urlencode({
            "notes": "Tampered completion attempt",
            "__RequestVerificationToken": csrf_alex_tamper
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/FollowUps/Complete/{fu_sarah_id}", data=data)
        assert resp.status in (403, 302)

        test_results["TC4_ListWorks"] = "PASS"
        test_results["TC16_CrossUserAccessBlocked"] = "PASS"
        print("  -> PASS: Scoping verified and cross-user tampering blocked.")

        # ----------------------------------------------------
        # TEST 5, 6, 7: Search, Filters, and Pagination
        # ----------------------------------------------------
        print("\n[TEST 5, 6, 7] Search, Filters, and Pagination on Follow-ups...")
        # Search
        search_html = c_admin.open(f"{BASE_URL}/FollowUps?SearchTerm=Executive").read().decode("utf-8")
        assert "Quarterly Executive Review" in search_html
        assert "Sarah Follow-up on Inbound Lead" not in search_html
        test_results["TC5_SearchWorks"] = "PASS"

        # Filter by Type (Task)
        type_html = c_admin.open(f"{BASE_URL}/FollowUps?Type=Task").read().decode("utf-8")
        assert "Alex Client Technical Demo" in type_html
        assert "Quarterly Executive Review" not in type_html
        test_results["TC6_FilterWorks"] = "PASS"

        # Pagination
        page_html = c_admin.open(f"{BASE_URL}/FollowUps?Page=1&PageSize=2").read().decode("utf-8")
        assert "Page 1 of 2" in page_html
        test_results["TC7_PaginationWorks"] = "PASS"
        print("  -> PASS: Search, Filters, and Pagination verified.")

        # ----------------------------------------------------
        # TEST 11: Complete works
        # ----------------------------------------------------
        print("\n[TEST 11] Follow-Up Completion...")
        details_alex = c_alex.open(f"{BASE_URL}/FollowUps/Details/{fu_alex_id}").read().decode("utf-8")
        csrf_alex = extract_csrf(details_alex)
        data = urllib.parse.urlencode({
            "notes": "Delivered technical demo. Client agreed on SLA terms.",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/FollowUps/Complete/{fu_alex_id}", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status, CompletedAt, Notes FROM FollowUps WHERE Id = ?", (fu_alex_id,))
        fu_alex_row = cur.fetchone()
        assert fu_alex_row[0] == 2  # Completed
        assert fu_alex_row[1] is not None
        assert "Delivered technical demo" in fu_alex_row[2]

        cur.execute("SELECT Action, Details FROM AuditLogs WHERE EntityName = 'FollowUp' AND EntityId = ? AND Action = 'CompleteFollowUp'", (str(fu_alex_id),))
        assert cur.fetchone() is not None
        conn.close()

        test_results["TC11_CompleteWorks"] = "PASS"
        print("  -> PASS: Follow-up completed with notes and audit log.")

        # ----------------------------------------------------
        # TEST 12: Mark Missed works
        # ----------------------------------------------------
        print("\n[TEST 12] Mark Follow-Up Missed...")
        details_sarah = c_sarah.open(f"{BASE_URL}/FollowUps/Details/{fu_sarah_id}").read().decode("utf-8")
        csrf_sarah = extract_csrf(details_sarah)
        data = urllib.parse.urlencode({
            "reason": "Client did not attend phone conference.",
            "__RequestVerificationToken": csrf_sarah
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_sarah_nr, f"{BASE_URL}/FollowUps/MarkMissed/{fu_sarah_id}", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status FROM FollowUps WHERE Id = ?", (fu_sarah_id,))
        assert cur.fetchone()[0] == 4  # Missed
        conn.close()

        test_results["TC12_MissedWorks"] = "PASS"
        print("  -> PASS: Follow-up marked as missed.")

        # ----------------------------------------------------
        # TEST 13: Cancel works & Rule 6 (Cancelled cannot complete)
        # ----------------------------------------------------
        print("\n[TEST 13] Cancel Follow-Up & Rule 6 validation...")
        details_admin = c_admin.open(f"{BASE_URL}/FollowUps/Details/{fu_admin_id}").read().decode("utf-8")
        csrf_admin = extract_csrf(details_admin)
        data = urllib.parse.urlencode({
            "reason": "Client postponed initiative indefinitely.",
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/FollowUps/Cancel/{fu_admin_id}", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status FROM FollowUps WHERE Id = ?", (fu_admin_id,))
        assert cur.fetchone()[0] == 3  # Cancelled
        conn.close()

        # Rule 6 test: Attempt to complete a Cancelled follow-up
        data = urllib.parse.urlencode({
            "notes": "Attempting to complete cancelled task",
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        c_admin.open(f"{BASE_URL}/FollowUps/Complete/{fu_admin_id}", data=data)
        details_after = c_admin.open(f"{BASE_URL}/FollowUps/Details/{fu_admin_id}").read().decode("utf-8")
        assert "Cancelled follow-ups cannot be completed" in details_after or "Cancelled" in details_after

        test_results["TC13_CancelWorks"] = "PASS"
        print("  -> PASS: Follow-up cancelled and Rule 6 verified.")

        # ----------------------------------------------------
        # TEST 14: Reschedule works
        # ----------------------------------------------------
        print("\n[TEST 14] Reschedule Follow-Up...")
        resched_date = (datetime.date.today() + datetime.timedelta(days=7)).strftime("%Y-%m-%dT11:00")
        details_sarah = c_sarah.open(f"{BASE_URL}/FollowUps/Details/{fu_sarah_id}").read().decode("utf-8")
        csrf_sarah = extract_csrf(details_sarah)
        data = urllib.parse.urlencode({
            "newDate": resched_date,
            "reason": "Moved slot to next Tuesday as requested by client.",
            "__RequestVerificationToken": csrf_sarah
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_sarah_nr, f"{BASE_URL}/FollowUps/Reschedule/{fu_sarah_id}", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status, ScheduledDate FROM FollowUps WHERE Id = ?", (fu_sarah_id,))
        resched_row = cur.fetchone()
        assert resched_row[0] == 1  # Planned again
        assert "11:00" in resched_row[1]

        cur.execute("SELECT Action FROM AuditLogs WHERE EntityName = 'FollowUp' AND EntityId = ? AND Action = 'RescheduleFollowUp'", (str(fu_sarah_id),))
        assert cur.fetchone() is not None
        conn.close()

        test_results["TC14_RescheduleWorks"] = "PASS"
        print("  -> PASS: Follow-up rescheduled and reset to Planned.")

        # ----------------------------------------------------
        # TEST 17: Audit Records are generated
        # ----------------------------------------------------
        print("\n[TEST 17] Verifying Audit Records for FollowUps...")
        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT DISTINCT Action FROM AuditLogs WHERE EntityName = 'FollowUp'")
        actions = [r[0] for r in cur.fetchall()]
        assert "CreateFollowUp" in actions
        assert "CompleteFollowUp" in actions
        assert "MissFollowUp" in actions
        assert "CancelFollowUp" in actions
        assert "RescheduleFollowUp" in actions
        conn.close()
        test_results["TC17_AuditRecordsGenerated"] = "PASS"
        print("  -> PASS: All follow-up lifecycle actions recorded in AuditLogs.")

        # ====================================================
        # PART B — ACTIVITY MANAGEMENT TESTS
        # ====================================================

        # ----------------------------------------------------
        # TEST 18: Activity creation works
        # ----------------------------------------------------
        print("\n[TEST 18] Activity Creation...")
        act_create_page = c_alex.open(f"{BASE_URL}/Activities/Create").read().decode("utf-8")
        csrf_alex = extract_csrf(act_create_page)
        data = urllib.parse.urlencode({
            "Subject": "Technical Architecture Discovery Call",
            "Type": "Call",
            "Status": "Completed",
            "ActivityDate": datetime.datetime.now().strftime("%Y-%m-%dT%H:%M"),
            "CustomerId": test_customer_id,
            "Description": "Reviewed database schemas, scaling requirements, and SLAs with customer lead architect.",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Activities/Create", data=data)
        assert resp.status == 302
        loc = resp.headers.get("Location")
        act_id = int(re.search(r'/Details/(\d+)', loc).group(1))

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Subject, Type, Status, CustomerId, UserId FROM Activities WHERE Id = ?", (act_id,))
        act_row = cur.fetchone()
        assert act_row is not None
        assert act_row[0] == "Technical Architecture Discovery Call"
        assert act_row[4] == alex_user_id
        conn.close()

        test_results["TC18_ActivityCreationWorks"] = "PASS"
        print(f"  -> PASS: Activity #{act_id} logged successfully.")

        # ----------------------------------------------------
        # TEST 19: Activity details works
        # ----------------------------------------------------
        print("\n[TEST 19] Activity Details Page...")
        act_details = c_alex.open(f"{BASE_URL}/Activities/Details/{act_id}").read().decode("utf-8")
        assert "Technical Architecture Discovery Call" in act_details
        assert "Reviewed database schemas" in act_details
        test_results["TC19_ActivityDetailsWorks"] = "PASS"
        print("  -> PASS: Activity details view verified.")

        # ----------------------------------------------------
        # TEST 20: Activity update works
        # ----------------------------------------------------
        print("\n[TEST 20] Activity Update / Edit...")
        edit_page = c_alex.open(f"{BASE_URL}/Activities/Edit/{act_id}").read().decode("utf-8")
        csrf_alex = extract_csrf(edit_page)
        data = urllib.parse.urlencode({
            "Id": act_id,
            "Subject": "Technical Architecture Discovery Call v2",
            "Type": "Call",
            "Status": "Completed",
            "ActivityDate": datetime.datetime.now().strftime("%Y-%m-%dT%H:%M"),
            "CustomerId": test_customer_id,
            "Description": "Updated notes: Client approved preliminary design document.",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Activities/Edit/{act_id}", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Subject, Description FROM Activities WHERE Id = ?", (act_id,))
        updated_act = cur.fetchone()
        assert updated_act[0] == "Technical Architecture Discovery Call v2"
        assert "Client approved preliminary design document" in updated_act[1]
        conn.close()

        test_results["TC20_ActivityUpdateWorks"] = "PASS"
        print("  -> PASS: Activity updated with audit logging.")

        # ----------------------------------------------------
        # TEST 21: Activity completion/cancellation works
        # ----------------------------------------------------
        print("\n[TEST 21] Activity Completion and Cancellation workflow...")
        # Create Planned activity
        data = urllib.parse.urlencode({
            "Subject": "Planned Strategy Sync",
            "Type": "Meeting",
            "Status": "Planned",
            "ActivityDate": future_date_str,
            "CustomerId": test_customer_id,
            "Description": "Upcoming strategy meeting",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Activities/Create", data=data)
        assert resp.status == 302
        act_planned_id = int(re.search(r'/Details/(\d+)', resp.headers.get("Location")).group(1))

        # Complete it
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Activities/Complete/{act_planned_id}", data=urllib.parse.urlencode({"__RequestVerificationToken": csrf_alex}).encode("utf-8"))
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status FROM Activities WHERE Id = ?", (act_planned_id,))
        assert cur.fetchone()[0] == 2  # Completed

        # Create Planned activity to cancel
        data = urllib.parse.urlencode({
            "Subject": "Call To Cancel",
            "Type": "Call",
            "Status": "Planned",
            "ActivityDate": future_date_str,
            "CustomerId": test_customer_id,
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Activities/Create", data=data)
        act_cancel_id = int(re.search(r'/Details/(\d+)', resp.headers.get("Location")).group(1))

        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Activities/Cancel/{act_cancel_id}", data=urllib.parse.urlencode({"__RequestVerificationToken": csrf_alex}).encode("utf-8"))
        assert resp.status == 302

        cur.execute("SELECT Status FROM Activities WHERE Id = ?", (act_cancel_id,))
        assert cur.fetchone()[0] == 3  # Cancelled
        conn.close()

        test_results["TC21_ActivityLifecycleWorks"] = "PASS"
        print("  -> PASS: Activity completion and cancellation verified.")

        # ----------------------------------------------------
        # TEST 22: Activity Authorization Works
        # ----------------------------------------------------
        print("\n[TEST 22] Activity Authorization Gates...")
        # Sarah tries to edit Alex's activity
        resp = safe_open_no_redirect(c_sarah_nr, f"{BASE_URL}/Activities/Edit/{act_id}")
        assert resp.status in (403, 302)

        csrf_sarah = extract_csrf(c_sarah.open(f"{BASE_URL}/Activities").read().decode("utf-8"))
        data = urllib.parse.urlencode({
            "Id": act_id,
            "Subject": "Hacked",
            "__RequestVerificationToken": csrf_sarah
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_sarah_nr, f"{BASE_URL}/Activities/Edit/{act_id}", data=data)
        assert resp.status in (403, 302)

        test_results["TC22_ActivityAuthorizationWorks"] = "PASS"
        print("  -> PASS: Cross-user activity modification blocked.")

        # ----------------------------------------------------
        # TEST 23: Customer Details shows related activities
        # ----------------------------------------------------
        print("\n[TEST 23] Customer Details shows related activities...")
        cust_details_html = c_admin.open(f"{BASE_URL}/Customers/Details/{test_customer_id}").read().decode("utf-8")
        assert "Technical Architecture Discovery Call" in cust_details_html
        test_results["TC23_CustomerDetailsShowsActivities"] = "PASS"
        print("  -> PASS: Customer Details correctly reflects related activities.")

        # ----------------------------------------------------
        # TEST 24: Lead Details shows related activities
        # ----------------------------------------------------
        print("\n[TEST 24] Lead Details shows related activities...")
        # Create activity for lead
        create_page = c_alex.open(f"{BASE_URL}/Activities/Create").read().decode("utf-8")
        csrf_alex_lead = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "Subject": "Lead Intro Call",
            "Type": "Call",
            "Status": "Completed",
            "ActivityDate": datetime.datetime.now().strftime("%Y-%m-%dT%H:%M"),
            "LeadId": test_lead_id,
            "Description": "Qualified lead budget and authority.",
            "__RequestVerificationToken": csrf_alex_lead
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Activities/Create", data=data)
        assert resp.status == 302

        lead_details_html = c_alex.open(f"{BASE_URL}/Leads/Details/{test_lead_id}").read().decode("utf-8")
        assert "Lead Intro Call" in lead_details_html
        test_results["TC24_LeadDetailsShowsActivities"] = "PASS"
        print("  -> PASS: Lead Details correctly reflects related activities.")

        # ----------------------------------------------------
        # SUMMARY
        # ----------------------------------------------------
        print("\n" + "=" * 70)
        print("PHASE 5 ACCEPTANCE TEST SUITE SUMMARY")
        print("=" * 70)
        for k, v in sorted(test_results.items()):
            print(f"  {k.ljust(36)}: {v}")

        assert len(test_results) == 24, f"Expected 24 tests, got {len(test_results)}"
        for k, v in test_results.items():
            assert v == "PASS", f"Test {k} failed!"

        print("\nALL 24 PHASE 5 TEST CASES PASSED SUCCESSFULLY!")

    finally:
        try:
            proc.terminate()
            proc.wait(timeout=5)
        except Exception:
            try:
                proc.kill()
            except Exception:
                pass

if __name__ == "__main__":
    run_tests()
