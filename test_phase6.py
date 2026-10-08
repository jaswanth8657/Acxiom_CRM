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

BASE_URL = "http://127.0.0.1:5256"
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

def seed_test_records(alex_id, sarah_id, admin_id):
    conn = sqlite3.connect("AcxiomCRM.db")
    cur = conn.cursor()

    # Clear prior test records for deterministic testing
    cur.execute("DELETE FROM FollowUps")
    cur.execute("DELETE FROM Activities")
    cur.execute("DELETE FROM Opportunities")
    cur.execute("DELETE FROM Leads")
    cur.execute("DELETE FROM Customers")
    cur.execute("DELETE FROM AuditLogs")
    conn.commit()

    now = datetime.datetime.utcnow().strftime("%Y-%m-%d %H:%M:%S")
    past_date = (datetime.datetime.utcnow() - datetime.timedelta(days=2)).strftime("%Y-%m-%d %H:%M:%S")
    future_date = (datetime.datetime.utcnow() + datetime.timedelta(days=3)).strftime("%Y-%m-%d %H:%M:%S")

    # 1. Customers: 1 for Alex, 1 for Sarah
    cur.execute("""
        INSERT INTO Customers (CustomerCode, CustomerName, Email, Phone, CompanyName, Status, CreatedDate, CreatedBy, AssignedToUserId)
        VALUES ('CUST-001', 'Alex Acme Corp', 'alex.acme@example.com', '555-0101', 'Acme Corp', 1, ?, 'admin', ?)
    """, (now, alex_id))
    cust1_id = cur.lastrowid

    cur.execute("""
        INSERT INTO Customers (CustomerCode, CustomerName, Email, Phone, CompanyName, Status, CreatedDate, CreatedBy, AssignedToUserId)
        VALUES ('CUST-002', 'Sarah Stark Ind', 'sarah.stark@example.com', '555-0102', 'Stark Ind', 1, ?, 'admin', ?)
    """, (now, sarah_id))
    cust2_id = cur.lastrowid

    # 2. Leads: 2 for Alex (1 New, 1 Converted), 1 for Sarah (1 Qualified)
    cur.execute("""
        INSERT INTO Leads (LeadCode, LeadName, Email, Phone, CompanyName, Source, Status, ExpectedValue, CreatedDate, CreatedBy, AssignedToUserId, ConvertedCustomerId)
        VALUES ('LEAD-001', 'Lead Inbound Alex', 'inbound@example.com', '555-0201', 'Inbound Co', 'Website', 1, 10000.0, ?, 'admin', ?, NULL)
    """, (now, alex_id))
    lead1_id = cur.lastrowid

    cur.execute("""
        INSERT INTO Leads (LeadCode, LeadName, Email, Phone, CompanyName, Source, Status, ExpectedValue, CreatedDate, CreatedBy, AssignedToUserId, ConvertedCustomerId)
        VALUES ('LEAD-002', 'Lead Converted Alex', 'conv@example.com', '555-0202', 'Acme Corp', 'Referral', 5, 25000.0, ?, 'admin', ?, ?)
    """, (now, alex_id, cust1_id))
    lead2_id = cur.lastrowid

    cur.execute("""
        INSERT INTO Leads (LeadCode, LeadName, Email, Phone, CompanyName, Source, Status, ExpectedValue, CreatedDate, CreatedBy, AssignedToUserId, ConvertedCustomerId)
        VALUES ('LEAD-003', 'Lead Qualified Sarah', 'sarah.lead@example.com', '555-0203', 'Stark Ind', 'LinkedIn', 3, 50000.0, ?, 'admin', ?, NULL)
    """, (now, sarah_id))
    lead3_id = cur.lastrowid

    # 3. Opportunities:
    # Alex: 1 Open (Proposal: $20,000, 60% prob), 1 Won ($30,000)
    # Sarah: 1 Open (Negotiation: $50,000, 80% prob), 1 Lost ($15,000)
    cur.execute("""
        INSERT INTO Opportunities (OpportunityName, CustomerId, LeadId, Amount, Stage, Probability, ExpectedCloseDate, Status, CreatedDate, AssignedToUserId)
        VALUES ('Alex Enterprise Deal', ?, ?, 20000.0, 3, 60, ?, 1, ?, ?)
    """, (cust1_id, lead1_id, future_date, now, alex_id))
    opp1_id = cur.lastrowid

    cur.execute("""
        INSERT INTO Opportunities (OpportunityName, CustomerId, LeadId, Amount, Stage, Probability, ExpectedCloseDate, Status, CreatedDate, AssignedToUserId)
        VALUES ('Alex Closed Deal', ?, ?, 30000.0, 5, 100, ?, 1, ?, ?)
    """, (cust1_id, lead2_id, past_date, now, alex_id))
    opp2_id = cur.lastrowid

    cur.execute("""
        INSERT INTO Opportunities (OpportunityName, CustomerId, LeadId, Amount, Stage, Probability, ExpectedCloseDate, Status, CreatedDate, AssignedToUserId)
        VALUES ('Sarah Stark Cloud Deal', ?, ?, 50000.0, 4, 80, ?, 1, ?, ?)
    """, (cust2_id, lead3_id, future_date, now, sarah_id))
    opp3_id = cur.lastrowid

    cur.execute("""
        INSERT INTO Opportunities (OpportunityName, CustomerId, LeadId, Amount, Stage, Probability, ExpectedCloseDate, Status, CreatedDate, AssignedToUserId)
        VALUES ('Sarah Lost Deal', ?, ?, 15000.0, 6, 0, ?, 1, ?, ?)
    """, (cust2_id, lead3_id, past_date, now, sarah_id))
    opp4_id = cur.lastrowid

    # 4. Follow-Ups:
    # Alex: 1 Planned Future, 1 Planned Past (Overdue)
    # Sarah: 1 Completed
    cur.execute("""
        INSERT INTO FollowUps (Subject, ScheduledDate, Type, Status, Priority, CustomerId, LeadId, OpportunityId, AssignedToUserId, CreatedAt)
        VALUES ('Alex Future Call', ?, 1, 1, 2, ?, ?, ?, ?, ?)
    """, (future_date, cust1_id, lead1_id, opp1_id, alex_id, now))

    cur.execute("""
        INSERT INTO FollowUps (Subject, ScheduledDate, Type, Status, Priority, CustomerId, LeadId, OpportunityId, AssignedToUserId, CreatedAt)
        VALUES ('Alex Overdue Meeting', ?, 2, 1, 3, ?, ?, ?, ?, ?)
    """, (past_date, cust1_id, lead1_id, opp1_id, alex_id, now))

    cur.execute("""
        INSERT INTO FollowUps (Subject, ScheduledDate, Type, Status, Priority, CustomerId, LeadId, OpportunityId, AssignedToUserId, CreatedAt, CompletedAt)
        VALUES ('Sarah Completed Email', ?, 3, 2, 2, ?, ?, ?, ?, ?, ?)
    """, (past_date, cust2_id, lead3_id, opp3_id, sarah_id, now, now))

    # 5. Activities:
    cur.execute("""
        INSERT INTO Activities (Subject, Type, Status, Description, ActivityDate, CustomerId, LeadId, OpportunityId, UserId, CreatedAt)
        VALUES ('Initial Discovery Call', 1, 1, 'Spoke with CTO', ?, ?, ?, ?, ?, ?)
    """, (now, cust1_id, lead1_id, opp1_id, alex_id, now))

    cur.execute("""
        INSERT INTO Activities (Subject, Type, Status, Description, ActivityDate, CustomerId, LeadId, OpportunityId, UserId, CreatedAt)
        VALUES ('Contract Review', 2, 1, 'Discussed terms', ?, ?, ?, ?, ?, ?)
    """, (now, cust2_id, lead3_id, opp3_id, sarah_id, now))

    # 6. Audit Logs:
    cur.execute("""
        INSERT INTO AuditLogs (UserId, Action, EntityName, EntityId, Details, IpAddress, Timestamp)
        VALUES (?, 'Create', 'Customer', ?, 'Created customer Alex Acme Corp', '127.0.0.1', ?)
    """, (admin_id, str(cust1_id), now))

    cur.execute("""
        INSERT INTO AuditLogs (UserId, Action, EntityName, EntityId, Details, IpAddress, Timestamp)
        VALUES (?, 'Create', 'Opportunity', ?, 'Created opportunity Alex Enterprise Deal', '127.0.0.1', ?)
    """, (admin_id, str(opp1_id), now))

    conn.commit()
    conn.close()

def run_tests():
    print("=" * 70)
    print("STARTING COMPREHENSIVE PHASE 6 DASHBOARD, ANALYTICS & REPORTS ACCEPTANCE SUITE")
    print("=" * 70)

    # Start Server on dedicated port 5256
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

        # Retrieve user IDs from database
        conn = sqlite3.connect("AcxiomCRM.db")
        c = conn.cursor()
        c.execute("SELECT Id FROM AspNetUsers WHERE Email = 'admin@acxiomcrm.local'")
        admin_id = c.fetchone()[0]
        c.execute("SELECT Id FROM AspNetUsers WHERE Email = 'alex.sales@acxiomcrm.local'")
        alex_id = c.fetchone()[0]
        c.execute("SELECT Id FROM AspNetUsers WHERE Email = 'sarah.sales@acxiomcrm.local'")
        sarah_id = c.fetchone()[0]
        conn.close()

        # Seed records with exact assignments
        seed_test_records(alex_id, sarah_id, admin_id)

        # Clients
        c_admin, c_admin_nr, _ = create_client()
        login_resp = login(c_admin, c_admin_nr, "admin@acxiomcrm.local", "Admin@Acxiom2026!")

        c_mgr, c_mgr_nr, _ = create_client()
        login(c_mgr, c_mgr_nr, "marcus.manager@acxiomcrm.local", "Manager@Acxiom2026!")

        c_alex, c_alex_nr, _ = create_client()
        alex_login_resp = login(c_alex, c_alex_nr, "alex.sales@acxiomcrm.local", "Sales@Acxiom2026!")

        c_sarah, c_sarah_nr, _ = create_client()
        login(c_sarah, c_sarah_nr, "sarah.sales@acxiomcrm.local", "Sales@Acxiom2026!")

        # -------------------------------------------------------------
        # TEST 1: Login redirect to /Dashboard
        # -------------------------------------------------------------
        print("\n[TEST 1] Login redirect to /Dashboard...")
        try:
            loc = login_resp.headers.get("Location")
            alex_loc = alex_login_resp.headers.get("Location")
            assert "/Dashboard" in loc or loc == "/" or loc == "/Dashboard", f"Admin redirect expected /Dashboard, got {loc}"
            assert "/Dashboard" in alex_loc or alex_loc == "/" or alex_loc == "/Dashboard", f"Alex redirect expected /Dashboard, got {alex_loc}"
            test_results["test_01_login_redirect_to_dashboard"] = "PASS"
            print("  -> PASS: Successful login redirects to /Dashboard")
        except Exception as e:
            test_results["test_01_login_redirect_to_dashboard"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 2: Dashboard page renders all 11 KPI cards
        # -------------------------------------------------------------
        print("\n[TEST 2] Dashboard renders all 11 KPI cards...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Dashboard")
            html = resp.read().decode("utf-8")
            assert resp.status == 200, f"Expected 200, got {resp.status}"

            kpis = [
                "kpi-total-customers", "kpi-total-leads", "kpi-open-leads",
                "kpi-total-opps", "kpi-open-opps", "kpi-won-opps", "kpi-lost-opps",
                "kpi-total-pipeline", "kpi-weighted-pipeline",
                "kpi-pending-followups", "kpi-overdue-followups"
            ]
            for k in kpis:
                assert f'id="{k}"' in html, f"Missing KPI element: {k}"

            test_results["test_02_dashboard_kpis_rendered"] = "PASS"
            print("  -> PASS: All 11 KPI cards present on /Dashboard")
        except Exception as e:
            test_results["test_02_dashboard_kpis_rendered"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 3: Dashboard date filtering (Today, ThisWeek, ThisMonth, Custom)
        # -------------------------------------------------------------
        print("\n[TEST 3] Dashboard date filtering...")
        try:
            periods = [0, 1, 2, 3] # AllTime, Today, ThisWeek, ThisMonth
            for p in periods:
                url = f"{BASE_URL}/Dashboard?period={p}"
                resp = c_admin.open(url)
                assert resp.status == 200, f"Failed for period {p}"

            # Custom range
            today_str = datetime.datetime.utcnow().strftime("%Y-%m-%d")
            custom_url = f"{BASE_URL}/Dashboard?period=4&startDate={today_str}&endDate={today_str}"
            resp = c_admin.open(custom_url)
            assert resp.status == 200, "Failed for custom date filter"

            test_results["test_03_dashboard_date_filtering"] = "PASS"
            print("  -> PASS: Date filtering (Today, Week, Month, Custom) supported")
        except Exception as e:
            test_results["test_03_dashboard_date_filtering"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 4: Lead Status Chart data endpoint
        # -------------------------------------------------------------
        print("\n[TEST 4] Lead status chart data endpoint...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Dashboard/GetLeadStatusChartData")
            assert resp.status == 200
            data = json.loads(resp.read().decode("utf-8"))
            assert "labels" in data and "counts" in data
            assert len(data["labels"]) == 5
            # Admin sees 3 leads total across statuses
            total_lead_counts = sum(data["counts"])
            assert total_lead_counts == 3, f"Expected 3 leads in chart, got {total_lead_counts}"
            test_results["test_04_dashboard_lead_status_chart_endpoint"] = "PASS"
            print("  -> PASS: Lead status chart endpoint returns valid dataset")
        except Exception as e:
            test_results["test_04_dashboard_lead_status_chart_endpoint"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 5: Opportunity Pipeline Chart data endpoint
        # -------------------------------------------------------------
        print("\n[TEST 5] Opportunity pipeline chart data endpoint...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Dashboard/GetOpportunityPipelineChartData")
            assert resp.status == 200
            data = json.loads(resp.read().decode("utf-8"))
            assert "labels" in data and "counts" in data and "amounts" in data
            assert len(data["labels"]) == 5
            total_opp_counts = sum(data["counts"])
            assert total_opp_counts == 4, f"Expected 4 opportunities in chart, got {total_opp_counts}"
            test_results["test_05_dashboard_opp_pipeline_chart_endpoint"] = "PASS"
            print("  -> PASS: Opportunity pipeline chart endpoint returns valid dataset")
        except Exception as e:
            test_results["test_05_dashboard_opp_pipeline_chart_endpoint"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 6: Monthly Sales Chart data endpoint
        # -------------------------------------------------------------
        print("\n[TEST 6] Monthly sales chart data endpoint...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Dashboard/GetMonthlySalesChartData")
            assert resp.status == 200
            data = json.loads(resp.read().decode("utf-8"))
            assert "months" in data and "wonAmounts" in data and "wonCounts" in data
            assert len(data["months"]) >= 6
            test_results["test_06_dashboard_monthly_sales_chart_endpoint"] = "PASS"
            print("  -> PASS: Monthly sales chart endpoint returns 6-month historical dataset")
        except Exception as e:
            test_results["test_06_dashboard_monthly_sales_chart_endpoint"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 7: Dashboard Role Scoping (SalesExecutive vs Admin)
        # -------------------------------------------------------------
        print("\n[TEST 7] Dashboard role scoping...")
        try:
            # Alex sees only Alex's data (1 customer, 2 leads, 2 opps, $20,000 open pipeline)
            resp_alex = c_alex.open(f"{BASE_URL}/Dashboard")
            html_alex = resp_alex.read().decode("utf-8")
            assert 'id="kpi-total-customers">1</div>' in html_alex, "Alex should see exactly 1 customer"
            assert 'id="kpi-total-leads">2</div>' in html_alex, "Alex should see exactly 2 leads"
            assert 'id="kpi-total-opps">2</div>' in html_alex, "Alex should see exactly 2 opportunities"
            assert 'id="kpi-open-opps">1</div>' in html_alex, "Alex should see exactly 1 open opportunity"
            assert 'id="kpi-won-opps">1</div>' in html_alex, "Alex should see exactly 1 won opportunity"

            # Chart API for Alex
            chart_resp_alex = c_alex.open(f"{BASE_URL}/Dashboard/GetLeadStatusChartData")
            chart_data_alex = json.loads(chart_resp_alex.read().decode("utf-8"))
            assert sum(chart_data_alex["counts"]) == 2, f"Alex should only see 2 leads in chart, got {sum(chart_data_alex['counts'])}"

            # Admin sees company-wide (2 customers, 3 leads, 4 opps, 2 open opps)
            resp_admin = c_admin.open(f"{BASE_URL}/Dashboard")
            html_admin = resp_admin.read().decode("utf-8")
            assert 'id="kpi-total-customers">2</div>' in html_admin, "Admin should see 2 customers"
            assert 'id="kpi-total-leads">3</div>' in html_admin, "Admin should see 3 leads"
            assert 'id="kpi-total-opps">4</div>' in html_admin, "Admin should see 4 opportunities"
            assert 'id="kpi-open-opps">2</div>' in html_admin, "Admin should see 2 open opportunities"

            test_results["test_07_dashboard_role_scoping"] = "PASS"
            print("  -> PASS: Role-based scoping verified (SalesExecutive scoped vs Admin enterprise)")
        except Exception as e:
            test_results["test_07_dashboard_role_scoping"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 8: Reports Catalog Index
        # -------------------------------------------------------------
        print("\n[TEST 8] Reports catalog index page...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports")
            html = resp.read().decode("utf-8")
            assert resp.status == 200
            assert "Customer Report" in html
            assert "Lead Report" in html
            assert "Follow-Up Report" in html
            assert "Opportunity Report" in html
            assert "Pipeline Report" in html
            assert "Conversion Report" in html
            assert "User Activity Report" in html
            assert "Audit Report" in html

            test_results["test_08_reports_index_catalog"] = "PASS"
            print("  -> PASS: Reports catalog index accessible with all 8 report tiles")
        except Exception as e:
            test_results["test_08_reports_index_catalog"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 9: Customer Report view and filtering
        # -------------------------------------------------------------
        print("\n[TEST 9] Customer Report view and filtering...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/Customers?searchTerm=Acme")
            html = resp.read().decode("utf-8")
            assert resp.status == 200
            assert "Alex Acme Corp" in html
            assert "Sarah Stark Ind" not in html

            test_results["test_09_customer_report_view_and_filtering"] = "PASS"
            print("  -> PASS: Customer Report renders and filters properly")
        except Exception as e:
            test_results["test_09_customer_report_view_and_filtering"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 10: Customer Report CSV export
        # -------------------------------------------------------------
        print("\n[TEST 10] Customer Report CSV export...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/ExportCustomers")
            assert resp.status == 200
            assert "text/csv" in resp.headers.get("Content-Type", "")
            csv_text = resp.read().decode("utf-8")
            lines = [l.strip() for l in csv_text.splitlines() if l.strip()]
            assert len(lines) >= 3, f"Expected header + 2 customers, got {len(lines)} lines"
            assert "Customer Code,Customer Name" in lines[0]
            assert "Alex Acme Corp" in csv_text
            assert "Sarah Stark Ind" in csv_text

            test_results["test_10_customer_report_csv_export"] = "PASS"
            print("  -> PASS: Customer Report CSV export generated correctly")
        except Exception as e:
            test_results["test_10_customer_report_csv_export"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 11: Lead Report view and filtering
        # -------------------------------------------------------------
        print("\n[TEST 11] Lead Report view and filtering...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/Leads?source=Referral")
            html = resp.read().decode("utf-8")
            assert resp.status == 200
            assert "Lead Converted Alex" in html
            assert "Lead Inbound Alex" not in html

            test_results["test_11_lead_report_view_and_filtering"] = "PASS"
            print("  -> PASS: Lead Report renders and filters by source")
        except Exception as e:
            test_results["test_11_lead_report_view_and_filtering"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 12: Lead Report CSV export
        # -------------------------------------------------------------
        print("\n[TEST 12] Lead Report CSV export...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/ExportLeads")
            assert resp.status == 200
            assert "text/csv" in resp.headers.get("Content-Type", "")
            csv_text = resp.read().decode("utf-8")
            lines = [l.strip() for l in csv_text.splitlines() if l.strip()]
            assert len(lines) >= 4, f"Expected header + 3 leads, got {len(lines)} lines"
            assert "Lead Code,Lead Name" in lines[0]
            assert "Lead Inbound Alex" in csv_text
            assert "Lead Qualified Sarah" in csv_text

            test_results["test_12_lead_report_csv_export"] = "PASS"
            print("  -> PASS: Lead Report CSV export generated correctly")
        except Exception as e:
            test_results["test_12_lead_report_csv_export"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 13: Follow-Up Report view and filtering
        # -------------------------------------------------------------
        print("\n[TEST 13] Follow-Up Report view and filtering...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/FollowUps?status=Completed")
            html = resp.read().decode("utf-8")
            assert resp.status == 200
            assert "Sarah Completed Email" in html
            assert "Alex Future Call" not in html

            test_results["test_13_followup_report_view_and_filtering"] = "PASS"
            print("  -> PASS: Follow-Up Report renders and filters by status")
        except Exception as e:
            test_results["test_13_followup_report_view_and_filtering"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 14: Follow-Up Report CSV export
        # -------------------------------------------------------------
        print("\n[TEST 14] Follow-Up Report CSV export...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/ExportFollowUps")
            assert resp.status == 200
            assert "text/csv" in resp.headers.get("Content-Type", "")
            csv_text = resp.read().decode("utf-8")
            lines = [l.strip() for l in csv_text.splitlines() if l.strip()]
            assert len(lines) >= 4, f"Expected header + 3 followups, got {len(lines)} lines"
            assert "Subject,Type,Status" in lines[0]
            assert "Alex Future Call" in csv_text
            assert "Alex Overdue Meeting" in csv_text

            test_results["test_14_followup_report_csv_export"] = "PASS"
            print("  -> PASS: Follow-Up Report CSV export generated correctly")
        except Exception as e:
            test_results["test_14_followup_report_csv_export"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 15: Opportunity Report view and filtering
        # -------------------------------------------------------------
        print("\n[TEST 15] Opportunity Report view and filtering...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/Opportunities?stage=Won")
            html = resp.read().decode("utf-8")
            assert resp.status == 200
            assert "Alex Closed Deal" in html
            assert "Alex Enterprise Deal" not in html

            test_results["test_15_opportunity_report_view_and_filtering"] = "PASS"
            print("  -> PASS: Opportunity Report renders and filters by stage")
        except Exception as e:
            test_results["test_15_opportunity_report_view_and_filtering"] = f"FAIL: {type(e).__name__}: {e}"
            print(f"  -> FAIL: {type(e).__name__}: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 16: Opportunity Report CSV export
        # -------------------------------------------------------------
        print("\n[TEST 16] Opportunity Report CSV export...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/ExportOpportunities")
            assert resp.status == 200
            assert "text/csv" in resp.headers.get("Content-Type", "")
            csv_text = resp.read().decode("utf-8")
            lines = [l.strip() for l in csv_text.splitlines() if l.strip()]
            assert len(lines) >= 5, f"Expected header + 4 deals, got {len(lines)} lines"
            assert "Opportunity Name,Customer,Stage" in lines[0]
            assert "Alex Enterprise Deal" in csv_text
            assert "Sarah Stark Cloud Deal" in csv_text

            test_results["test_16_opportunity_report_csv_export"] = "PASS"
            print("  -> PASS: Opportunity Report CSV export generated correctly")
        except Exception as e:
            test_results["test_16_opportunity_report_csv_export"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")

        # -------------------------------------------------------------
        # TEST 17: Pipeline Report and CSV export
        # -------------------------------------------------------------
        print("\n[TEST 17] Pipeline Report and CSV export...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/Pipeline")
            html = resp.read().decode("utf-8")
            assert resp.status == 200
            assert "Stage-Wise Pipeline Breakdown" in html
            assert "Active Open Deals" in html

            # CSV
            resp_csv = c_admin.open(f"{BASE_URL}/Reports/ExportPipeline")
            assert resp_csv.status == 200
            assert "text/csv" in resp_csv.headers.get("Content-Type", "")
            csv_text = resp_csv.read().decode("utf-8")
            assert "Pipeline Stage" in csv_text and "Total Value" in csv_text

            test_results["test_17_pipeline_report_and_csv_export"] = "PASS"
            print("  -> PASS: Pipeline Report and CSV export verified")
        except Exception as e:
            test_results["test_17_pipeline_report_and_csv_export"] = f"FAIL: {type(e).__name__}: {e}"
            print(f"  -> FAIL: {type(e).__name__}: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 18: Conversion Report and CSV export
        # -------------------------------------------------------------
        print("\n[TEST 18] Conversion Report and CSV export...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/Conversion")
            html = resp.read().decode("utf-8")
            assert resp.status == 200
            assert "Conversion Rate by Lead Source" in html
            assert "Converted Leads" in html

            # CSV
            resp_csv = c_admin.open(f"{BASE_URL}/Reports/ExportConversion")
            assert resp_csv.status == 200
            assert "text/csv" in resp_csv.headers.get("Content-Type", "")
            csv_text = resp_csv.read().decode("utf-8")
            assert "Lead Source,Total Leads,Converted Leads" in csv_text

            test_results["test_18_conversion_report_and_csv_export"] = "PASS"
            print("  -> PASS: Conversion Report and CSV export verified")
        except Exception as e:
            test_results["test_18_conversion_report_and_csv_export"] = f"FAIL: {type(e).__name__}: {e}"
            print(f"  -> FAIL: {type(e).__name__}: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 19: User Activity Report and CSV export
        # -------------------------------------------------------------
        print("\n[TEST 19] User Activity Report and CSV export...")
        try:
            resp = c_admin.open(f"{BASE_URL}/Reports/UserActivity")
            html = resp.read().decode("utf-8")
            assert resp.status == 200
            assert "Sales Team Activity Breakdown" in html
            assert "Alex Mercer" in html or "Alex" in html

            # CSV
            resp_csv = c_admin.open(f"{BASE_URL}/Reports/ExportUserActivity")
            assert resp_csv.status == 200
            assert "text/csv" in resp_csv.headers.get("Content-Type", "")
            csv_text = resp_csv.read().decode("utf-8")
            assert "User,Role,Total Activities" in csv_text

            test_results["test_19_user_activity_report_and_csv_export"] = "PASS"
            print("  -> PASS: User Activity Report and CSV export verified")
        except Exception as e:
            test_results["test_19_user_activity_report_and_csv_export"] = f"FAIL: {type(e).__name__}: {e}"
            print(f"  -> FAIL: {type(e).__name__}: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 20: Audit Report access control and scoping tamper resistance
        # -------------------------------------------------------------
        print("\n[TEST 20] Audit Report admin access and security enforcement...")
        try:
            # 1. Admin can access Audit Report
            resp_audit = c_admin.open(f"{BASE_URL}/Reports/Audit")
            assert resp_audit.status == 200
            html_audit = resp_audit.read().decode("utf-8")
            assert "System Audit Log Report" in html_audit

            # Admin can export Audit CSV
            resp_audit_csv = c_admin.open(f"{BASE_URL}/Reports/ExportAudit")
            assert resp_audit_csv.status == 200
            assert "Timestamp,User,Action,Entity" in resp_audit_csv.read().decode("utf-8")

            # 2. SalesExecutive access is denied
            resp_alex_audit = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Reports/Audit")
            code_alex = resp_alex_audit.status if hasattr(resp_alex_audit, "status") else resp_alex_audit.code
            loc_alex = resp_alex_audit.headers.get("Location") if hasattr(resp_alex_audit, "headers") else ""
            assert code_alex == 403 or (code_alex == 302 and "AccessDenied" in loc_alex), f"Expected forbidden/AccessDenied for Alex, got status {code_alex}, loc {loc_alex}"

            resp_alex_csv = safe_open_no_redirect(c_alex_nr, f"{BASE_URL}/Reports/ExportAudit")
            code_alex_csv = resp_alex_csv.status if hasattr(resp_alex_csv, "status") else resp_alex_csv.code
            loc_alex_csv = resp_alex_csv.headers.get("Location") if hasattr(resp_alex_csv, "headers") else ""
            assert code_alex_csv == 403 or (code_alex_csv == 302 and "AccessDenied" in loc_alex_csv), f"Expected forbidden/AccessDenied for Alex CSV, got status {code_alex_csv}, loc {loc_alex_csv}"

            # 3. Tamper resistance: Alex attempts to view Sarah's records via query string (?assignedToUserId=sarah_id)
            resp_tamper = c_alex.open(f"{BASE_URL}/Reports/Customers?assignedToUserId={sarah_id}")
            html_tamper = resp_tamper.read().decode("utf-8")
            assert "Alex Acme Corp" in html_tamper, "Alex's own records must be displayed"
            assert "Sarah Stark Ind" not in html_tamper, "Tampered query string must NOT expose other user's records to SalesExecutive!"

            test_results["test_20_audit_report_access_and_scoping"] = "PASS"
            print("  -> PASS: Audit Report security & SalesExecutive tamper resistance verified")
        except Exception as e:
            test_results["test_20_audit_report_access_and_scoping"] = f"FAIL: {type(e).__name__}: {e or str(e)}"
            print(f"  -> FAIL: {type(e).__name__}: {e}")
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
    print("\n" + "=" * 70)
    print("PHASE 6 TEST EXECUTION SUMMARY:")
    print("=" * 70)
    passed = 0
    failed = 0
    for name, res in test_results.items():
        status = "PASSED" if res == "PASS" else "FAILED"
        print(f"[{status}] {name}: {res}")
        if res == "PASS":
            passed += 1
        else:
            failed += 1

    print("-" * 70)
    print(f"TOTAL: {len(test_results)} | PASSED: {passed} | FAILED: {failed}")
    print("=" * 70)

    if failed > 0:
        sys.exit(1)

if __name__ == "__main__":
    run_tests()
