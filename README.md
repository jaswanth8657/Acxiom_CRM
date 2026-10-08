# AcxiomCRM

## Overview
AcxiomCRM is an enterprise-grade Customer Relationship Management system developed using ASP.NET Core 8. Built to streamline sales operations, customer onboarding, pipeline tracking, and team activities, AcxiomCRM delivers strict role-based access control, automated lead-to-opportunity conversion workflows, interactive dashboard analytics, and secured RESTful APIs.

---

## Features
- **Authentication & Authorization**: Identity-backed session security with cookie authentication and strict RBAC.
- **Three-Tier RBAC**: Granular permissions across `Admin`, `Manager`, and `SalesExecutive` roles.
- **Customer Management**: Complete customer lifecycle, auto-generated codes (`CUST-YYYYMM-XXXX`), search, filtering, and history tracking.
- **Lead Management**: Lead qualification pipelines, automated code generation (`LEAD-YYYYMM-XXXX`), and assignment.
- **Atomic Lead Conversion**: Single-transaction conversion of qualified leads into linked Customer and Opportunity records.
- **Opportunity & Sales Pipeline**: Multi-stage deal tracker (`Prospecting`, `Qualification`, `Proposal`, `Negotiation`, `ClosedWon`, `ClosedLost`) with automated win probabilities and weighted pipeline calculation.
- **Follow-Up & Activity Management**: Multi-channel interaction scheduling (`Call`, `Email`, `Meeting`, `Demo`), overdue detection, and audit activity streaming.
- **Dashboard & Analytics**: Role-filtered KPI metric cards, Chart.js trend visualizations, and upcoming task trackers.
- **Reports & Data Export**: 8 business intelligence reports with on-demand CSV data export capabilities.
- **Audit Logging**: Comprehensive, non-repudiable audit trails capturing entity mutations, user changes, and workflow state transitions.
- **Secured REST API**: Versioned endpoints (`/api/customers`, `/api/leads`, `/api/opportunities`, `/api/dashboard/stats`) with OpenAPI / Swagger documentation.
- **Security Hardening**: Anti-CSRF protection on all mutating actions, server-enforced IDOR protection, parameterized queries preventing SQL injection, and zero sensitive credential leakage.

---

## Technology Stack
- **Framework**: ASP.NET Core 8 (MVC + Web API)
- **Language**: C# 12 (.NET 8 SDK)
- **Data Access & ORM**: Entity Framework Core 8
- **Database**: SQLite (`Microsoft.EntityFrameworkCore.Sqlite`)
- **Identity & Security**: ASP.NET Core Identity with role-based policies
- **Front-End & UI**: Bootstrap 5, Bootstrap Icons, jQuery, jQuery Validation
- **Visual Analytics**: Chart.js
- **API Documentation**: Swashbuckle / Swagger UI (OpenAPI)

---

## Architecture

AcxiomCRM follows a clean layered architecture adhering to separation of concerns and maintainability principles:

```
┌────────────────────────────────────────────────────────┐
│             Presentation Layer                         │
│  - ASP.NET Core MVC (Razor Views & ViewModels)         │
│  - ASP.NET Core Web API Controllers & DTOs             │
│  - Chart.js UI & Responsive Bootstrap Layout           │
├────────────────────────────────────────────────────────┤
│             Application & Service Layer                │
│  - CustomerService, LeadService, OpportunityService    │
│  - FollowUpService, DashboardService, ReportService    │
│  - AuditLogService                                     │
├────────────────────────────────────────────────────────┤
│             Domain & Business Logic                    │
│  - Core Entities: Customer, Lead, Opportunity,          │
│    FollowUp, Activity, AuditLog, ApplicationUser       │
│  - Business Rules, State Machines, Probability Math    │
├────────────────────────────────────────────────────────┤
│             Data Access Layer                          │
│  - ApplicationDbContext (EF Core)                      │
│  - DbInitializer (Database Seeding & Roles)            │
│  - LINQ Parameterized Queries & Transactions           │
├────────────────────────────────────────────────────────┤
│             Database Provider                          │
│  - SQLite (AcxiomCRM.db)                               │
├────────────────────────────────────────────────────────┤
│             Security & Cross-Cutting                   │
│  - ASP.NET Core Identity                               │
│  - Anti-Forgery Validation                             │
│  - Server-Side IDOR & Ownership Validation             │
│  - Global Exception Handling & Serilog Logging         │
└────────────────────────────────────────────────────────┘
```

---

## Main CRM Workflow

The platform standardizes the B2B sales cycle from initial contact to revenue realization:

```
[ New Lead ]
     │
     ▼
[ Contacted ]
     │
     ▼
[ Qualified ]
     │
     ├── (Atomic Lead Conversion)
     ▼
┌──────────────────┐      ┌─────────────────────────┐
│ Customer Record  │ ───► │  Opportunity Created    │
└──────────────────┘      └─────────────────────────┘
                                       │
                                       ▼
     ┌──────────────────────────────────────────────────────────────┐
     │ Scheduled Follow-Ups & Activities (Calls, Meetings, Demos)   │
     └──────────────────────────────────────────────────────────────┘
                                       │
                                       ▼
     ┌──────────────────────────────────────────────────────────────┐
     │ Opportunity Pipeline:                                        │
     │ Prospecting (10%) ─► Qualification (25%) ─► Proposal (50%)   │
     │ ─► Negotiation (75%) ─► Closed Won (100%) / Closed Lost (0%) │
     └──────────────────────────────────────────────────────────────┘
```

---

## Roles & Permissions

### Admin
- Full system governance and administrative control.
- User management: create users, assign roles, toggle active status.
- Access to raw audit logs and system-wide reports.
- Hard/soft deletion authority on customers and pipeline records.

### Manager
- Organization-wide oversight of all customers, leads, and opportunities.
- Lead and deal assignment across sales representatives.
- Access to high-level executive dashboards and performance reports.
- Business reporting and CSV export.

### SalesExecutive
- Field execution restricted to assigned accounts and opportunities.
- Ownership-gated access (IDOR protection prevents viewing or mutating peers' records).
- Lead progression, conversion, and opportunity stage advancement.
- Scheduling and completing assigned follow-ups and customer activities.

---

## Testing & Quality Assurance

AcxiomCRM was developed under rigorous test-driven validation, achieving **100% pass rate across 142 automated acceptance tests** with zero compiler warnings and zero build errors.

```text
Build succeeded:
    0 Warning(s)
    0 Error(s)
```

### Automated Acceptance Suites:
- **Customer Management Suite (`test_phase2.py`)**: 22 / 22 Passed
- **Lead Management & Conversion Suite (`test_phase3.py`)**: 16 / 16 Passed
- **Opportunity & Pipeline Suite (`test_phase4.py`)**: 24 / 24 Passed
- **Follow-Up & Activity Management Suite (`test_phase5.py`)**: 24 / 24 Passed
- **Dashboard, Analytics & Reports Suite (`test_phase6.py`)**: 20 / 20 Passed
- **Secured REST API Suite (`test_api.py`)**: 15 / 15 Passed
- **End-to-End Evaluator Journey Suite (`test_phase8_evaluator.py`)**: 21 / 21 Passed
- **Total**: **142 / 142 Passed (100%)**

---

## Running Locally

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher.
- A modern web browser.

### Setup Instructions

1. **Clone the repository:**
   ```bash
   git clone https://github.com/jaswanth8657/Acxiom_CRM.git
   cd Acxiom_CRM
   ```

2. **Restore dependencies:**
   ```bash
   dotnet restore
   ```

3. **Build the application:**
   ```bash
   dotnet build
   ```

4. **Initialize Database & Seed Data:**
   The application uses Entity Framework Core with automatic database creation and initial seeding on startup.
   Alternatively, apply migrations manually:
   ```bash
   dotnet ef database update
   ```

5. **Run the application:**
   ```bash
   dotnet run
   ```

6. **Access the CRM:**
   - Web Portal: Navigate to `https://localhost:5001` or `http://localhost:5000`
   - Swagger / OpenAPI Docs: Navigate to `/swagger` (available in Development environment)

### Default Roles Seeded on First Run
On initial startup, `DbInitializer` provisions standard demonstration roles (`Admin`, `Manager`, `SalesExecutive`). Refer to project administrator configurations or configure an initial administrator account using ASP.NET Core configuration secrets.

---

## License
This project is developed for evaluation and educational purposes under the Acxiom technical assessment framework.
