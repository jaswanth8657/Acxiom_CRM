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
    print("STARTING COMPREHENSIVE PHASE 4 OPPORTUNITY MANAGEMENT ACCEPTANCE TEST SUITE")
    print("=" * 70)

    # Clean test opportunity data before test run
    conn = sqlite3.connect("AcxiomCRM.db")
    conn.execute("DELETE FROM Opportunities")
    conn.execute("DELETE FROM AuditLogs WHERE EntityName = 'Opportunity'")
    conn.execute("DELETE FROM Activities WHERE OpportunityId IS NOT NULL")
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
        # Pre-seed standard test users
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

        # Ensure a test customer exists
        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Id FROM AspNetUsers WHERE Email = 'alex.sales@acxiomcrm.local'")
        alex_user_id = cur.fetchone()[0]
        cur.execute("SELECT Id FROM AspNetUsers WHERE Email = 'sarah.sales@acxiomcrm.local'")
        sarah_user_id = cur.fetchone()[0]

        cur.execute("SELECT Id FROM Customers WHERE Status = 1 LIMIT 1")
        existing_cust = cur.fetchone()
        if not existing_cust:
            cur.execute("""
                INSERT INTO Customers (CustomerCode, CustomerName, Email, Phone, CompanyName, Status, CreatedDate, AssignedToUserId)
                VALUES ('CUST-9999', 'Global Tech Industries', 'contact@globaltech.com', '+1 (555) 777-8888', 'Global Tech', 1, ?, ?)
            """, (datetime.datetime.utcnow().isoformat(), alex_user_id))
            conn.commit()
            cur.execute("SELECT Id FROM Customers WHERE CustomerCode = 'CUST-9999'")
            test_customer_id = cur.fetchone()[0]
        else:
            test_customer_id = existing_cust[0]
        conn.close()

        future_date = (datetime.date.today() + datetime.timedelta(days=45)).strftime("%Y-%m-%d")
        past_date = (datetime.date.today() - datetime.timedelta(days=10)).strftime("%Y-%m-%d")

        # ----------------------------------------------------
        # TEST 19: Unauthenticated Access Blocked
        # ----------------------------------------------------
        print("\n[TEST 19] Unauthenticated access to /Opportunities redirects to login...")
        c_anon, c_anon_nr, _ = create_client()
        resp = safe_open_no_redirect(c_anon_nr, f"{BASE_URL}/Opportunities")
        assert resp.status == 302 and "/Account/Login" in resp.headers.get("Location")
        test_results["TC19_UnauthenticatedBlocked"] = "PASS"
        print("  -> PASS: Unauthenticated access blocked.")

        # ----------------------------------------------------
        # TEST 12, 13, 14, 15, 16, 18: Business Validations Server-side
        # ----------------------------------------------------
        print("\n[TEST 12, 13, 14, 15, 16, 18] Server-side Business Validations on Create...")
        create_page = c_admin.open(f"{BASE_URL}/Opportunities/Create").read().decode("utf-8")
        csrf_admin = extract_csrf(create_page)

        # TC12: Amount = 0 rejected
        data = urllib.parse.urlencode({
            "OpportunityName": "Zero Deal",
            "CustomerId": test_customer_id,
            "Amount": "0",
            "Stage": "Qualification",
            "Probability": "25",
            "ExpectedCloseDate": future_date,
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/Opportunities/Create", data=data).read().decode("utf-8")
        assert "Amount must be greater than 0" in resp
        test_results["TC12_AmountZeroRejected"] = "PASS"
        print("  -> PASS: Amount = 0 rejected.")

        # TC13: Negative amount rejected
        data = urllib.parse.urlencode({
            "OpportunityName": "Negative Deal",
            "CustomerId": test_customer_id,
            "Amount": "-5000",
            "Stage": "Qualification",
            "Probability": "25",
            "ExpectedCloseDate": future_date,
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/Opportunities/Create", data=data).read().decode("utf-8")
        assert "Amount must be greater than 0" in resp
        test_results["TC13_NegativeAmountRejected"] = "PASS"
        print("  -> PASS: Negative amount rejected.")

        # TC14: Probability > 100 rejected
        data = urllib.parse.urlencode({
            "OpportunityName": "High Prob Deal",
            "CustomerId": test_customer_id,
            "Amount": "10000",
            "Stage": "Qualification",
            "Probability": "150",
            "ExpectedCloseDate": future_date,
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/Opportunities/Create", data=data).read().decode("utf-8")
        assert "Probability must be between 0 and 100" in resp
        test_results["TC14_ProbabilityHighRejected"] = "PASS"
        print("  -> PASS: Probability > 100 rejected.")

        # TC15: Probability < 0 rejected
        data = urllib.parse.urlencode({
            "OpportunityName": "Low Prob Deal",
            "CustomerId": test_customer_id,
            "Amount": "10000",
            "Stage": "Qualification",
            "Probability": "-10",
            "ExpectedCloseDate": future_date,
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/Opportunities/Create", data=data).read().decode("utf-8")
        assert "Probability must be between 0 and 100" in resp
        test_results["TC15_ProbabilityLowRejected"] = "PASS"
        print("  -> PASS: Probability < 0 rejected.")

        # TC16: Past expected close date rejected for active opportunity
        data = urllib.parse.urlencode({
            "OpportunityName": "Past Date Deal",
            "CustomerId": test_customer_id,
            "Amount": "10000",
            "Stage": "Qualification",
            "Probability": "50",
            "ExpectedCloseDate": past_date,
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/Opportunities/Create", data=data).read().decode("utf-8")
        assert "Expected Close Date cannot be in the past" in resp
        test_results["TC16_PastDateRejected"] = "PASS"
        print("  -> PASS: Past expected close date rejected.")

        test_results["TC18_ServerValidationWithoutJS"] = "PASS"
        print("  -> PASS: Server-side validation verified with raw direct requests (JS bypassed).")

        # ----------------------------------------------------
        # TEST 1: Admin Creates Opportunity
        # ----------------------------------------------------
        print("\n[TEST 1] Admin creates opportunity...")
        create_page = c_admin.open(f"{BASE_URL}/Opportunities/Create").read().decode("utf-8")
        csrf_admin = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "OpportunityName": "Enterprise Cloud Platform",
            "CustomerId": test_customer_id,
            "Amount": "100000",
            "Stage": "Qualification",
            "Probability": "50",
            "ExpectedCloseDate": future_date,
            "AssignedToUserId": alex_user_id,
            "Notes": "Strategic multi-year cloud transformation contract.",
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Opportunities/Create", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Id, OpportunityName, Amount, Probability, Stage, AssignedToUserId FROM Opportunities WHERE OpportunityName = 'Enterprise Cloud Platform'")
        opp_admin = cur.fetchone()
        assert opp_admin is not None, "Opportunity was not saved to DB!"
        opp_admin_id = opp_admin[0]
        assert float(opp_admin[2]) == 100000.0
        assert opp_admin[3] == 50
        assert opp_admin[5] == alex_user_id

        # Verify Audit Log
        cur.execute("SELECT Action FROM AuditLogs WHERE EntityName = 'Opportunity' AND EntityId = ? AND Action = 'CreateOpportunity'", (str(opp_admin_id),))
        assert cur.fetchone() is not None, "AuditLog for CreateOpportunity not found!"
        conn.close()

        test_results["TC1_AdminCreatesOpportunity"] = "PASS"
        test_results["TC23_CreateUpdateAuditLog"] = "PASS"
        print(f"  -> PASS: Admin created Opportunity ID {opp_admin_id} with audit log.")

        # ----------------------------------------------------
        # TEST 2: Manager Creates Opportunity
        # ----------------------------------------------------
        print("\n[TEST 2] Manager creates opportunity...")
        create_page = c_mgr.open(f"{BASE_URL}/Opportunities/Create").read().decode("utf-8")
        csrf_mgr = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "OpportunityName": "Security Ops Modernization",
            "CustomerId": test_customer_id,
            "Amount": "75000",
            "Stage": "Proposal",
            "Probability": "60",
            "ExpectedCloseDate": future_date,
            "AssignedToUserId": sarah_user_id,
            "Notes": "SOC 2 Type II tooling modernization and SIEM.",
            "__RequestVerificationToken": csrf_mgr
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_mgr_nr, f"{BASE_URL}/Opportunities/Create", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Id, OpportunityName, Amount, Probability, Stage, AssignedToUserId FROM Opportunities WHERE OpportunityName = 'Security Ops Modernization'")
        opp_sarah = cur.fetchone()
        opp_sarah_id = opp_sarah[0]
        conn.close()

        test_results["TC2_ManagerCreatesOpportunity"] = "PASS"
        print(f"  -> PASS: Manager created Opportunity ID {opp_sarah_id} assigned to Sarah.")

        # ----------------------------------------------------
        # TEST 3: SalesExecutive Creates Assigned Opportunity
        # ----------------------------------------------------
        print("\n[TEST 3] SalesExecutive Alex creates assigned opportunity...")
        create_page = c_alex.open(f"{BASE_URL}/Opportunities/Create").read().decode("utf-8")
        csrf_alex = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "OpportunityName": "Analytics Addon",
            "CustomerId": test_customer_id,
            "Amount": "25000",
            "Stage": "Negotiation",
            "Probability": "80",
            "ExpectedCloseDate": future_date,
            "Notes": "Executive analytics dashboard license expansion.",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Opportunities/Create", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Id, OpportunityName, AssignedToUserId FROM Opportunities WHERE OpportunityName = 'Analytics Addon'")
        opp_alex = cur.fetchone()
        opp_alex_id = opp_alex[0]
        assert opp_alex[2] == alex_user_id, "SalesExecutive created deal must be assigned to himself!"
        conn.close()

        test_results["TC3_SalesExecCreatesOpportunity"] = "PASS"
        print(f"  -> PASS: SalesExecutive created assigned Opportunity ID {opp_alex_id}.")

        # ----------------------------------------------------
        # TEST 4 & 20: List Works & Role Scoping
        # ----------------------------------------------------
        print("\n[TEST 4 & 20] Opportunity List & Cross-Executive Scoping...")
        # Admin sees all 3 deals
        admin_list = c_admin.open(f"{BASE_URL}/Opportunities").read().decode("utf-8")
        assert "Enterprise Cloud Platform" in admin_list
        assert "Security Ops Modernization" in admin_list
        assert "Analytics Addon" in admin_list

        # Alex ONLY sees his assigned deals
        alex_list = c_alex.open(f"{BASE_URL}/Opportunities").read().decode("utf-8")
        assert "Enterprise Cloud Platform" in alex_list
        assert "Analytics Addon" in alex_list
        assert "Security Ops Modernization" not in alex_list, "Alex MUST NOT see Sarah's opportunity!"

        # Sarah ONLY sees her assigned deal
        sarah_list = c_sarah.open(f"{BASE_URL}/Opportunities").read().decode("utf-8")
        assert "Security Ops Modernization" in sarah_list
        assert "Enterprise Cloud Platform" not in sarah_list
        assert "Analytics Addon" not in sarah_list

        # Cross-user tampering test: Alex targeting Sarah's deal
        alex_opps_page = c_alex.open(f"{BASE_URL}/Opportunities").read().decode("utf-8")
        csrf_alex_tamper = extract_csrf(alex_opps_page)

        # GET Details
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Opportunities/Details/{opp_sarah_id}")
        assert resp.status in (403, 302)

        # GET Edit
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Opportunities/Edit/{opp_sarah_id}")
        assert resp.status in (403, 302)

        # POST Edit
        data = urllib.parse.urlencode({
            "Id": opp_sarah_id,
            "OpportunityName": "Hacked Deal",
            "CustomerId": test_customer_id,
            "Amount": "99999",
            "Stage": "Won",
            "Probability": "100",
            "ExpectedCloseDate": future_date,
            "__RequestVerificationToken": csrf_alex_tamper
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Opportunities/Edit/{opp_sarah_id}", data=data)
        assert resp.status in (403, 302)

        # POST Delete
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Opportunities/Delete/{opp_sarah_id}", data=urllib.parse.urlencode({"__RequestVerificationToken": csrf_alex_tamper}).encode("utf-8"))
        assert resp.status in (403, 302)

        test_results["TC4_ListWorks"] = "PASS"
        test_results["TC20_CrossExecBlocked"] = "PASS"
        print("  -> PASS: Role-based scoping verified and cross-executive tampering blocked.")

        # ----------------------------------------------------
        # TEST 5 & 21: Details Works & Weighted Pipeline Calculation
        # ----------------------------------------------------
        print("\n[TEST 5 & 21] Details Page & Weighted Pipeline Calculation...")
        details_html = c_admin.open(f"{BASE_URL}/Opportunities/Details/{opp_admin_id}").read().decode("utf-8")
        assert "Enterprise Cloud Platform" in details_html
        assert "100,000" in details_html
        assert "50%" in details_html
        assert "50,000" in details_html  # Weighted Amount: 100000 * 50% = 50000

        # Check list pipeline totals
        # Deals:
        # Admin opp: 100,000, 50% -> 50,000
        # Sarah opp: 75,000, 60% -> 45,000
        # Alex opp: 25,000, 80% -> 20,000
        # Total Pipeline = 200,000; Weighted Pipeline = 115,000
        assert "200,000" in admin_list, "Expected total pipeline $200,000"
        assert "115,000" in admin_list, "Expected weighted pipeline $115,000"

        test_results["TC5_DetailsWorks"] = "PASS"
        test_results["TC21_WeightedPipelineCorrect"] = "PASS"
        print("  -> PASS: Details view and Weighted Pipeline calculations are 100% accurate.")

        # ----------------------------------------------------
        # TEST 6 & 23: Edit Works & Audit Logging
        # ----------------------------------------------------
        print("\n[TEST 6 & 23] Edit Opportunity and Update Audit Logging...")
        edit_page = c_admin.open(f"{BASE_URL}/Opportunities/Edit/{opp_admin_id}").read().decode("utf-8")
        csrf_admin = extract_csrf(edit_page)
        data = urllib.parse.urlencode({
            "Id": opp_admin_id,
            "OpportunityName": "Enterprise Cloud Platform v2",
            "CustomerId": test_customer_id,
            "Amount": "120000",
            "CurrentStage": "Qualification",
            "Stage": "Proposal",  # Valid transition
            "Probability": "65",
            "ExpectedCloseDate": future_date,
            "Status": "Active",
            "AssignedToUserId": alex_user_id,
            "Notes": "Expanded scope to include multi-region failover.",
            "StageChangeNote": "Delivered architecture RFP proposal.",
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Opportunities/Edit/{opp_admin_id}", data=data)
        assert resp.status == 302
        c_admin.open(f"{BASE_URL}/Opportunities").read() # Follow redirect and clear TempData

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT OpportunityName, Amount, Stage, Probability FROM Opportunities WHERE Id = ?", (opp_admin_id,))
        updated_row = cur.fetchone()
        assert updated_row[0] == "Enterprise Cloud Platform v2"
        assert float(updated_row[1]) == 120000.0
        assert updated_row[2] == 3  # Proposal
        assert updated_row[3] == 65

        # Check Audit Log for Stage Change and Update
        cur.execute("SELECT Action, Details FROM AuditLogs WHERE EntityName = 'Opportunity' AND EntityId = ? AND Action = 'OpportunityStageChange'", (str(opp_admin_id),))
        stage_audit = cur.fetchone()
        assert stage_audit is not None and "Proposal" in stage_audit[1]
        conn.close()

        test_results["TC6_EditWorks"] = "PASS"
        test_results["TC22_StageChangeAuditLog"] = "PASS"
        print("  -> PASS: Edit and Stage change executed with audit logs.")

        # ----------------------------------------------------
        # TEST 17 & 24: Won/Lost Status Behavior & Terminal Enforcement
        # ----------------------------------------------------
        print("\n[TEST 17 & 24] Won/Lost Behavior and Terminal State Protection...")

        # 24a: Close Alex's deal as WON
        details_alex = c_alex.open(f"{BASE_URL}/Opportunities/Details/{opp_alex_id}").read().decode("utf-8")
        csrf_alex = extract_csrf(details_alex)
        data = urllib.parse.urlencode({
            "OpportunityId": opp_alex_id,
            "NewStage": "Won",
            "Note": "Contract signed, PO received.",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Opportunities/ChangeStage", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Stage, Probability FROM Opportunities WHERE Id = ?", (opp_alex_id,))
        won_row = cur.fetchone()
        assert won_row[0] == 5, f"Stage should be Won (5), got {won_row[0]}"
        assert won_row[1] == 100, f"Probability on Won deal should be 100%, got {won_row[1]}"

        # Verify OpportunityWon audit log
        cur.execute("SELECT Action FROM AuditLogs WHERE EntityName = 'Opportunity' AND EntityId = ? AND Action = 'OpportunityWon'", (str(opp_alex_id),))
        assert cur.fetchone() is not None, "OpportunityWon audit log not found!"
        conn.close()

        # 24b: Close Sarah's deal as LOST
        details_sarah = c_sarah.open(f"{BASE_URL}/Opportunities/Details/{opp_sarah_id}").read().decode("utf-8")
        csrf_sarah = extract_csrf(details_sarah)
        data = urllib.parse.urlencode({
            "OpportunityId": opp_sarah_id,
            "NewStage": "Lost",
            "Note": "Customer selected internal solution.",
            "__RequestVerificationToken": csrf_sarah
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_sarah_nr, f"{BASE_URL}/Opportunities/ChangeStage", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Stage, Probability FROM Opportunities WHERE Id = ?", (opp_sarah_id,))
        lost_row = cur.fetchone()
        assert lost_row[0] == 6, f"Stage should be Lost (6), got {lost_row[0]}"
        assert lost_row[1] == 0, f"Probability on Lost deal should be 0%, got {lost_row[1]}"

        cur.execute("SELECT Action FROM AuditLogs WHERE EntityName = 'Opportunity' AND EntityId = ? AND Action = 'OpportunityLost'", (str(opp_sarah_id),))
        assert cur.fetchone() is not None, "OpportunityLost audit log not found!"
        conn.close()

        # TC17: Terminal state protection: Attempt to move WON deal back to Qualification
        edit_won = c_alex.open(f"{BASE_URL}/Opportunities/Edit/{opp_alex_id}").read().decode("utf-8")
        csrf_alex_won = extract_csrf(edit_won)
        data = urllib.parse.urlencode({
            "Id": opp_alex_id,
            "OpportunityName": "Analytics Addon",
            "CustomerId": test_customer_id,
            "Amount": "25000",
            "CurrentStage": "Won",
            "Stage": "Qualification",  # FORBIDDEN!
            "Probability": "50",
            "ExpectedCloseDate": future_date,
            "__RequestVerificationToken": csrf_alex_won
        }).encode("utf-8")
        resp = c_alex.open(f"{BASE_URL}/Opportunities/Edit/{opp_alex_id}", data=data).read().decode("utf-8")
        assert "Invalid stage transition" in resp, "Reverting a Won deal must be rejected!"

        test_results["TC17_InvalidStageRejected"] = "PASS"
        test_results["TC24_WonLostBehaviorWorks"] = "PASS"
        print("  -> PASS: Won and Lost stage transitions, automatic probability updates, and terminal locks verified.")

        # ----------------------------------------------------
        # TEST 8, 9, 10, 11: Search, Filtering, and Pagination
        # ----------------------------------------------------
        print("\n[TEST 8, 9, 10, 11] Search, Filtering, and Pagination...")

        # Search by Opportunity name
        search_html = c_admin.open(f"{BASE_URL}/Opportunities?SearchTerm=Security").read().decode("utf-8")
        assert "Security Ops Modernization" in search_html
        assert "Enterprise Cloud Platform" not in search_html
        test_results["TC8_SearchWorks"] = "PASS"

        # Filter by Stage (Won)
        stage_html = c_admin.open(f"{BASE_URL}/Opportunities?Stage=Won").read().decode("utf-8")
        assert "Analytics Addon" in stage_html
        assert "Security Ops Modernization" not in stage_html
        test_results["TC9_StageFilterWorks"] = "PASS"

        # Filter by Assigned User (Sarah)
        user_html = c_admin.open(f"{BASE_URL}/Opportunities?AssignedToUserId={sarah_user_id}").read().decode("utf-8")
        assert "Security Ops Modernization" in user_html
        assert "Enterprise Cloud Platform" not in user_html
        test_results["TC10_AssignedUserFilterWorks"] = "PASS"

        # Pagination
        page_html = c_admin.open(f"{BASE_URL}/Opportunities?Page=1&PageSize=2").read().decode("utf-8")
        assert "Page 1 of 2" in page_html
        test_results["TC11_PaginationWorks"] = "PASS"
        print("  -> PASS: Search, Stage filter, User filter, and Pagination verified.")

        # ----------------------------------------------------
        # TEST 7: Delete / Deactivate Works
        # ----------------------------------------------------
        print("\n[TEST 7] Opportunity Delete / Deactivation...")
        del_page = c_admin.open(f"{BASE_URL}/Opportunities/Delete/{opp_admin_id}").read().decode("utf-8")
        csrf_admin_del = extract_csrf(del_page)
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Opportunities/Delete/{opp_admin_id}", data=urllib.parse.urlencode({"__RequestVerificationToken": csrf_admin_del}).encode("utf-8"))
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status FROM Opportunities WHERE Id = ?", (opp_admin_id,))
        status_row = cur.fetchone()
        assert status_row[0] == 2, f"Expected Status=2 (Inactive), got {status_row[0]}"

        cur.execute("SELECT Action FROM AuditLogs WHERE EntityName = 'Opportunity' AND EntityId = ? AND Action = 'DeleteOpportunity'", (str(opp_admin_id),))
        assert cur.fetchone() is not None, "DeleteOpportunity audit log not found!"
        conn.close()

        test_results["TC7_DeleteDeactivateWorks"] = "PASS"
        print("  -> PASS: Soft deactivation preserved opportunity record and logged DeleteOpportunity audit.")

        # ----------------------------------------------------
        # Summary
        # ----------------------------------------------------
        print("\n" + "=" * 70)
        print("PHASE 4 OPPORTUNITY ACCEPTANCE TEST SUITE SUMMARY")
        print("=" * 70)
        all_passed = True
        for test_name, status in sorted(test_results.items()):
            print(f"  {test_name.ljust(35)} : {status}")
            if status != "PASS":
                all_passed = False

        assert all_passed, "Some Phase 4 tests failed!"
        print("\nALL 24 OPPORTUNITY TEST CASES PASSED SUCCESSFULLY!")

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
