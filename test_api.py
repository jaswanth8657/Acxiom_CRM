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

BASE_URL = "http://127.0.0.1:5257"
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

def seed_test_records(alex_id, sarah_id, admin_id):
    conn = sqlite3.connect("AcxiomCRM.db")
    cur = conn.cursor()

    # Clear prior test records for deterministic API testing
    cur.execute("DELETE FROM FollowUps")
    cur.execute("DELETE FROM Activities")
    cur.execute("DELETE FROM Opportunities")
    cur.execute("DELETE FROM Leads")
    cur.execute("DELETE FROM Customers")
    cur.execute("DELETE FROM AuditLogs")
    conn.commit()

    now = datetime.datetime.now(datetime.timezone.utc).strftime("%Y-%m-%d %H:%M:%S")
    future_date = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=30)).strftime("%Y-%m-%d %H:%M:%S")

    # 1. Customers: 1 for Alex, 1 for Sarah
    cur.execute("""
        INSERT INTO Customers (CustomerCode, CustomerName, Email, Phone, CompanyName, Status, CreatedDate, CreatedBy, AssignedToUserId)
        VALUES ('CUST-API-001', 'Alex Customer Corp', 'alex.customer@api-example.com', '555-0101', 'Alex Corp', 1, ?, 'admin', ?)
    """, (now, alex_id))
    cust_alex_id = cur.lastrowid

    cur.execute("""
        INSERT INTO Customers (CustomerCode, CustomerName, Email, Phone, CompanyName, Status, CreatedDate, CreatedBy, AssignedToUserId)
        VALUES ('CUST-API-002', 'Sarah Customer Ind', 'sarah.customer@api-example.com', '555-0102', 'Sarah Ind', 1, ?, 'admin', ?)
    """, (now, sarah_id))
    cust_sarah_id = cur.lastrowid

    # 2. Leads: 1 for Alex, 1 for Sarah
    cur.execute("""
        INSERT INTO Leads (LeadCode, LeadName, Email, Phone, CompanyName, Source, Status, ExpectedValue, CreatedDate, CreatedBy, AssignedToUserId)
        VALUES ('LEAD-API-001', 'Alex Lead One', 'alex.lead@api-example.com', '555-0201', 'Alex Tech', 'Website', 1, 15000.0, ?, 'admin', ?)
    """, (now, alex_id))
    lead_alex_id = cur.lastrowid

    cur.execute("""
        INSERT INTO Leads (LeadCode, LeadName, Email, Phone, CompanyName, Source, Status, ExpectedValue, CreatedDate, CreatedBy, AssignedToUserId)
        VALUES ('LEAD-API-002', 'Sarah Lead One', 'sarah.lead@api-example.com', '555-0202', 'Sarah Systems', 'LinkedIn', 3, 40000.0, ?, 'admin', ?)
    """, (now, sarah_id))
    lead_sarah_id = cur.lastrowid

    # 3. Opportunities: 1 for Alex, 1 for Sarah
    cur.execute("""
        INSERT INTO Opportunities (OpportunityName, CustomerId, LeadId, Amount, Stage, Probability, ExpectedCloseDate, Status, CreatedDate, AssignedToUserId)
        VALUES ('Alex Big Deal', ?, ?, 25000.0, 2, 40, ?, 1, ?, ?)
    """, (cust_alex_id, lead_alex_id, future_date, now, alex_id))
    opp_alex_id = cur.lastrowid

    cur.execute("""
        INSERT INTO Opportunities (OpportunityName, CustomerId, LeadId, Amount, Stage, Probability, ExpectedCloseDate, Status, CreatedDate, AssignedToUserId)
        VALUES ('Sarah Mega Deal', ?, ?, 60000.0, 3, 60, ?, 1, ?, ?)
    """, (cust_sarah_id, lead_sarah_id, future_date, now, sarah_id))
    opp_sarah_id = cur.lastrowid

    conn.commit()
    conn.close()

    return {
        "cust_alex_id": cust_alex_id,
        "cust_sarah_id": cust_sarah_id,
        "lead_alex_id": lead_alex_id,
        "lead_sarah_id": lead_sarah_id,
        "opp_alex_id": opp_alex_id,
        "opp_sarah_id": opp_sarah_id,
    }

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
    print("=" * 75)
    print("STARTING COMPREHENSIVE PHASE 7 REST API VERIFICATION SUITE")
    print("=" * 75)

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

        conn = sqlite3.connect("AcxiomCRM.db")
        c = conn.cursor()
        c.execute("SELECT Id FROM AspNetUsers WHERE Email = 'admin@acxiomcrm.local'")
        admin_id = c.fetchone()[0]
        c.execute("SELECT Id FROM AspNetUsers WHERE Email = 'alex.sales@acxiomcrm.local'")
        alex_id = c.fetchone()[0]
        c.execute("SELECT Id FROM AspNetUsers WHERE Email = 'sarah.sales@acxiomcrm.local'")
        sarah_id = c.fetchone()[0]
        conn.close()

        seeded = seed_test_records(alex_id, sarah_id, admin_id)

        # Clients
        c_anon, _, _ = create_client()
        c_admin, c_admin_nr, _ = create_client()
        login(c_admin, c_admin_nr, "admin@acxiomcrm.local", "Admin@Acxiom2026!")

        c_alex, c_alex_nr, _ = create_client()
        login(c_alex, c_alex_nr, "alex.sales@acxiomcrm.local", "Sales@Acxiom2026!")

        c_sarah, c_sarah_nr, _ = create_client()
        login(c_sarah, c_sarah_nr, "sarah.sales@acxiomcrm.local", "Sales@Acxiom2026!")

        # -------------------------------------------------------------
        # TEST 1: Swagger OpenAPI JSON & Swagger UI
        # -------------------------------------------------------------
        print("\n[TEST 1] Swagger OpenAPI specification & Swagger UI endpoints...")
        try:
            status, swagger_doc, _, _ = api_request(c_anon, f"{BASE_URL}/swagger/v1/swagger.json")
            assert status == 200, f"Swagger doc returned {status}"
            assert "paths" in swagger_doc, "Swagger doc missing 'paths' object"
            assert "/api/customers" in swagger_doc["paths"], "Swagger doc missing /api/customers endpoint"
            assert "/api/leads" in swagger_doc["paths"], "Swagger doc missing /api/leads endpoint"
            assert "/api/opportunities" in swagger_doc["paths"], "Swagger doc missing /api/opportunities endpoint"

            resp_ui = safe_open(c_anon, f"{BASE_URL}/swagger/index.html")
            ui_code = resp_ui.status if hasattr(resp_ui, "status") else resp_ui.code
            assert ui_code == 200, f"Swagger UI returned {ui_code}"
            assert "swagger-ui" in resp_ui.read().decode("utf-8").lower(), "Swagger UI HTML missing"

            test_results["test_01_swagger_endpoints"] = "PASS"
            print("  -> PASS: Swagger JSON and UI endpoints operational")
        except Exception as e:
            test_results["test_01_swagger_endpoints"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 2: Anonymous GET/POST endpoints return 401 Unauthorized
        # -------------------------------------------------------------
        print("\n[TEST 2] Anonymous requests return HTTP 401 Unauthorized (not 302 redirect)...")
        try:
            status_cust, body_cust, _, raw_cust = api_request(c_anon, f"{BASE_URL}/api/customers")
            assert status_cust == 401, f"Expected 401 for GET /api/customers, got {status_cust}"
            assert "Authentication required" in raw_cust, "Expected JSON message explaining auth requirement"

            status_leads, _, _, _ = api_request(c_anon, f"{BASE_URL}/api/leads")
            assert status_leads == 401, f"Expected 401 for GET /api/leads, got {status_leads}"

            status_opps, _, _, _ = api_request(c_anon, f"{BASE_URL}/api/opportunities")
            assert status_opps == 401, f"Expected 401 for GET /api/opportunities, got {status_opps}"

            status_post, _, _, _ = api_request(c_anon, f"{BASE_URL}/api/customers", method="POST", payload={
                "customerName": "Anon Hack",
                "email": "anon@bad.com",
                "phone": "555-9999"
            })
            assert status_post == 401, f"Expected 401 for POST /api/customers, got {status_post}"

            test_results["test_02_anonymous_auth_enforcement"] = "PASS"
            print("  -> PASS: 401 Unauthorized strictly enforced for unauthenticated requests")
        except Exception as e:
            test_results["test_02_anonymous_auth_enforcement"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 3: Admin GET /api/customers & DTO Security
        # -------------------------------------------------------------
        print("\n[TEST 3] Admin GET /api/customers & DTO security verification...")
        try:
            status, json_data, _, _ = api_request(c_admin, f"{BASE_URL}/api/customers")
            assert status == 200, f"Expected 200, got {status}"
            assert "items" in json_data and "totalItems" in json_data, "Response missing pagination DTO"
            assert json_data["totalItems"] >= 2, f"Expected at least 2 customers, got {json_data['totalItems']}"
            
            # Security check on returned payload
            check_no_security_fields(json_data)

            test_results["test_03_admin_get_customers_and_dto_security"] = "PASS"
            print("  -> PASS: Admin retrieved customers; no password or sensitive identity fields exposed")
        except Exception as e:
            test_results["test_03_admin_get_customers_and_dto_security"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 4: Admin POST /api/customers (Create Customer) -> 201 Created
        # -------------------------------------------------------------
        created_customer_id = None
        print("\n[TEST 4] Admin POST /api/customers (Create Customer)...")
        try:
            payload = {
                "customerName": "Omni Global Logistics",
                "email": "omni.logistics@example.com",
                "phone": "555-7788",
                "companyName": "Omni Global",
                "address": "100 Logistics Blvd",
                "city": "Chicago",
                "state": "IL"
            }
            status, data, headers, _ = api_request(c_admin, f"{BASE_URL}/api/customers", method="POST", payload=payload)
            assert status == 201, f"Expected 201 Created, got {status}"
            assert "id" in data, "Created DTO missing id"
            created_customer_id = data["id"]
            assert data["customerName"] == "Omni Global Logistics", f"Unexpected name: {data.get('customerName')}"
            assert headers.get("Location") is not None, "201 Created response missing Location header"

            check_no_security_fields(data)

            test_results["test_04_admin_create_customer_201"] = "PASS"
            print(f"  -> PASS: Customer created with ID {created_customer_id} (201 Created)")
        except Exception as e:
            test_results["test_04_admin_create_customer_201"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 5: Customer Validation Failures (Invalid Email & Duplicate Email)
        # -------------------------------------------------------------
        print("\n[TEST 5] Customer validation failures (bad email format & duplicate)...")
        try:
            bad_email_payload = {
                "customerName": "Invalid Email Corp",
                "email": "not-an-email",
                "phone": "555-1234"
            }
            status_bad_email, _, _, _ = api_request(c_admin, f"{BASE_URL}/api/customers", method="POST", payload=bad_email_payload)
            assert status_bad_email == 400, f"Expected 400 for bad email, got {status_bad_email}"

            duplicate_payload = {
                "customerName": "Duplicate Corp",
                "email": "omni.logistics@example.com",  # Duplicate!
                "phone": "555-9988"
            }
            status_dup, _, _, _ = api_request(c_admin, f"{BASE_URL}/api/customers", method="POST", payload=duplicate_payload)
            assert status_dup in (400, 409), f"Expected 400 or 409 for duplicate email, got {status_dup}"

            test_results["test_05_customer_validation_errors"] = "PASS"
            print("  -> PASS: Bad email rejected with 400; duplicate email rejected with 400/409")
        except Exception as e:
            test_results["test_05_customer_validation_errors"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 6: Admin GET & PUT /api/customers/{id}
        # -------------------------------------------------------------
        print("\n[TEST 6] Admin GET and PUT /api/customers/{id}...")
        try:
            status_get, data_get, _, _ = api_request(c_admin, f"{BASE_URL}/api/customers/{created_customer_id}")
            assert status_get == 200, f"Expected 200, got {status_get}"
            assert data_get["customerName"] == "Omni Global Logistics"

            update_payload = {
                "customerName": "Omni Global Logistics International",
                "email": "omni.logistics@example.com",
                "phone": "555-7799",
                "companyName": "Omni Global Intl",
                "address": "200 Logistics Blvd",
                "city": "Chicago",
                "state": "IL",
                "status": 1
            }
            status_put, data_put, _, _ = api_request(c_admin, f"{BASE_URL}/api/customers/{created_customer_id}", method="PUT", payload=update_payload)
            assert status_put == 200, f"Expected 200, got {status_put}"
            assert data_put["customerName"] == "Omni Global Logistics International"
            assert data_put["phone"] == "555-7799"

            test_results["test_06_admin_get_and_put_customer"] = "PASS"
            print("  -> PASS: Customer retrieved and updated successfully (200 OK)")
        except Exception as e:
            test_results["test_06_admin_get_and_put_customer"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 7: Admin POST /api/leads (Create Lead) -> 201 Created
        # -------------------------------------------------------------
        created_lead_id = None
        print("\n[TEST 7] Admin POST /api/leads (Create Lead)...")
        try:
            payload = {
                "leadName": "Hyperion Solutions Lead",
                "email": "contact@hyperionsol.com",
                "phone": "555-3344",
                "companyName": "Hyperion Solutions",
                "title": "Director of IT",
                "source": "Website",
                "status": 1, # New
                "expectedValue": 35000.0,
                "assignedToUserId": alex_id
            }
            status, data, headers, _ = api_request(c_admin, f"{BASE_URL}/api/leads", method="POST", payload=payload)
            assert status == 201, f"Expected 201 Created, got {status}"
            assert "id" in data, "Created lead missing id"
            created_lead_id = data["id"]
            assert data["leadName"] == "Hyperion Solutions Lead"
            assert headers.get("Location") is not None, "201 Created missing Location header"

            check_no_security_fields(data)

            test_results["test_07_admin_create_lead_201"] = "PASS"
            print(f"  -> PASS: Lead created with ID {created_lead_id} (201 Created)")
        except Exception as e:
            test_results["test_07_admin_create_lead_201"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 8: Lead Validation Failures (Invalid Email & Duplicate)
        # -------------------------------------------------------------
        print("\n[TEST 8] Lead validation failures (bad email & duplicate)...")
        try:
            bad_lead = {
                "leadName": "Bad Lead",
                "email": "not-an-email-format",
                "source": "Website",
                "status": 1
            }
            status_bad, _, _, _ = api_request(c_admin, f"{BASE_URL}/api/leads", method="POST", payload=bad_lead)
            assert status_bad == 400, f"Expected 400 for bad email, got {status_bad}"

            dup_lead = {
                "leadName": "Dup Lead",
                "email": "contact@hyperionsol.com",
                "source": "Website",
                "status": 1
            }
            status_dup, _, _, _ = api_request(c_admin, f"{BASE_URL}/api/leads", method="POST", payload=dup_lead)
            assert status_dup in (400, 409), f"Expected 400 or 409 for duplicate lead email, got {status_dup}"

            test_results["test_08_lead_validation_errors"] = "PASS"
            print("  -> PASS: Lead bad email rejected with 400; duplicate email rejected with 400/409")
        except Exception as e:
            test_results["test_08_lead_validation_errors"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 9: Lead Status Transition Rules (New -> Qualified)
        # -------------------------------------------------------------
        print("\n[TEST 9] Lead status transition rules via API...")
        try:
            # 1. Invalid status transition: New (1) -> Converted (5) directly
            invalid_transition = {
                "leadName": "Hyperion Solutions Lead",
                "email": "contact@hyperionsol.com",
                "phone": "555-3344",
                "companyName": "Hyperion Solutions",
                "source": "Website",
                "status": 5, # Converted directly is forbidden by business rules
                "expectedValue": 35000.0,
                "assignedToUserId": alex_id
            }
            status_inv, _, _, raw_inv = api_request(c_admin, f"{BASE_URL}/api/leads/{created_lead_id}", method="PUT", payload=invalid_transition)
            assert status_inv == 400, f"Expected 400 for illegal status transition, got {status_inv}"

            # 2. Valid transition: New (1) -> Contacted (2)
            valid_transition_1 = {
                "leadName": "Hyperion Solutions Lead",
                "email": "contact@hyperionsol.com",
                "phone": "555-3344",
                "companyName": "Hyperion Solutions",
                "source": "Website",
                "status": 2, # Contacted
                "expectedValue": 35000.0,
                "assignedToUserId": alex_id,
                "statusChangeNote": "Initial outreach call completed"
            }
            status_v1, data_v1, _, _ = api_request(c_admin, f"{BASE_URL}/api/leads/{created_lead_id}", method="PUT", payload=valid_transition_1)
            assert status_v1 == 200, f"Expected 200 for Contacted transition, got {status_v1}"
            assert data_v1["status"] == "Contacted"

            # 3. Valid transition: Contacted (2) -> Qualified (3)
            valid_transition_2 = {
                "leadName": "Hyperion Solutions Lead",
                "email": "contact@hyperionsol.com",
                "phone": "555-3344",
                "companyName": "Hyperion Solutions",
                "source": "Website",
                "status": 3, # Qualified
                "expectedValue": 45000.0,
                "assignedToUserId": alex_id,
                "statusChangeNote": "BANT criteria confirmed"
            }
            status_v2, data_v2, _, _ = api_request(c_admin, f"{BASE_URL}/api/leads/{created_lead_id}", method="PUT", payload=valid_transition_2)
            assert status_v2 == 200, f"Expected 200 for Qualified transition, got {status_v2}"
            assert data_v2["status"] == "Qualified"

            test_results["test_09_lead_status_transition_rules"] = "PASS"
            print("  -> PASS: Illegal status transition rejected (400); sequential transitions succeeded (200)")
        except Exception as e:
            test_results["test_09_lead_status_transition_rules"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 10: Lead Conversion Enforcement & Execution
        # -------------------------------------------------------------
        converted_customer_id = None
        converted_opp_id = None
        print("\n[TEST 10] Lead conversion qualification gate and execution...")
        try:
            # 1. Attempting to convert an unqualified lead (seeded Alex Lead is New (1))
            status_unq, _, _, _ = api_request(c_admin, f"{BASE_URL}/api/leads/{seeded['lead_alex_id']}/convert", method="POST", payload={
                "companyName": "Alex Corp",
                "opportunityName": "Premature Deal",
                "opportunityAmount": 10000.0
            })
            assert status_unq == 400, f"Expected 400 when converting unqualified lead, got {status_unq}"

            # 2. Converting the Qualified lead created in TEST 7 & 9
            convert_payload = {
                "companyName": "Hyperion Enterprise Group",
                "opportunityName": "Hyperion Enterprise SaaS Contract",
                "opportunityAmount": 45000.0,
                "expectedCloseDate": (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=45)).isoformat()
            }
            status_conv, data_conv, _, _ = api_request(c_admin, f"{BASE_URL}/api/leads/{created_lead_id}/convert", method="POST", payload=convert_payload)
            assert status_conv == 200, f"Expected 200 for valid conversion, got {status_conv}"
            assert "customerId" in data_conv and data_conv["customerId"] is not None, "Missing customerId in conversion result"
            assert "opportunityId" in data_conv and data_conv["opportunityId"] is not None, "Missing opportunityId in conversion result"
            converted_customer_id = data_conv["customerId"]
            converted_opp_id = data_conv["opportunityId"]

            # 3. Attempting to re-convert the now-converted lead must be rejected
            status_reconv, _, _, _ = api_request(c_admin, f"{BASE_URL}/api/leads/{created_lead_id}/convert", method="POST", payload=convert_payload)
            assert status_reconv == 400, f"Expected 400 when converting already converted lead, got {status_reconv}"

            test_results["test_10_lead_conversion_workflow"] = "PASS"
            print(f"  -> PASS: Conversion gated to Qualified; converted lead -> Customer #{converted_customer_id}, Opp #{converted_opp_id}; re-conversion blocked")
        except Exception as e:
            test_results["test_10_lead_conversion_workflow"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 11: Opportunity Validation Rules (Amount, Probability, Close Date)
        # -------------------------------------------------------------
        print("\n[TEST 11] Opportunity validation rules (Amount <= 0, Prob < 0 or > 100, Past Date)...")
        try:
            # Bad Amount
            bad_amt = {
                "opportunityName": "Negative Deal",
                "customerId": created_customer_id,
                "amount": -500.0,
                "stage": 1,
                "probability": 20,
                "expectedCloseDate": (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=30)).isoformat()
            }
            status_amt, _, _, _ = api_request(c_admin, f"{BASE_URL}/api/opportunities", method="POST", payload=bad_amt)
            assert status_amt == 400, f"Expected 400 for negative amount, got {status_amt}"

            # Bad Probability
            bad_prob = {
                "opportunityName": "High Prob Deal",
                "customerId": created_customer_id,
                "amount": 10000.0,
                "stage": 1,
                "probability": 150, # Invalid > 100
                "expectedCloseDate": (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=30)).isoformat()
            }
            status_prob, _, _, _ = api_request(c_admin, f"{BASE_URL}/api/opportunities", method="POST", payload=bad_prob)
            assert status_prob == 400, f"Expected 400 for probability > 100, got {status_prob}"

            # Past Close Date
            bad_date = {
                "opportunityName": "Past Date Deal",
                "customerId": created_customer_id,
                "amount": 10000.0,
                "stage": 1,
                "probability": 50,
                "expectedCloseDate": (datetime.datetime.now(datetime.timezone.utc) - datetime.timedelta(days=10)).isoformat()
            }
            status_date, _, _, _ = api_request(c_admin, f"{BASE_URL}/api/opportunities", method="POST", payload=bad_date)
            assert status_date == 400, f"Expected 400 for past close date, got {status_date}"

            test_results["test_11_opportunity_validations"] = "PASS"
            print("  -> PASS: All opportunity business constraints verified (Amount > 0, Prob 0..100, Future Date)")
        except Exception as e:
            test_results["test_11_opportunity_validations"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 12: Admin POST /api/opportunities (Create Opportunity) -> 201 Created
        # -------------------------------------------------------------
        created_opp_id_2 = None
        print("\n[TEST 12] Admin POST /api/opportunities (Create Opportunity)...")
        try:
            future_close = (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=45)).isoformat()
            payload = {
                "opportunityName": "Direct Pipeline Deal",
                "customerId": created_customer_id,
                "amount": 75000.0,
                "stage": 2, # Qualification
                "probability": 30,
                "expectedCloseDate": future_close,
                "assignedToUserId": alex_id
            }
            status, data, headers, _ = api_request(c_admin, f"{BASE_URL}/api/opportunities", method="POST", payload=payload)
            assert status == 201, f"Expected 201 Created, got {status}"
            assert "id" in data, "Opportunity DTO missing id"
            created_opp_id_2 = data["id"]
            assert data["amount"] == 75000.0
            assert data["weightedAmount"] == 22500.0, f"Weighted amount calculation mismatch: {data.get('weightedAmount')}"
            assert headers.get("Location") is not None, "Missing Location header"

            check_no_security_fields(data)

            test_results["test_12_admin_create_opportunity_201"] = "PASS"
            print(f"  -> PASS: Opportunity created with ID {created_opp_id_2} (201 Created) with correct weighted amount")
        except Exception as e:
            test_results["test_12_admin_create_opportunity_201"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 13: SalesExecutive Scoping (Alex only sees assigned records)
        # -------------------------------------------------------------
        print("\n[TEST 13] SalesExecutive role scoping on GET endpoints...")
        try:
            # 1. Customers list as Alex
            status, alex_customers, _, _ = api_request(c_alex, f"{BASE_URL}/api/customers")
            assert status == 200
            for cust in alex_customers["items"]:
                assert cust["assignedToUserId"] == alex_id or cust["assignedToUserId"] is None, \
                    f"Alex saw unauthorized customer assigned to {cust.get('assignedToUserId')}"
                assert cust["id"] != seeded["cust_sarah_id"], "Alex must NOT see Sarah's customer!"

            # 2. Leads list as Alex
            status, alex_leads, _, _ = api_request(c_alex, f"{BASE_URL}/api/leads")
            assert status == 200
            for lead in alex_leads["items"]:
                assert lead["assignedToUserId"] == alex_id or lead["assignedToUserId"] is None, \
                    f"Alex saw unauthorized lead assigned to {lead.get('assignedToUserId')}"
                assert lead["id"] != seeded["lead_sarah_id"], "Alex must NOT see Sarah's lead!"

            # 3. Opportunities list as Alex
            status, alex_opps, _, _ = api_request(c_alex, f"{BASE_URL}/api/opportunities")
            assert status == 200
            for opp in alex_opps["items"]:
                assert opp["assignedToUserId"] == alex_id or opp["assignedToUserId"] is None, \
                    f"Alex saw unauthorized opportunity assigned to {opp.get('assignedToUserId')}"
                assert opp["id"] != seeded["opp_sarah_id"], "Alex must NOT see Sarah's opportunity!"

            test_results["test_13_sales_executive_scoping"] = "PASS"
            print("  -> PASS: SalesExecutive list endpoints are strictly filtered by assigned ownership")
        except Exception as e:
            test_results["test_13_sales_executive_scoping"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 14: SalesExecutive Cross-User Tampering Prevention (403 Forbidden)
        # -------------------------------------------------------------
        print("\n[TEST 14] Cross-user ID tampering prevention (403 Forbidden)...")
        try:
            sarah_cust = seeded["cust_sarah_id"]
            sarah_lead = seeded["lead_sarah_id"]
            sarah_opp = seeded["opp_sarah_id"]

            # Alex attempts GET Sarah's customer
            status_cg, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/customers/{sarah_cust}")
            assert status_cg in (403, 404), f"Expected 403/404 for unauthorized customer GET, got {status_cg}"

            # Alex attempts PUT Sarah's customer
            status_cp, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/customers/{sarah_cust}", method="PUT", payload={
                "customerName": "Hacked Customer",
                "email": "sarah.customer@api-example.com",
                "phone": "555-0102",
                "status": 1
            })
            assert status_cp in (403, 404), f"Expected 403/404 for unauthorized customer PUT, got {status_cp}"

            # Alex attempts DELETE Sarah's customer
            status_cd, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/customers/{sarah_cust}", method="DELETE")
            assert status_cd in (403, 404), f"Expected 403/404 for unauthorized customer DELETE, got {status_cd}"

            # Alex attempts GET Sarah's lead
            status_lg, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/leads/{sarah_lead}")
            assert status_lg in (403, 404), f"Expected 403/404 for unauthorized lead GET, got {status_lg}"

            # Alex attempts PUT Sarah's lead
            status_lp, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/leads/{sarah_lead}", method="PUT", payload={
                "leadName": "Hacked Lead",
                "email": "sarah.lead@api-example.com",
                "source": "LinkedIn",
                "status": 3
            })
            assert status_lp in (403, 404), f"Expected 403/404 for unauthorized lead PUT, got {status_lp}"

            # Alex attempts POST convert on Sarah's lead
            status_lc, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/leads/{sarah_lead}/convert", method="POST", payload={
                "opportunityName": "Hacked Opp"
            })
            assert status_lc in (403, 404), f"Expected 403/404 for unauthorized lead convert, got {status_lc}"

            # Alex attempts GET Sarah's opportunity
            status_og, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/opportunities/{sarah_opp}")
            assert status_og in (403, 404), f"Expected 403/404 for unauthorized opportunity GET, got {status_og}"

            # Alex attempts PUT Sarah's opportunity
            status_op, _, _, _ = api_request(c_alex, f"{BASE_URL}/api/opportunities/{sarah_opp}", method="PUT", payload={
                "opportunityName": "Hacked Deal",
                "amount": 1000.0,
                "stage": 3,
                "probability": 50,
                "expectedCloseDate": (datetime.datetime.now(datetime.timezone.utc) + datetime.timedelta(days=30)).isoformat()
            })
            assert status_op in (403, 404), f"Expected 403/404 for unauthorized opportunity PUT, got {status_op}"

            test_results["test_14_cross_user_tampering_prevention"] = "PASS"
            print("  -> PASS: All 8 cross-user ID tampering attempts blocked with 403 Forbidden")
        except Exception as e:
            test_results["test_14_cross_user_tampering_prevention"] = f"FAIL: {e}"
            print(f"  -> FAIL: {e}")
            traceback.print_exc()

        # -------------------------------------------------------------
        # TEST 15: Admin DELETE /api/customers/{id} (Deactivate)
        # -------------------------------------------------------------
        print("\n[TEST 15] Admin DELETE /api/customers/{id} (Deactivate)...")
        try:
            status_del, data_del, _, _ = api_request(c_admin, f"{BASE_URL}/api/customers/{created_customer_id}", method="DELETE")
            assert status_del == 200, f"Expected 200 for customer delete, got {status_del}"

            # Re-fetch customer to verify status is Inactive
            status_get, data_get, _, _ = api_request(c_admin, f"{BASE_URL}/api/customers/{created_customer_id}")
            assert status_get == 200
            assert data_get["status"] == "Inactive", f"Expected Inactive status, got {data_get.get('status')}"

            test_results["test_15_admin_delete_customer"] = "PASS"
            print(f"  -> PASS: Customer #{created_customer_id} successfully deactivated (status = Inactive)")
        except Exception as e:
            test_results["test_15_admin_delete_customer"] = f"FAIL: {e}"
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
    print("\n" + "=" * 75)
    print("PHASE 7 REST API TEST EXECUTION SUMMARY:")
    print("=" * 75)
    passed = 0
    failed = 0
    for name, res in test_results.items():
        status = "PASSED" if res == "PASS" else "FAILED"
        print(f"[{status}] {name}: {res}")
        if res == "PASS":
            passed += 1
        else:
            failed += 1

    print("-" * 75)
    print(f"TOTAL: {len(test_results)} | PASSED: {passed} | FAILED: {failed}")
    print("=" * 75)

    if failed > 0:
        sys.exit(1)

if __name__ == "__main__":
    run_tests()
