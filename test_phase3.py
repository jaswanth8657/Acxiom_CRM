import subprocess
import time
import urllib.request
import urllib.parse
import http.cookiejar
import re
import os
import sqlite3
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
    print("STARTING COMPREHENSIVE PHASE 3 LEAD MANAGEMENT ACCEPTANCE TEST SUITE")
    print("=" * 70)

    # Clean test lead/opportunity data before test run
    conn = sqlite3.connect("AcxiomCRM.db")
    conn.execute("DELETE FROM Opportunities")
    conn.execute("DELETE FROM Leads")
    conn.execute("DELETE FROM Customers WHERE CustomerCode LIKE 'CUST-%'")
    conn.execute("DELETE FROM AuditLogs WHERE EntityName IN ('Lead', 'Customer', 'Opportunity')")
    conn.execute("DELETE FROM Activities WHERE LeadId IS NOT NULL")
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
        # Pre-seed standard test users if needed
        register_user("Marcus Manager", "marcus.manager@acxiomcrm.local", "Manager", "Manager@Acxiom2026!")
        register_user("Alex Sales", "alex.sales@acxiomcrm.local", "SalesExecutive", "Sales@Acxiom2026!")
        register_user("Sarah Sales", "sarah.sales@acxiomcrm.local", "SalesExecutive", "Sales@Acxiom2026!")

        # ----------------------------------------------------
        # TEST 1: Unauthenticated Guard
        # ----------------------------------------------------
        print("\n[TEST 1] Unauthenticated access to /Leads redirects to /Account/Login...")
        c_anon, c_anon_nr, _ = create_client()
        resp = safe_open_no_redirect(c_anon_nr, f"{BASE_URL}/Leads")
        loc = resp.headers.get("Location")
        assert resp.status == 302 and "/Account/Login" in loc, f"Expected redirect to login, got {resp.status} to {loc}"
        test_results["TC1_UnauthenticatedGuard"] = "PASS"
        print("  -> PASS: Unauthenticated access properly blocked.")

        # ----------------------------------------------------
        # TEST 2: Admin & Executive Login & Access
        # ----------------------------------------------------
        print("\n[TEST 2] Authenticated access to /Leads by Admin and SalesExecutive...")
        c_admin, c_admin_nr, _ = create_client()
        login(c_admin, c_admin_nr, "admin@acxiomcrm.local", "Admin@Acxiom2026!")
        admin_leads = c_admin.open(f"{BASE_URL}/Leads").read().decode("utf-8")
        assert "Lead Management" in admin_leads, "Expected 'Lead Management' header on Admin /Leads page"

        c_alex, c_alex_nr, _ = create_client()
        login(c_alex, c_alex_nr, "alex.sales@acxiomcrm.local", "Sales@Acxiom2026!")
        alex_leads = c_alex.open(f"{BASE_URL}/Leads").read().decode("utf-8")
        assert "Lead Management" in alex_leads, "Expected 'Lead Management' header on SalesExecutive /Leads page"
        test_results["TC2_LeadsAccess"] = "PASS"
        print("  -> PASS: Both Admin and SalesExecutive can access /Leads.")

        # Get User IDs from DB
        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Id FROM AspNetUsers WHERE Email = 'alex.sales@acxiomcrm.local'")
        alex_user_id = cur.fetchone()[0]
        cur.execute("SELECT Id FROM AspNetUsers WHERE Email = 'sarah.sales@acxiomcrm.local'")
        sarah_user_id = cur.fetchone()[0]
        conn.close()

        # ----------------------------------------------------
        # TEST 3: Validation on Create (Server-side)
        # ----------------------------------------------------
        print("\n[TEST 3] Server-side validation enforcement on Lead Create...")
        create_page = c_admin.open(f"{BASE_URL}/Leads/Create").read().decode("utf-8")
        csrf = extract_csrf(create_page)

        # 3a: Empty LeadName
        data = urllib.parse.urlencode({
            "LeadName": "",
            "Email": "valid@email.com",
            "Source": "Website",
            "Status": "New",
            "__RequestVerificationToken": csrf
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/Leads/Create", data=data).read().decode("utf-8")
        assert "Lead Name is required" in resp, "Expected Lead Name is required validation message"

        # 3b: Invalid Email
        data = urllib.parse.urlencode({
            "LeadName": "John Doe",
            "Email": "not-an-email",
            "Source": "Website",
            "Status": "New",
            "__RequestVerificationToken": csrf
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/Leads/Create", data=data).read().decode("utf-8")
        assert "Enter a valid email address" in resp, "Expected valid email validation message"

        # 3c: Negative Expected Value
        data = urllib.parse.urlencode({
            "LeadName": "John Doe",
            "Email": "valid@email.com",
            "Source": "Website",
            "Status": "New",
            "ExpectedValue": "-500",
            "__RequestVerificationToken": csrf
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/Leads/Create", data=data).read().decode("utf-8")
        assert ("Expected Value cannot be negative" in resp) or ("Expected Value must be between" in resp), "Expected ExpectedValue validation message"
        test_results["TC3_ServerSideValidation"] = "PASS"
        print("  -> PASS: Server-side validation correctly rejects invalid inputs.")

        # ----------------------------------------------------
        # TEST 4: Valid Lead Creation & Auto Code Generation
        # ----------------------------------------------------
        print("\n[TEST 4] Valid Lead Creation with Code Generation and Audit Logging...")
        create_page = c_admin.open(f"{BASE_URL}/Leads/Create").read().decode("utf-8")
        csrf = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "LeadName": "John Tech",
            "Email": "john.tech@novasystems.io",
            "Phone": "+1 (555) 234-5678",
            "CompanyName": "Nova Systems",
            "Title": "CTO",
            "Source": "Website",
            "Status": "New",
            "ExpectedValue": "25000",
            "AssignedToUserId": alex_user_id,
            "__RequestVerificationToken": csrf
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Leads/Create", data=data)
        if resp.status != 302:
            body = resp.read().decode("utf-8")
            for err in re.findall(r'<span[^>]*field-validation-error[^>]*>(.*?)</span>', body):
                print("FIELD ERROR:", err)
            for err in re.findall(r'<div[^>]*validation-summary-errors[^>]*>(.*?)</div>', body, re.DOTALL):
                print("SUMMARY ERROR:", err)
            for err in re.findall(r'<div[^>]*alert-danger[^>]*>(.*?)</div>', body, re.DOTALL):
                print("ALERT DANGER:", err)
        assert resp.status == 302, f"Expected 302 redirect after creation, got {resp.status}"

        # Verify DB entry
        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Id, LeadCode, LeadName, Email, Status, ExpectedValue, AssignedToUserId FROM Leads WHERE Email = 'john.tech@novasystems.io'")
        lead_alex = cur.fetchone()
        assert lead_alex is not None, "Lead John Tech was not inserted into database!"
        lead_alex_id, lead_alex_code, _, _, lead_alex_status, lead_alex_val, lead_alex_assigned = lead_alex
        assert lead_alex_code.startswith("LED-"), f"Expected auto-generated code starting with LED-, got {lead_alex_code}"
        assert lead_alex_status == 1, f"Expected Status=1 (New), got {lead_alex_status}"
        assert lead_alex_assigned == alex_user_id, f"Expected AssignedToUserId={alex_user_id}, got {lead_alex_assigned}"

        # Verify Audit Log
        cur.execute("SELECT Action, EntityName, EntityId FROM AuditLogs WHERE EntityName = 'Lead' AND EntityId = ? AND Action = 'CreateLead'", (str(lead_alex_id),))
        audit = cur.fetchone()
        assert audit is not None, "AuditLog for CreateLead not found!"

        # Verify Initial Activity
        cur.execute("SELECT Subject FROM Activities WHERE LeadId = ?", (lead_alex_id,))
        activity = cur.fetchone()
        assert activity is not None and "Lead Created" in activity[0], "Activity for Lead Created not found!"
        conn.close()

        test_results["TC4_ValidLeadCreation"] = "PASS"
        print(f"  -> PASS: Lead John Tech created successfully ({lead_alex_code}, ID: {lead_alex_id}) with audit log and activity.")

        # ----------------------------------------------------
        # TEST 5: Duplicate Detection on Create
        # ----------------------------------------------------
        print("\n[TEST 5] Duplicate Lead Code & Email Rejection...")
        create_page = c_admin.open(f"{BASE_URL}/Leads/Create").read().decode("utf-8")
        csrf = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "LeadName": "Duplicate John",
            "Email": "john.tech@novasystems.io",
            "Source": "Website",
            "Status": "New",
            "__RequestVerificationToken": csrf
        }).encode("utf-8")
        resp = c_admin.open(f"{BASE_URL}/Leads/Create", data=data).read().decode("utf-8")
        assert "Email already exists" in resp, "Expected 'Email already exists' error message"
        test_results["TC5_DuplicateDetection"] = "PASS"
        print("  -> PASS: Duplicate email rejected.")

        # ----------------------------------------------------
        # TEST 6: Create Second Lead Assigned to Sarah
        # ----------------------------------------------------
        print("\n[TEST 6] Create second Lead assigned to Sarah...")
        create_page = c_admin.open(f"{BASE_URL}/Leads/Create").read().decode("utf-8")
        csrf = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "LeadName": "Elena Rostova",
            "Email": "elena.rostova@cyberdyne.com",
            "Phone": "+1 (555) 987-6543",
            "CompanyName": "Cyberdyne Systems",
            "Title": "VP Operations",
            "Source": "Referral",
            "Status": "New",
            "ExpectedValue": "45000",
            "AssignedToUserId": sarah_user_id,
            "__RequestVerificationToken": csrf
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Leads/Create", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Id, LeadCode FROM Leads WHERE Email = 'elena.rostova@cyberdyne.com'")
        lead_sarah = cur.fetchone()
        lead_sarah_id, lead_sarah_code = lead_sarah
        conn.close()
        test_results["TC6_SecondLeadCreation"] = "PASS"
        print(f"  -> PASS: Second Lead created ({lead_sarah_code}, ID: {lead_sarah_id}) assigned to Sarah.")

        # ----------------------------------------------------
        # TEST 7: Role Scoping & Visibility
        # ----------------------------------------------------
        print("\n[TEST 7] Role-based Visibility Scoping (Alex vs Sarah vs Manager vs Admin)...")
        # Alex views /Leads
        alex_html = c_alex.open(f"{BASE_URL}/Leads").read().decode("utf-8")
        assert "John Tech" in alex_html, "Alex SHOULD see his assigned lead John Tech"
        assert "Elena Rostova" not in alex_html, "Alex MUST NOT see Sarah's lead Elena Rostova!"

        # Sarah views /Leads
        c_sarah, c_sarah_nr, _ = create_client()
        login(c_sarah, c_sarah_nr, "sarah.sales@acxiomcrm.local", "Sales@Acxiom2026!")
        sarah_html = c_sarah.open(f"{BASE_URL}/Leads").read().decode("utf-8")
        assert "Elena Rostova" in sarah_html, "Sarah SHOULD see her assigned lead Elena Rostova"
        assert "John Tech" not in sarah_html, "Sarah MUST NOT see Alex's lead John Tech!"

        # Marcus (Manager) views /Leads
        c_mgr, c_mgr_nr, _ = create_client()
        login(c_mgr, c_mgr_nr, "marcus.manager@acxiomcrm.local", "Manager@Acxiom2026!")
        mgr_html = c_mgr.open(f"{BASE_URL}/Leads").read().decode("utf-8")
        assert "John Tech" in mgr_html and "Elena Rostova" in mgr_html, "Manager should see both leads!"

        # Admin views /Leads
        admin_html = c_admin.open(f"{BASE_URL}/Leads").read().decode("utf-8")
        assert "John Tech" in admin_html and "Elena Rostova" in admin_html, "Admin should see both leads!"

        test_results["TC7_RoleScoping"] = "PASS"
        print("  -> PASS: Scoping strictly filters leads per user role (SalesExecutives see only assigned leads).")

        # ----------------------------------------------------
        # TEST 8: Cross-User Security Gates (Tampering Prevention)
        # ----------------------------------------------------
        print("\n[TEST 8] Cross-User Security Access Tampering Prevention (Alex targeting Sarah's Lead)...")

        # 8a: GET Details
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Details/{lead_sarah_id}")
        assert resp.status in (403, 302), f"Expected 403 or 302 on unauthorized GET Details, got {resp.status}"
        if resp.status == 302:
            assert "/Account/AccessDenied" in resp.headers.get("Location")

        # 8b: GET Edit
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Edit/{lead_sarah_id}")
        assert resp.status in (403, 302), f"Expected 403 or 302 on unauthorized GET Edit, got {resp.status}"
        if resp.status == 302:
            assert "/Account/AccessDenied" in resp.headers.get("Location")

        # 8c: POST Edit tampering
        alex_leads_page = c_alex.open(f"{BASE_URL}/Leads").read().decode("utf-8")
        csrf_alex = extract_csrf(alex_leads_page)
        data = urllib.parse.urlencode({
            "Id": lead_sarah_id,
            "LeadCode": lead_sarah_code,
            "LeadName": "Hacked Name",
            "Email": "elena.rostova@cyberdyne.com",
            "Source": "Referral",
            "Status": "Qualified",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Edit/{lead_sarah_id}", data=data)
        assert resp.status in (403, 302), f"Expected 403 or 302 on unauthorized POST Edit, got {resp.status}"

        # 8d: GET Convert
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Convert/{lead_sarah_id}")
        assert resp.status in (403, 302), f"Expected 403 or 302 on unauthorized GET Convert, got {resp.status}"

        # 8e: POST Convert
        data = urllib.parse.urlencode({
            "LeadId": lead_sarah_id,
            "OpportunityTitle": "Unauthorized Deal",
            "OpportunityAmount": "1000",
            "ExpectedCloseDate": "2026-12-31",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Convert/{lead_sarah_id}", data=data)
        assert resp.status in (403, 302), f"Expected 403 or 302 on unauthorized POST Convert, got {resp.status}"

        # 8f: POST Delete
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Delete/{lead_sarah_id}", data=urllib.parse.urlencode({"__RequestVerificationToken": csrf_alex}).encode("utf-8"))
        assert resp.status in (403, 302), f"Expected 403 or 302 on unauthorized POST Delete, got {resp.status}"

        test_results["TC8_SecurityGates"] = "PASS"
        print("  -> PASS: All cross-user tampering attempts blocked by server-side authorization checks.")

        # ----------------------------------------------------
        # TEST 9: Controlled Status Transitions
        # ----------------------------------------------------
        print("\n[TEST 9] Controlled Status Transitions (New -> Contacted -> Qualified)...")

        # 9a: Alex advances John: New -> Contacted
        details_alex = c_alex.open(f"{BASE_URL}/Leads/Details/{lead_alex_id}").read().decode("utf-8")
        csrf_alex = extract_csrf(details_alex)
        data = urllib.parse.urlencode({
            "LeadId": lead_alex_id,
            "NewStatus": "Contacted",
            "Note": "Initial exploratory discovery call completed",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/ChangeStatus", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status FROM Leads WHERE Id = ?", (lead_alex_id,))
        assert cur.fetchone()[0] == 2, "Status should now be Contacted (2)"

        # Check AuditLog for status change
        cur.execute("SELECT Details FROM AuditLogs WHERE EntityName = 'Lead' AND EntityId = ? AND Action = 'LeadStatusChange'", (str(lead_alex_id),))
        status_audit = cur.fetchone()
        assert status_audit is not None and "Contacted" in status_audit[0], "AuditLog for LeadStatusChange not recorded"
        conn.close()

        # 9b: Alex advances John: Contacted -> Qualified
        details_alex = c_alex.open(f"{BASE_URL}/Leads/Details/{lead_alex_id}").read().decode("utf-8")
        csrf_alex = extract_csrf(details_alex)
        data = urllib.parse.urlencode({
            "LeadId": lead_alex_id,
            "NewStatus": "Qualified",
            "Note": "BANT qualification complete, prospect budget verified",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/ChangeStatus", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status FROM Leads WHERE Id = ?", (lead_alex_id,))
        assert cur.fetchone()[0] == 3, "Status should now be Qualified (3)"
        conn.close()

        test_results["TC9_ControlledTransitions"] = "PASS"
        print("  -> PASS: Status successfully progressed to Qualified with audit logs.")

        # ----------------------------------------------------
        # TEST 10: Invalid Status Transition Rejection
        # ----------------------------------------------------
        print("\n[TEST 10] Invalid Transition Prevention (Qualified -> New)...")
        edit_alex = c_alex.open(f"{BASE_URL}/Leads/Edit/{lead_alex_id}").read().decode("utf-8")
        csrf_alex = extract_csrf(edit_alex)
        data = urllib.parse.urlencode({
            "Id": lead_alex_id,
            "LeadCode": lead_alex_code,
            "LeadName": "John Tech",
            "Email": "john.tech@novasystems.io",
            "Source": "Website",
            "CurrentStatus": "Qualified",
            "Status": "New",  # FORBIDDEN!
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = c_alex.open(f"{BASE_URL}/Leads/Edit/{lead_alex_id}", data=data).read().decode("utf-8")
        assert "Invalid status transition" in resp, "Expected 'Invalid status transition' rejection message"

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status FROM Leads WHERE Id = ?", (lead_alex_id,))
        assert cur.fetchone()[0] == 3, "Status must remain Qualified!"
        conn.close()

        test_results["TC10_InvalidTransitionBlocked"] = "PASS"
        print("  -> PASS: Invalid status transition strictly blocked on server.")

        # ----------------------------------------------------
        # TEST 11: Lead Qualification Gate on Conversion
        # ----------------------------------------------------
        print("\n[TEST 11] Qualification Gate for Lead Conversion...")
        # Sarah's lead is currently in status 'New'
        resp = safe_open_no_redirect(c_sarah_nr, f"{BASE_URL}/Leads/Convert/{lead_sarah_id}")
        assert resp.status == 302, f"Expected redirect when attempting to convert unqualified lead, got {resp.status}"

        test_results["TC11_ConversionEligibility"] = "PASS"
        print("  -> PASS: Unqualified lead blocked from conversion.")

        # ----------------------------------------------------
        # TEST 12: Transactional Lead Conversion Execution
        # ----------------------------------------------------
        print("\n[TEST 12] Complete Lead Conversion Execution (Qualified Lead -> Customer + Opportunity)...")
        convert_page = c_alex.open(f"{BASE_URL}/Leads/Convert/{lead_alex_id}").read().decode("utf-8")
        assert "Convert Qualified Lead" in convert_page, "Expected Convert page"
        csrf_alex = extract_csrf(convert_page)

        data = urllib.parse.urlencode({
            "LeadId": lead_alex_id,
            "LeadCode": lead_alex_code,
            "LeadName": "John Tech",
            "Email": "john.tech@novasystems.io",
            "Phone": "+1 (555) 234-5678",
            "CompanyName": "Nova Systems",
            "ExpectedValue": "25000",
            "AssignedToUserId": alex_user_id,
            "OpportunityTitle": "Nova Systems - Cloud Migration Enterprise Deal",
            "OpportunityAmount": "25000",
            "OpportunityStage": "Prospecting",
            "ExpectedCloseDate": "2026-11-20",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Convert/{lead_alex_id}", data=data)
        assert resp.status == 302, f"Expected 302 redirect after conversion, got {resp.status}"

        # Verify DB Records
        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()

        # Check Lead record updated
        cur.execute("SELECT Status, ConvertedCustomerId, ConvertedOpportunityId, ConvertedDate FROM Leads WHERE Id = ?", (lead_alex_id,))
        lead_conv = cur.fetchone()
        assert lead_conv[0] == 5, f"Lead status should be Converted (5), got {lead_conv[0]}"
        cust_id = lead_conv[1]
        opp_id = lead_conv[2]
        assert cust_id is not None, "ConvertedCustomerId was not set on Lead!"
        assert opp_id is not None, "ConvertedOpportunityId was not set on Lead!"
        assert lead_conv[3] is not None, "ConvertedDate was not set on Lead!"

        # Check Customer record created
        cur.execute("SELECT CustomerName, Email, CompanyName, Status, AssignedToUserId FROM Customers WHERE Id = ?", (cust_id,))
        cust_row = cur.fetchone()
        assert cust_row is not None, f"Customer {cust_id} not found!"
        assert cust_row[0] == "John Tech"
        assert cust_row[1] == "john.tech@novasystems.io"
        assert cust_row[2] == "Nova Systems"
        assert cust_row[3] == 1  # Active

        # Check Opportunity record created
        try:
            cur.execute("SELECT OpportunityName, Amount, Stage, CustomerId, LeadId, AssignedToUserId FROM Opportunities WHERE Id = ?", (opp_id,))
        except sqlite3.OperationalError:
            cur.execute("SELECT Title, Amount, Stage, CustomerId, LeadId, AssignedToUserId FROM Opportunities WHERE Id = ?", (opp_id,))
        opp_row = cur.fetchone()
        assert opp_row is not None, f"Opportunity {opp_id} not found!"
        assert opp_row[0] == "Nova Systems - Cloud Migration Enterprise Deal"
        assert float(opp_row[1]) == 25000.0
        assert opp_row[3] == cust_id
        assert opp_row[4] == lead_alex_id

        # Check AuditLog for Conversion
        cur.execute("SELECT Action, Details FROM AuditLogs WHERE EntityName = 'Lead' AND EntityId = ? AND Action = 'LeadConversion'", (str(lead_alex_id),))
        conv_audit = cur.fetchone()
        assert conv_audit is not None, "AuditLog for LeadConversion not found!"
        assert "converted to Customer" in conv_audit[1]

        conn.close()
        test_results["TC12_LeadConversion"] = "PASS"
        print(f"  -> PASS: Lead converted atomically to Customer (ID: {cust_id}) and Opportunity (ID: {opp_id}).")

        # ----------------------------------------------------
        # TEST 13: Converted Lead Immutability
        # ----------------------------------------------------
        print("\n[TEST 13] Converted Lead Immutability (Cannot change status or re-convert)...")
        # Attempt edit status change
        data = urllib.parse.urlencode({
            "LeadId": lead_alex_id,
            "NewStatus": "New",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/ChangeStatus", data=data)
        # Should redirect with error
        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Status FROM Leads WHERE Id = ?", (lead_alex_id,))
        assert cur.fetchone()[0] == 5, "Status must remain Converted (5)!"
        conn.close()

        # Attempt re-convert
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Convert/{lead_alex_id}")
        assert resp.status == 302

        test_results["TC13_ConvertedLeadImmutability"] = "PASS"
        print("  -> PASS: Converted lead is protected against invalid transitions.")

        # ----------------------------------------------------
        # TEST 14: Duplicate Customer Prevention during Conversion
        # ----------------------------------------------------
        print("\n[TEST 14] Duplicate Customer Prevention during Lead Conversion...")
        # Create 3rd lead with identical email as existing customer (john.tech@novasystems.io)
        # Note: Email must be unique for leads, so use existing Customer's phone!
        create_page = c_admin.open(f"{BASE_URL}/Leads/Create").read().decode("utf-8")
        csrf_admin = extract_csrf(create_page)
        data = urllib.parse.urlencode({
            "LeadName": "Nova Branch Office",
            "Email": "branch@novasystems.io",
            "Phone": "+1 (555) 234-5678",  # MATCHES EXISTING CUSTOMER'S PHONE!
            "CompanyName": "Nova Systems Branch",
            "Source": "Website",
            "Status": "Qualified",
            "ExpectedValue": "15000",
            "AssignedToUserId": alex_user_id,
            "__RequestVerificationToken": csrf_admin
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Leads/Create", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT Id, LeadCode FROM Leads WHERE Email = 'branch@novasystems.io'")
        lead_branch_id, lead_branch_code = cur.fetchone()
        cur.execute("SELECT COUNT(*) FROM Customers")
        customer_count_before = cur.fetchone()[0]
        conn.close()

        # Now convert this branch lead
        convert_page = c_alex.open(f"{BASE_URL}/Leads/Convert/{lead_branch_id}").read().decode("utf-8")
        csrf_alex = extract_csrf(convert_page)
        data = urllib.parse.urlencode({
            "LeadId": lead_branch_id,
            "LeadCode": lead_branch_code,
            "LeadName": "Nova Branch Office",
            "Email": "branch@novasystems.io",
            "Phone": "+1 (555) 234-5678",
            "ExpectedValue": "15000",
            "OpportunityTitle": "Nova Branch - Addon Licenses",
            "OpportunityAmount": "15000",
            "OpportunityStage": "Prospecting",
            "ExpectedCloseDate": "2026-12-01",
            "__RequestVerificationToken": csrf_alex
        }).encode("utf-8")
        resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Convert/{lead_branch_id}", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT COUNT(*) FROM Customers")
        customer_count_after = cur.fetchone()[0]
        cur.execute("SELECT ConvertedCustomerId FROM Leads WHERE Id = ?", (lead_branch_id,))
        branch_conv_cust_id = cur.fetchone()[0]
        assert customer_count_after == customer_count_before, f"Expected NO duplicate customer created (count: {customer_count_before} vs {customer_count_after})!"
        assert branch_conv_cust_id == cust_id, f"Expected linked to existing customer {cust_id}, got {branch_conv_cust_id}"
        conn.close()

        test_results["TC14_DuplicateCustomerPrevention"] = "PASS"
        print(f"  -> PASS: Linked to existing customer #{cust_id} without duplicate account creation.")

        # ----------------------------------------------------
        # TEST 15: Search, Filter, Pagination
        # ----------------------------------------------------
        print("\n[TEST 15] Search, Filtering, and Pagination on Leads list...")
        # Search by company
        search_html = c_admin.open(f"{BASE_URL}/Leads?SearchTerm=Cyberdyne").read().decode("utf-8")
        assert "Elena Rostova" in search_html, "Search by Cyberdyne should return Elena"
        assert "John Tech" not in search_html, "Search by Cyberdyne should not return John"

        # Filter by status
        filter_status = c_admin.open(f"{BASE_URL}/Leads?Status=Converted").read().decode("utf-8")
        assert "John Tech" in filter_status, "Filter by Converted should return John"
        assert "Elena Rostova" not in filter_status, "Filter by Converted should not return Elena (New)"

        # Filter by source
        filter_source = c_admin.open(f"{BASE_URL}/Leads?Source=Referral").read().decode("utf-8")
        assert "Elena Rostova" in filter_source, "Filter by Referral should return Elena"

        test_results["TC15_SearchAndFilter"] = "PASS"
        print("  -> PASS: Search and filter return exact expected result sets.")

        # ----------------------------------------------------
        # TEST 16: Lead Deletion & Audit Logging
        # ----------------------------------------------------
        print("\n[TEST 16] Lead Deletion and Audit Trail...")
        del_page = c_admin.open(f"{BASE_URL}/Leads/Delete/{lead_sarah_id}").read().decode("utf-8")
        csrf_admin = extract_csrf(del_page)
        data = urllib.parse.urlencode({"__RequestVerificationToken": csrf_admin}).encode("utf-8")
        resp = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Leads/Delete/{lead_sarah_id}", data=data)
        assert resp.status == 302

        conn = sqlite3.connect("AcxiomCRM.db")
        cur = conn.cursor()
        cur.execute("SELECT COUNT(*) FROM Leads WHERE Id = ?", (lead_sarah_id,))
        assert cur.fetchone()[0] == 0, "Elena Rostova lead should be deleted from DB!"

        cur.execute("SELECT Details FROM AuditLogs WHERE EntityName = 'Lead' AND EntityId = ? AND Action = 'DeleteLead'", (str(lead_sarah_id),))
        del_audit = cur.fetchone()
        assert del_audit is not None, "AuditLog for DeleteLead not recorded!"
        conn.close()

        test_results["TC16_LeadDeletion"] = "PASS"
        print("  -> PASS: Lead deleted and recorded in audit log.")

        # ----------------------------------------------------
        # Summary
        # ----------------------------------------------------
        print("\n" + "=" * 70)
        print("PHASE 3 ACCEPTANCE TEST RESULTS SUMMARY")
        print("=" * 70)
        all_passed = True
        for test_name, status in test_results.items():
            print(f"  {test_name.ljust(35)} : {status}")
            if status != "PASS":
                all_passed = False

        assert all_passed, "Some tests failed!"
        print("\nALL 16 TEST SUITES PASSED! PHASE 3 VERIFICATION COMPLETE!")

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
