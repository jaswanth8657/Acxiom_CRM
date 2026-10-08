import subprocess
import time
import urllib.request
import urllib.parse
import http.cookiejar
import re
import os
import sqlite3
import datetime
import json
import sys
import traceback

BASE_URL = "http://127.0.0.1:5258"
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

def safe_open(opener, url, data=None):
    try:
        return opener.open(url, data=data)
    except urllib.error.HTTPError as e:
        return e

def api_request(opener, url, method="GET", payload=None):
    data = None
    headers = {"Accept": "application/json"}
    if payload is not None:
        data = json.dumps(payload).encode("utf-8")
        headers["Content-Type"] = "application/json"
    
    req = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        resp = opener.open(req)
        status_code = resp.status if hasattr(resp, "status") else resp.code
        body_text = resp.read().decode("utf-8")
        json_data = None
        if body_text:
            try:
                json_data = json.loads(body_text)
            except Exception:
                pass
        return status_code, json_data, resp.headers, body_text
    except urllib.error.HTTPError as e:
        status_code = e.code
        body_text = e.read().decode("utf-8")
        json_data = None
        if body_text:
            try:
                json_data = json.loads(body_text)
            except Exception:
                pass
        return status_code, json_data, e.headers, body_text

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

def check_no_security_fields(data, path=""):
    forbidden_keys = {
        "password", "passwordhash", "securitystamp", "concurrencystamp", 
        "token", "tokens", "normalizedusername", "normalizedemail"
    }
    if isinstance(data, dict):
        for k, v in data.items():
            lower_k = k.lower()
            assert lower_k not in forbidden_keys, f"Security leak! Found forbidden field '{k}' at {path}"
            check_no_security_fields(v, f"{path}.{k}")
    elif isinstance(data, list):
        for i, item in enumerate(data):
            check_no_security_fields(item, f"{path}[{i}]")

def run_tests():
    print("=" * 80)
    print("STARTING PHASE 8 — FINAL EVALUATOR SIMULATION & HARDENING AUDIT SUITE")
    print("=" * 80)

    # 1. Deterministic database cleanup before launching server
    conn = sqlite3.connect("AcxiomCRM.db")
    cur = conn.cursor()
    cur.execute("DELETE FROM FollowUps")
    cur.execute("DELETE FROM Activities")
    cur.execute("DELETE FROM Opportunities")
    cur.execute("DELETE FROM Leads")
    cur.execute("DELETE FROM Customers")
    cur.execute("DELETE FROM AuditLogs")
    cur.execute("DELETE FROM AspNetUsers WHERE Email LIKE '%audit.rep%' OR Email LIKE '%test.%'")
    conn.commit()
    conn.close()

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
        # Register standard users
        register_user("Marcus Manager", "marcus.manager@acxiomcrm.local", "Manager", "Manager@Acxiom2026!")
        register_user("Alex Sales", "alex.sales@acxiomcrm.local", "SalesExecutive", "Sales@Acxiom2026!")
        register_user("Sarah Sales", "sarah.sales@acxiomcrm.local", "SalesExecutive", "Sales@Acxiom2026!")

        conn = sqlite3.connect("AcxiomCRM.db")
        c = conn.cursor()
        c.execute("SELECT Id FROM AspNetUsers WHERE Email = 'admin@acxiomcrm.local'")
        admin_id = c.fetchone()[0]
        c.execute("SELECT Id FROM AspNetUsers WHERE Email = 'alex.sales@acxiomcrm.local'")
        alex_id = c.fetchone()[0]
        c.execute("SELECT Id FROM AspNetUsers WHERE Email = 'sarah.sales@acxiomcrm.local'")
        sarah_id = c.fetchone()[0]
        conn.close()

        # Clients
        c_anon, c_anon_nr, _ = create_client()
        c_admin, c_admin_nr, _ = create_client()
        c_alex, c_alex_nr, _ = create_client()
        c_sarah, c_sarah_nr, _ = create_client()

        # -------------------------------------------------------------
        # STEP 1: Anonymous user opens CRM -> blocked
        # -------------------------------------------------------------
        print("\n[STEP 1] Anonymous user opens CRM -> blocked from protected resources...")
        try:
            endpoints = ["/Dashboard", "/Customers", "/Leads", "/Opportunities", "/FollowUps", "/Reports", "/Users"]
            for ep in endpoints:
                resp = safe_open_no_redirect(c_anon_nr, f"{BASE_URL}{ep}")
                code = resp.status if hasattr(resp, "status") else resp.code
                loc = resp.headers.get("Location") if hasattr(resp, "headers") else ""
                assert code == 302 and "/Account/Login" in loc, f"Anonymous access to {ep} not redirected to Login! Code: {code}, Loc: {loc}"

            # API endpoints return 401
            for api_ep in ["/api/customers", "/api/leads", "/api/opportunities"]:
                status, _, _, _ = api_request(c_anon, f"{BASE_URL}{api_ep}")
                assert status == 401, f"Anonymous API access to {api_ep} returned {status} instead of 401"

            test_results["step_01_anonymous_blocked"] = "PASS"
            print("  -> PASS: Anonymous access to all MVC and API endpoints strictly blocked")
        except Exception as e:
            test_results["step_01_anonymous_blocked"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 2: User logs in -> Dashboard
        # -------------------------------------------------------------
        print("\n[STEP 2] User authentication with valid credentials -> redirects to /Dashboard...")
        try:
            # Bad credentials rejected
            login_page = c_anon.open(f"{BASE_URL}/Account/Login").read().decode("utf-8")
            csrf = extract_csrf(login_page)
            bad_data = urllib.parse.urlencode({
                "Email": "admin@acxiomcrm.local",
                "Password": "WrongPassword123!",
                "__RequestVerificationToken": csrf
            }).encode("utf-8")
            resp_bad = safe_open(c_anon, f"{BASE_URL}/Account/Login", data=bad_data)
            bad_html = resp_bad.read().decode("utf-8")
            assert "Invalid login attempt" in bad_html, "Failed login must show error message"

            # Valid login redirects to /Dashboard
            admin_resp = login(c_admin, c_admin_nr, "admin@acxiomcrm.local", "Admin@Acxiom2026!")
            assert "/Dashboard" in admin_resp.headers.get("Location"), "Admin login did not redirect to /Dashboard"

            alex_resp = login(c_alex, c_alex_nr, "alex.sales@acxiomcrm.local", "Sales@Acxiom2026!")
            assert "/Dashboard" in alex_resp.headers.get("Location"), "SalesExecutive login did not redirect to /Dashboard"

            login(c_sarah, c_sarah_nr, "sarah.sales@acxiomcrm.local", "Sales@Acxiom2026!")

            test_results["step_02_login_to_dashboard"] = "PASS"
            print("  -> PASS: Bad login rejected; valid login redirects to /Dashboard")
        except Exception as e:
            test_results["step_02_login_to_dashboard"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 3: Dashboard displays KPIs & charts
        # -------------------------------------------------------------
        print("\n[STEP 3] Dashboard displays KPI cards and analytics charts...")
        try:
            dash_html = c_admin.open(f"{BASE_URL}/Dashboard").read().decode("utf-8")
            expected_kpi_ids = [
                "kpi-total-customers", "kpi-total-leads", "kpi-open-leads", "kpi-total-opps",
                "kpi-open-opps", "kpi-won-opps", "kpi-lost-opps",
                "kpi-total-pipeline", "kpi-weighted-pipeline", "kpi-pending-followups", "kpi-overdue-followups"
            ]
            for kpi_id in expected_kpi_ids:
                assert kpi_id in dash_html, f"KPI card id '{kpi_id}' missing from Dashboard"

            # Chart endpoints
            status_l, data_l, _, _ = api_request(c_admin, f"{BASE_URL}/Dashboard/GetLeadStatusChartData")
            assert status_l == 200 and "labels" in data_l
            status_p, data_p, _, _ = api_request(c_admin, f"{BASE_URL}/Dashboard/GetOpportunityPipelineChartData")
            assert status_p == 200 and "labels" in data_p
            status_s, data_s, _, _ = api_request(c_admin, f"{BASE_URL}/Dashboard/GetMonthlySalesChartData")
            assert status_s == 200 and "months" in data_s

            test_results["step_03_dashboard_kpis_charts"] = "PASS"
            print("  -> PASS: All 11 KPI cards and all 3 Chart.js endpoints operational")
        except Exception as e:
            test_results["step_03_dashboard_kpis_charts"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 4: Sales Executive sees only assigned records
        # -------------------------------------------------------------
        sarah_cust_id = None
        print("\n[STEP 4] Sales Executive role scoping on CRM modules...")
        try:
            # Create a customer for Sarah to ensure separation
            s_cust_payload = {
                "CustomerCode": "CUST-SRH-991",
                "CustomerName": "Sarah Secret Client",
                "Email": "sarah.secret@corp.com",
                "Phone": "555-010-9988",
                "CompanyName": "Sarah Corp",
                "Address": "123 Sarah St",
                "City": "Boston",
                "State": "MA",
                "Status": "Active"
            }
            create_page = c_sarah.open(f"{BASE_URL}/Customers/Create").read().decode("utf-8")
            s_cust_payload["__RequestVerificationToken"] = extract_csrf(create_page)
            resp_c = safe_open_no_redirect(c_sarah_nr, f"{BASE_URL}/Customers/Create", data=urllib.parse.urlencode(s_cust_payload).encode("utf-8"))
            loc = resp_c.headers.get("Location")
            assert resp_c.status == 302 and "/Customers/Details" in loc, f"Customer creation for Sarah failed: {resp_c.status}, loc: {loc}"
            sarah_cust_id = int(re.search(r'/Details/(\d+)', loc).group(1))

            # Alex views customer list: must NOT see Sarah's customer
            alex_cust_list = c_alex.open(f"{BASE_URL}/Customers").read().decode("utf-8")
            assert "Sarah Secret Client" not in alex_cust_list, "SalesExecutive Alex saw Sarah's customer in list!"

            # Alex views API customer list: must NOT contain Sarah's customer
            status, alex_api_cust, _, _ = api_request(c_alex, f"{BASE_URL}/api/customers")
            assert status == 200
            for item in alex_api_cust["items"]:
                assert item["id"] != sarah_cust_id, "Alex API customer list leaked Sarah's customer"

            test_results["step_04_sales_exec_scoping"] = "PASS"
            print("  -> PASS: SalesExecutive records are strictly scoped in UI and API")
        except Exception as e:
            test_results["step_04_sales_exec_scoping"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 5: Sales Executive creates / updates Customer
        # -------------------------------------------------------------
        alex_cust_id = None
        print("\n[STEP 5] Sales Executive creates and updates assigned Customer...")
        try:
            cust_page = c_alex.open(f"{BASE_URL}/Customers/Create").read().decode("utf-8")
            csrf = extract_csrf(cust_page)
            payload = {
                "CustomerCode": "CUST-ALX-101",
                "CustomerName": "Alex Venture Capital",
                "Email": "contact@alexventures.com",
                "Phone": "555-010-7711",
                "CompanyName": "Alex Ventures",
                "Address": "500 Tech Blvd",
                "City": "San Francisco",
                "State": "CA",
                "Status": "Active",
                "__RequestVerificationToken": csrf
            }
            resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Customers/Create", data=urllib.parse.urlencode(payload).encode("utf-8"))
            assert resp.status == 302
            alex_cust_id = int(re.search(r'/Details/(\d+)', resp.headers.get("Location")).group(1))
            assert alex_cust_id > 0, "Customer creation failed"

            # Update customer
            edit_page = c_alex.open(f"{BASE_URL}/Customers/Edit/{alex_cust_id}").read().decode("utf-8")
            csrf_edit = extract_csrf(edit_page)
            update_payload = {
                "Id": alex_cust_id,
                "CustomerCode": "CUST-ALX-101",
                "CustomerName": "Alex Venture Capital Partners",
                "Email": "contact@alexventures.com",
                "Phone": "555-010-7722",
                "CompanyName": "Alex Ventures Partners",
                "Address": "500 Tech Blvd Suite 200",
                "City": "San Francisco",
                "State": "CA",
                "Status": "Active",
                "__RequestVerificationToken": csrf_edit
            }
            resp_up = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Customers/Edit/{alex_cust_id}", data=urllib.parse.urlencode(update_payload).encode("utf-8"))
            assert resp_up.status == 302, "Customer update failed"

            details = c_alex.open(f"{BASE_URL}/Customers/Details/{alex_cust_id}").read().decode("utf-8")
            assert "Alex Venture Capital Partners" in details, "Customer details did not reflect updated name"

            test_results["step_05_customer_create_update"] = "PASS"
            print(f"  -> PASS: Customer #{alex_cust_id} created and updated by SalesExecutive")
        except Exception as e:
            test_results["step_05_customer_create_update"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 6: Sales Executive creates Lead
        # -------------------------------------------------------------
        alex_lead_id = None
        alex_lead_code = None
        print("\n[STEP 6] Sales Executive creates Lead...")
        try:
            lead_page = c_alex.open(f"{BASE_URL}/Leads/Create").read().decode("utf-8")
            csrf = extract_csrf(lead_page)
            lead_payload = {
                "LeadName": "Quantum Systems Prospect",
                "Email": "contact@quantumsys.com",
                "Phone": "+1 (555) 234-5678",
                "CompanyName": "Quantum Systems",
                "Title": "VP Engineering",
                "Source": "Website",
                "Status": "New",
                "ExpectedValue": "65000",
                "__RequestVerificationToken": csrf
            }
            resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Create", data=urllib.parse.urlencode(lead_payload).encode("utf-8"))
            assert resp.status == 302
            alex_lead_id = int(re.search(r'/Details/(\d+)', resp.headers.get("Location")).group(1))
            assert alex_lead_id > 0, "Lead creation failed"

            conn = sqlite3.connect("AcxiomCRM.db")
            cur = conn.cursor()
            cur.execute("SELECT LeadCode FROM Leads WHERE Id = ?", (alex_lead_id,))
            alex_lead_code = cur.fetchone()[0]
            conn.close()

            test_results["step_06_lead_create"] = "PASS"
            print(f"  -> PASS: Lead #{alex_lead_id} (Code: {alex_lead_code}) created by SalesExecutive")
        except Exception as e:
            test_results["step_06_lead_create"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 7: Lead progresses through valid lifecycle (New -> Contacted -> Qualified)
        # -------------------------------------------------------------
        print("\n[STEP 7] Lead lifecycle progression through valid states...")
        try:
            # 1. New -> Contacted via /Leads/ChangeStatus
            details_alex = c_alex.open(f"{BASE_URL}/Leads/Details/{alex_lead_id}").read().decode("utf-8")
            csrf_alex = extract_csrf(details_alex)
            data1 = urllib.parse.urlencode({
                "LeadId": alex_lead_id,
                "NewStatus": "Contacted",
                "Note": "Initial exploratory discovery call completed",
                "__RequestVerificationToken": csrf_alex
            }).encode("utf-8")
            resp_c1 = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/ChangeStatus", data=data1)
            assert resp_c1.status == 302

            # 2. Contacted -> Qualified via /Leads/ChangeStatus
            details_alex2 = c_alex.open(f"{BASE_URL}/Leads/Details/{alex_lead_id}").read().decode("utf-8")
            csrf_alex2 = extract_csrf(details_alex2)
            data2 = urllib.parse.urlencode({
                "LeadId": alex_lead_id,
                "NewStatus": "Qualified",
                "Note": "BANT criteria confirmed with decision maker",
                "__RequestVerificationToken": csrf_alex2
            }).encode("utf-8")
            resp_c2 = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/ChangeStatus", data=data2)
            assert resp_c2.status == 302

            lead_details = c_alex.open(f"{BASE_URL}/Leads/Details/{alex_lead_id}").read().decode("utf-8")
            assert "Qualified" in lead_details, "Lead status not updated to Qualified"

            test_results["step_07_lead_lifecycle"] = "PASS"
            print("  -> PASS: Lead successfully moved New -> Contacted -> Qualified")
        except Exception as e:
            test_results["step_07_lead_lifecycle"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 8: Qualified Lead converts to Customer + Opportunity
        # -------------------------------------------------------------
        converted_cust_id = None
        converted_opp_id = None
        print("\n[STEP 8] Qualified Lead converts to Customer + Opportunity...")
        try:
            conv_page = c_alex.open(f"{BASE_URL}/Leads/Convert/{alex_lead_id}").read().decode("utf-8")
            csrf = extract_csrf(conv_page)
            conv_payload = {
                "LeadId": alex_lead_id,
                "LeadCode": alex_lead_code,
                "LeadName": "Quantum Systems Prospect",
                "Email": "contact@quantumsys.com",
                "Phone": "+1 (555) 234-5678",
                "CompanyName": "Quantum Systems Inc",
                "ExpectedValue": "75000",
                "AssignedToUserId": alex_id,
                "OpportunityTitle": "Quantum Systems Enterprise Platform Contract",
                "OpportunityAmount": "75000",
                "OpportunityStage": "Prospecting",
                "ExpectedCloseDate": (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=45)).strftime("%Y-%m-%d"),
                "Notes": "Converted following successful technical evaluation",
                "__RequestVerificationToken": csrf
            }
            resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Leads/Convert/{alex_lead_id}", data=urllib.parse.urlencode(conv_payload).encode("utf-8"))
            assert resp.status == 302, f"Lead conversion did not redirect, status: {resp.status}"

            # Check database for linked customer and opportunity
            conn = sqlite3.connect("AcxiomCRM.db")
            cur = conn.cursor()
            cur.execute("SELECT Status, ConvertedCustomerId, ConvertedOpportunityId FROM Leads WHERE Id = ?", (alex_lead_id,))
            row = cur.fetchone()
            conn.close()

            assert row[0] == 5, f"Lead status expected Converted (5), got {row[0]}"
            converted_cust_id = row[1]
            converted_opp_id = row[2]
            assert converted_cust_id is not None and converted_opp_id is not None, "Lead missing converted customer or opp ID"

            test_results["step_08_lead_conversion"] = "PASS"
            print(f"  -> PASS: Lead converted to Customer #{converted_cust_id} and Opportunity #{converted_opp_id}")
        except Exception as e:
            test_results["step_08_lead_conversion"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 9: Opportunity progresses through pipeline
        # -------------------------------------------------------------
        print("\n[STEP 9] Opportunity pipeline stage transitions and probability updates...")
        try:
            # Move from Prospecting to Proposal (3)
            edit_page = c_alex.open(f"{BASE_URL}/Opportunities/Edit/{converted_opp_id}").read().decode("utf-8")
            csrf = extract_csrf(edit_page)
            future_date = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=30)).strftime("%Y-%m-%d")
            p_opp = {
                "Id": converted_opp_id,
                "OpportunityName": "Quantum Systems Enterprise Platform Contract",
                "CustomerId": converted_cust_id,
                "LeadId": alex_lead_id,
                "Amount": "85000",
                "CurrentStage": "Prospecting",
                "Stage": "Proposal",
                "Probability": "60",
                "ExpectedCloseDate": future_date,
                "Status": "Active",
                "StageChangeNote": "Formal proposal delivered to CFO",
                "__RequestVerificationToken": csrf
            }
            resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Opportunities/Edit/{converted_opp_id}", data=urllib.parse.urlencode(p_opp).encode("utf-8"))
            assert resp.status == 302, f"Opportunity edit expected 302, got {resp.status}"

            opp_details = c_alex.open(f"{BASE_URL}/Opportunities/Details/{converted_opp_id}").read().decode("utf-8")
            assert "Proposal" in opp_details
            assert "85,000" in opp_details or "$85,000" in opp_details

            test_results["step_09_opportunity_progression"] = "PASS"
            print("  -> PASS: Opportunity moved to Proposal stage ($85,000, 60% probability)")
        except Exception as e:
            test_results["step_09_opportunity_progression"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 10: Follow-Up created
        # -------------------------------------------------------------
        followup_id = None
        print("\n[STEP 10] Follow-Up creation with customer and opportunity linkage...")
        try:
            fu_page = c_alex.open(f"{BASE_URL}/FollowUps/Create").read().decode("utf-8")
            csrf = extract_csrf(fu_page)
            fu_date = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=3)).strftime("%Y-%m-%d %H:%M")
            fu_payload = {
                "Subject": "Quantum Systems Contract Review Call",
                "ScheduledDate": fu_date,
                "Type": "Call",
                "Priority": "High",
                "CustomerId": str(converted_cust_id),
                "OpportunityId": str(converted_opp_id),
                "Notes": "Discuss commercial terms and SLA addendum",
                "__RequestVerificationToken": csrf
            }
            resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/FollowUps/Create", data=urllib.parse.urlencode(fu_payload).encode("utf-8"))
            assert resp.status == 302
            followup_id = int(re.search(r'/Details/(\d+)', resp.headers.get("Location")).group(1))
            assert followup_id > 0

            test_results["step_10_followup_create"] = "PASS"
            print(f"  -> PASS: Follow-Up #{followup_id} created successfully")
        except Exception as e:
            test_results["step_10_followup_create"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 11: Follow-Up completed
        # -------------------------------------------------------------
        print("\n[STEP 11] Follow-Up completion workflow...")
        try:
            details_fu = c_alex.open(f"{BASE_URL}/FollowUps/Details/{followup_id}").read().decode("utf-8")
            csrf_fu = extract_csrf(details_fu)
            comp_payload = {
                "id": followup_id,
                "notes": "Call completed; customer approved SLA terms",
                "__RequestVerificationToken": csrf_fu
            }
            resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/FollowUps/Complete/{followup_id}", data=urllib.parse.urlencode(comp_payload).encode("utf-8"))
            assert resp.status == 302

            fu_details = c_alex.open(f"{BASE_URL}/FollowUps/Details/{followup_id}").read().decode("utf-8")
            assert "Completed" in fu_details, "Follow-Up status not Completed"

            test_results["step_11_followup_completed"] = "PASS"
            print("  -> PASS: Follow-Up marked Completed with outcome notes")
        except Exception as e:
            test_results["step_11_followup_completed"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 12: Activity created
        # -------------------------------------------------------------
        activity_id = None
        print("\n[STEP 12] Activity logged against customer and opportunity...")
        try:
            act_page = c_alex.open(f"{BASE_URL}/Activities/Create").read().decode("utf-8")
            csrf = extract_csrf(act_page)
            act_payload = {
                "Subject": "Executive Alignment Meeting",
                "Type": "Meeting",
                "ActivityDate": datetime.datetime.now().strftime("%Y-%m-%dT%H:%M"),
                "CustomerId": str(converted_cust_id),
                "OpportunityId": str(converted_opp_id),
                "Description": "Aligned on Q4 kickoff milestone and implementation roadmap",
                "Status": "Completed",
                "__RequestVerificationToken": csrf
            }
            resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Activities/Create", data=urllib.parse.urlencode(act_payload).encode("utf-8"))
            assert resp.status == 302
            loc = resp.headers.get("Location")
            activity_id = int(re.search(r'/Details/(\d+)', loc).group(1))
            assert activity_id > 0

            test_results["step_12_activity_created"] = "PASS"
            print(f"  -> PASS: Activity #{activity_id} logged successfully")
        except Exception as e:
            test_results["step_12_activity_created"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 13: Dashboard metrics reflect changes
        # -------------------------------------------------------------
        print("\n[STEP 13] Dashboard metrics calculation reflects newly created data...")
        try:
            dash_html = c_alex.open(f"{BASE_URL}/Dashboard").read().decode("utf-8")
            assert "Quantum Systems" in dash_html or "85,000" in dash_html or "$85,000" in dash_html or "Proposal" in dash_html

            test_results["step_13_dashboard_reflection"] = "PASS"
            print("  -> PASS: Dashboard immediately reflects updated CRM data")
        except Exception as e:
            test_results["step_13_dashboard_reflection"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 14: Reports reflect changes
        # -------------------------------------------------------------
        print("\n[STEP 14] Reports reflect new customers, leads, opportunities...")
        try:
            rep_cust = c_alex.open(f"{BASE_URL}/Reports/Customers").read().decode("utf-8")
            assert "Quantum Systems" in rep_cust or "Alex Venture" in rep_cust

            rep_opp = c_alex.open(f"{BASE_URL}/Reports/Opportunities").read().decode("utf-8")
            assert "Quantum Systems" in rep_opp

            test_results["step_14_reports_reflection"] = "PASS"
            print("  -> PASS: CRM reports render accurate data")
        except Exception as e:
            test_results["step_14_reports_reflection"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 15: CSV export works
        # -------------------------------------------------------------
        print("\n[STEP 15] Reports CSV export functionality and sensitive data exclusion...")
        try:
            csv_resp = c_alex.open(f"{BASE_URL}/Reports/ExportCustomers")
            csv_content = csv_resp.read().decode("utf-8")
            assert "Customer Code" in csv_content or "Customer Name" in csv_content
            assert "PasswordHash" not in csv_content
            assert "SecurityStamp" not in csv_content

            test_results["step_15_csv_export"] = "PASS"
            print("  -> PASS: CSV export valid; zero sensitive credential fields in export")
        except Exception as e:
            test_results["step_15_csv_export"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 16: API returns corresponding records
        # -------------------------------------------------------------
        print("\n[STEP 16] REST API returns created customer, lead, opportunity...")
        try:
            status_c, data_c, _, _ = api_request(c_alex, f"{BASE_URL}/api/customers/{converted_cust_id}")
            assert status_c == 200 and data_c["id"] == converted_cust_id

            status_l, data_l, _, _ = api_request(c_alex, f"{BASE_URL}/api/leads/{alex_lead_id}")
            assert status_l == 200 and data_l["id"] == alex_lead_id

            status_o, data_o, _, _ = api_request(c_alex, f"{BASE_URL}/api/opportunities/{converted_opp_id}")
            assert status_o == 200 and data_o["id"] == converted_opp_id

            test_results["step_16_api_record_retrieval"] = "PASS"
            print("  -> PASS: REST API seamlessly returns records created via MVC")
        except Exception as e:
            test_results["step_16_api_record_retrieval"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 17: Unauthorized record access is blocked
        # -------------------------------------------------------------
        print("\n[STEP 17] Unauthorized cross-user record tampering is blocked (MVC & API)...")
        try:
            assert sarah_cust_id is not None, "sarah_cust_id missing"

            # Alex attempts to view Sarah's customer via MVC
            resp_tamper_mvc = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Customers/Details/{sarah_cust_id}")
            code = resp_tamper_mvc.status if hasattr(resp_tamper_mvc, "status") else resp_tamper_mvc.code
            loc = resp_tamper_mvc.headers.get("Location") if hasattr(resp_tamper_mvc, "headers") else ""
            assert code == 302 and "AccessDenied" in loc, f"MVC cross-user access allowed! Code: {code}, Loc: {loc}"

            # Alex attempts to view Sarah's customer via API
            status_api, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/customers/{sarah_cust_id}")
            assert status_api in (403, 404), f"API cross-user access allowed with status {status_api}!"

            test_results["step_17_unauthorized_access_blocked"] = "PASS"
            print("  -> PASS: Both MVC (AccessDenied 302) and API (403/404) block IDOR tampering")
        except Exception as e:
            test_results["step_17_unauthorized_access_blocked"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 18: Admin sees audit trail
        # -------------------------------------------------------------
        print("\n[STEP 18] Admin audit trail visibility...")
        try:
            audit_html = c_admin.open(f"{BASE_URL}/Reports/Audit").read().decode("utf-8")
            assert "System Audit Log Report" in audit_html
            assert "CreateCustomer" in audit_html or "LeadConversion" in audit_html

            # SalesExecutive cannot view audit trail
            resp_alex_audit = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Reports/Audit")
            code_a = resp_alex_audit.status if hasattr(resp_alex_audit, "status") else resp_alex_audit.code
            loc_a = resp_alex_audit.headers.get("Location") if hasattr(resp_alex_audit, "headers") else ""
            assert code_a == 302 and "AccessDenied" in loc_a, "Non-admin accessed audit logs!"

            test_results["step_18_admin_audit_trail"] = "PASS"
            print("  -> PASS: Admin accesses comprehensive audit log; non-admin blocked")
        except Exception as e:
            test_results["step_18_admin_audit_trail"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 19: Invalid business data is rejected
        # -------------------------------------------------------------
        print("\n[STEP 19] Comprehensive business rules and constraint rejections...")
        try:
            # 1. Invalid opportunity amount <= 0
            status_bad_amt, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/opportunities", method="POST", payload={
                "opportunityName": "Free Deal",
                "customerId": converted_cust_id,
                "amount": 0,
                "stage": 1,
                "probability": 20,
                "expectedCloseDate": (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=30)).isoformat()
            })
            assert status_bad_amt == 400

            # 2. Probability > 100
            status_bad_p, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/opportunities", method="POST", payload={
                "opportunityName": "110% Deal",
                "customerId": converted_cust_id,
                "amount": 1000,
                "stage": 1,
                "probability": 110,
                "expectedCloseDate": (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=30)).isoformat()
            })
            assert status_bad_p == 400

            # 3. Duplicate customer email
            status_dup_c, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/customers", method="POST", payload={
                "customerName": "Dup Customer",
                "email": "contact@alexventures.com", # already exists!
                "phone": "555-010-8888"
            })
            assert status_dup_c in (400, 409), f"Duplicate customer email status was {status_dup_c}"

            test_results["step_19_business_data_rejected"] = "PASS"
            print("  -> PASS: All invalid business inputs rejected with 400/409")
        except Exception as e:
            test_results["step_19_business_data_rejected"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # STEP 20: No sensitive information is exposed
        # -------------------------------------------------------------
        print("\n[STEP 20] Security audit: Zero sensitive information exposed in UI or API...")
        try:
            # Inspect responses across all API endpoints
            for ep in [f"/api/customers/{converted_cust_id}", f"/api/leads/{alex_lead_id}", f"/api/opportunities/{converted_opp_id}"]:
                _, data, _, raw = api_request(c_admin, f"{BASE_URL}{ep}")
                check_no_security_fields(data)
                assert "PasswordHash" not in raw
                assert "SecurityStamp" not in raw
                assert "StackTrace" not in raw

            test_results["step_20_zero_sensitive_data_exposure"] = "PASS"
            print("  -> PASS: Complete absence of credentials, hashes, and internal security tokens verified")
        except Exception as e:
            test_results["step_20_zero_sensitive_data_exposure"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # BONUS STEP 21: User & Role Management Audit (/Users)
        # -------------------------------------------------------------
        print("\n[STEP 21] Admin User & Role Management audit...")
        try:
            # 1. Admin accesses /Users
            users_page = c_admin.open(f"{BASE_URL}/Users").read().decode("utf-8")
            assert "User & Role Management" in users_page
            assert "admin@acxiomcrm.local" in users_page

            # 2. Non-admin (Alex) accessing /Users is blocked with AccessDenied
            alex_users_resp = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Users")
            code_u = alex_users_resp.status if hasattr(alex_users_resp, "status") else alex_users_resp.code
            loc_u = alex_users_resp.headers.get("Location") if hasattr(alex_users_resp, "headers") else ""
            assert code_u == 302 and "AccessDenied" in loc_u, f"Non-admin accessed /Users! Code: {code_u}, Loc: {loc_u}"

            # 3. Admin creates new user via /Users/Create
            u_create_page = c_admin.open(f"{BASE_URL}/Users/Create").read().decode("utf-8")
            csrf_u = extract_csrf(u_create_page)
            new_user_payload = {
                "FullName": "Audit Test SalesRep",
                "Email": "audit.rep@acxiomcrm.local",
                "SelectedRole": "SalesExecutive",
                "Password": "User@Acxiom2026!",
                "ConfirmPassword": "User@Acxiom2026!",
                "__RequestVerificationToken": csrf_u
            }
            resp_nu = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Users/Create", data=urllib.parse.urlencode(new_user_payload).encode("utf-8"))
            assert resp_nu.status == 302 and "/Users" in resp_nu.headers.get("Location")

            # Verify in database
            conn = sqlite3.connect("AcxiomCRM.db")
            cur = conn.cursor()
            cur.execute("SELECT Id, FullName, IsActive FROM AspNetUsers WHERE Email = 'audit.rep@acxiomcrm.local'")
            nu_row = cur.fetchone()
            assert nu_row is not None, "New user was not created in database"
            nu_id = nu_row[0]

            # 4. Admin edits role via /Users/EditRole
            u_role_page = c_admin.open(f"{BASE_URL}/Users/EditRole/{nu_id}").read().decode("utf-8")
            csrf_r = extract_csrf(u_role_page)
            role_payload = {
                "UserId": nu_id,
                "SelectedRole": "Manager",
                "__RequestVerificationToken": csrf_r
            }
            resp_r = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Users/EditRole", data=urllib.parse.urlencode(role_payload).encode("utf-8"))
            assert resp_r.status == 302

            # 5. Admin toggles active via /Users/ToggleActive
            users_refresh = c_admin.open(f"{BASE_URL}/Users").read().decode("utf-8")
            csrf_t = extract_csrf(users_refresh)
            toggle_payload = {
                "__RequestVerificationToken": csrf_t
            }
            resp_t = safe_open_no_redirect(c_admin_nr, f"{BASE_URL}/Users/ToggleActive/{nu_id}", data=urllib.parse.urlencode(toggle_payload).encode("utf-8"))
            assert resp_t.status == 302

            cur.execute("SELECT IsActive FROM AspNetUsers WHERE Id = ?", (nu_id,))
            is_active = cur.fetchone()[0]
            conn.close()
            assert is_active == 0, f"Expected deactivated user (0), got {is_active}"

            test_results["step_21_user_role_management"] = "PASS"
            print("  -> PASS: User creation, role change (Manager), and deactivation verified; non-admin blocked")
        except Exception as e:
            test_results["step_21_user_role_management"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

    finally:
        print("\nShutting down test server...")
        proc.terminate()
        try:
            proc.wait(timeout=5)
        except Exception:
            proc.kill()

    # -----------------------------------------------------------------
    # Print Summary
    # -----------------------------------------------------------------
    print("\n" + "=" * 80)
    print("PHASE 8 FINAL EVALUATOR SIMULATION & AUDIT RESULTS:")
    print("=" * 80)
    passed = 0
    failed = 0
    for name, res in test_results.items():
        status = "PASSED" if res == "PASS" else "FAILED"
        print(f"[{status}] {name}: {res}")
        if res == "PASS":
            passed += 1
        else:
            failed += 1

    print("-" * 80)
    print(f"TOTAL: {len(test_results)} | PASSED: {passed} | FAILED: {failed}")
    print("=" * 80)

    if failed > 0:
        sys.exit(1)

if __name__ == "__main__":
    run_tests()
