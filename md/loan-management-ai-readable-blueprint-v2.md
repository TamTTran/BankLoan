# Loan Management System — Master Planning Doc / AI-Readable Blueprint v2

## 1. Document Metadata

- **Document Name:** Loan Management System Master Planning Doc / AI-Readable Blueprint
- **Version:** v2.0
- **Purpose:** Single source of truth for BA, Dev, QA, PM, and AI coding assistants
- **Project Codename:** LoanFlow Lite
- **Target Market:** Small lenders, installment retailers, SME lenders, microfinance-style operations
- **Scope Type:** MVP for demo and first customer deployment
- **System Type:** Internal back-office loan management system
- **Deployment Mode:** Web-based admin portal
- **Default Timezone:** Asia/Ho_Chi_Minh
- **Default Currency:** VND
- **Decimal Precision:** 2
- **Rounding Mode:** Round half up
- **Architecture Direction:** Modular monolith first, expand later only if needed

---

## 2. Executive Summary

This product manages the end-to-end lifecycle of a retail or SME-style loan from customer creation through application, approval, disbursement, repayment tracking, overdue handling, and reporting.

### Core flow
Customer -> Loan Product -> Loan Application -> Approval -> Loan Account -> Disbursement -> Repayment Schedule -> Payment -> Overdue -> Dashboard / Report

### MVP objective
Build an end-to-end system that:
- allows users to create customers and loan products
- captures loan applications
- supports approval / rejection
- converts approved applications into active loans
- generates repayment schedules
- records payments
- tracks overdue accounts
- shows operational dashboard data

### Out of scope for MVP
- multi-currency
- multiple disbursements
- payment reversal
- penalty engine
- restructuring / refinancing
- accounting integration
- customer portal
- mobile app
- credit bureau integration
- OCR / eKYC integration
- automated SMS/email sending

---

## 3. Business Context

### Problem being solved
Target customers often manage loans using spreadsheets, messaging apps, and manual follow-up. This causes:
- inconsistent customer records
- no clear approval workflow
- difficulty tracking due and overdue accounts
- manual calculation errors
- poor auditability

### Business value
- centralize loan operations
- improve approval control
- reduce payment tracking errors
- provide visibility into due and overdue loans
- create a base for future automation

---

## 4. Scope

## 4.1 In scope
- user login and role-based access
- customer management
- loan product management
- loan application lifecycle
- approval workflow
- loan account creation
- single disbursement
- repayment schedule generation
- payment posting
- overdue tracking
- collection notes
- dashboard and basic reports
- audit logging for critical actions

## 4.2 Out of scope
- multi-branch fund accounting
- general ledger posting
- multiple disbursement tranches
- waiver, penalty, fee engine beyond fixed processing fee
- payment reversal
- write-off
- restructuring
- NPL classification beyond simple overdue
- workflow engine
- document OCR
- external integrations

---

## 5. Actors

| Actor | Description |
|---|---|
| Admin | Full access to configuration and operations |
| Loan Officer | Creates customers, products, applications, and can submit applications |
| Approver | Reviews submitted applications and approves or rejects |
| Collector | Views overdue loans and adds collection notes |
| Manager | Read-heavy operational oversight role (optional in MVP) |

---

## 6. Glossary

| Term | Meaning |
|---|---|
| Customer | Borrower record in the system |
| Loan Product | Configured lending template with min/max amount, tenor, rate, fee |
| Loan Application | Borrower request for a loan before approval |
| Approval | Decision step to approve or reject a submitted application |
| Loan Account | Actual booked loan created from an approved application |
| Disbursement | Release of approved funds to the borrower |
| Repayment Schedule | Planned list of installments with due dates and amounts |
| Installment | One repayment line in the schedule |
| Outstanding | Remaining unpaid balance |
| DPD | Days Past Due |
| Overdue | Loan with at least one unpaid installment past due date |
| Collection Note | Operational note added by collector or staff for overdue follow-up |
| Audit Log | System record of critical user actions |

---

## 7. End-to-End Business Flow

1. User creates customer
2. User creates or selects active loan product
3. User creates loan application for a customer
4. System validates amount and tenor against product rules
5. Loan Officer submits application
6. Approver approves or rejects application
7. If approved, system creates loan account
8. User disburses approved loan amount
9. System generates repayment schedule immediately after successful disbursement
10. User records payments against active loan
11. System updates installment status and outstanding balance
12. If installment remains unpaid after due date, loan becomes overdue
13. Collector views overdue list and adds collection notes
14. Dashboard aggregates active, due, and overdue data

---

## 8. System Assumptions

- One customer can have multiple applications and multiple loans
- One application belongs to exactly one customer and one product
- One approved application creates exactly one loan
- One loan has exactly one disbursement in MVP
- Repayment frequency is monthly only in MVP
- Interest type for MVP is **flat monthly interest**
- Due date is generated monthly from disbursement date
- Overdue is evaluated using local business date
- Currency is fixed to VND in MVP
- Money values are stored with decimal precision 2
- Product must be active to be used in new applications
- Customer must exist before application creation
- Payment reversal is not supported in MVP

---

## 9. Functional Modules

1. Authentication and RBAC
2. Customer Management
3. Loan Product Management
4. Loan Application Management
5. Approval Workflow
6. Loan Account and Disbursement
7. Repayment Schedule
8. Payment Posting
9. Overdue and Collections Lite
10. Dashboard and Basic Reports
11. Audit Logging

---

## 10. Requirement Catalog

Each requirement has:
- Requirement ID
- Module
- Actor
- Preconditions
- Trigger
- Input
- Validation
- Output
- Success result
- Error result
- Related APIs
- Related screens
- Acceptance criteria

### 10.1 Authentication and RBAC

#### AUTH-001 Login
- **Module:** Authentication
- **Actor:** All users
- **Preconditions:** User account exists and is active
- **Trigger:** User submits login form
- **Input:** username, password
- **Validation:** credentials must match active user
- **Output:** authenticated session / access token
- **Success result:** user enters system with role-based access
- **Error result:** invalid credential error shown
- **Related APIs:** `POST /auth/login`
- **Related Screens:** Login
- **Acceptance Criteria:**
  - valid credentials log user in
  - invalid credentials return error
  - inactive user cannot log in

#### AUTH-002 Role-based access control
- **Module:** Authorization
- **Actor:** System
- **Preconditions:** User is authenticated
- **Trigger:** User accesses screen or API
- **Validation:** role must have permission
- **Output:** allow or deny action
- **Related APIs:** all protected endpoints
- **Acceptance Criteria:**
  - unauthorized role cannot access restricted endpoint
  - UI hides restricted actions when role lacks permission

### 10.2 Customer Management

#### CUS-001 Create customer
- **Module:** Customer
- **Actor:** Admin, Loan Officer
- **Preconditions:** User has create permission
- **Trigger:** User submits create customer form
- **Input:** full name, phone, id number, address, date of birth
- **Validation:** phone unique, id number unique, required fields present
- **Output:** customer record
- **Success result:** customer created with ACTIVE status
- **Error result:** duplicate or validation error shown
- **Related APIs:** `POST /customers`
- **Related Screens:** Customer Create
- **Acceptance Criteria:**
  - required fields must be present
  - duplicate phone rejected
  - duplicate id number rejected
  - new customer appears in customer list

#### CUS-002 Update customer
- **Module:** Customer
- **Actor:** Admin, Loan Officer
- **Preconditions:** Customer exists
- **Trigger:** User saves customer edit form
- **Input:** editable customer fields
- **Validation:** uniqueness still enforced
- **Output:** updated customer record
- **Related APIs:** `PATCH /customers/{id}`
- **Acceptance Criteria:**
  - existing customer can be updated
  - duplicate phone/id blocked
  - audit log created for update

#### CUS-003 View customer list
- **Module:** Customer
- **Actor:** Admin, Loan Officer, Approver
- **Trigger:** User opens customer list
- **Output:** paginated list of customers
- **Related APIs:** `GET /customers`
- **Acceptance Criteria:**
  - customer list displays core fields
  - search by name, phone, id number works

#### CUS-004 View customer detail
- **Module:** Customer
- **Actor:** Admin, Loan Officer, Approver
- **Preconditions:** Customer exists
- **Trigger:** User opens customer detail
- **Output:** customer profile and related applications / loans summary
- **Related APIs:** `GET /customers/{id}`
- **Acceptance Criteria:**
  - detail page shows customer data
  - related applications and loans are visible

### 10.3 Loan Product Management

#### PRD-001 Create loan product
- **Module:** Loan Product
- **Actor:** Admin, Loan Officer
- **Input:** product name, min amount, max amount, allowed tenors, monthly flat interest rate, processing fee, repayment frequency
- **Validation:** max > min, interest >= 0, tenor list not empty, frequency = MONTHLY in MVP
- **Output:** active or draft product record
- **Related APIs:** `POST /loan-products`
- **Acceptance Criteria:**
  - product created with valid data
  - invalid amount range rejected

#### PRD-002 Update loan product
- **Module:** Loan Product
- **Actor:** Admin
- **Preconditions:** Product exists
- **Validation:** cannot invalidate data structure
- **Related APIs:** `PATCH /loan-products/{id}`
- **Acceptance Criteria:**
  - product can be updated
  - product currently used by applications is not deleted in MVP

#### PRD-003 View loan products
- **Module:** Loan Product
- **Actor:** Admin, Loan Officer, Approver
- **Related APIs:** `GET /loan-products`
- **Acceptance Criteria:**
  - active products can be listed and selected

### 10.4 Loan Application Management

#### APP-001 Create application draft
- **Module:** Loan Application
- **Actor:** Admin, Loan Officer
- **Preconditions:** Customer exists, product active
- **Input:** customer_id, product_id, requested_amount, requested_tenor_months, purpose, remarks
- **Validation:** amount within product range, tenor in allowed tenor list
- **Output:** application with DRAFT status
- **Related APIs:** `POST /applications`
- **Acceptance Criteria:**
  - valid application saved as DRAFT
  - invalid amount or tenor rejected

#### APP-002 Update application draft
- **Module:** Loan Application
- **Actor:** Admin, Loan Officer
- **Preconditions:** Application status = DRAFT
- **Related APIs:** `PATCH /applications/{id}`
- **Acceptance Criteria:**
  - draft application can be edited
  - submitted or approved application cannot be edited by Loan Officer

#### APP-003 Submit application
- **Module:** Loan Application
- **Actor:** Admin, Loan Officer
- **Preconditions:** Application exists in DRAFT
- **Validation:** required fields complete, product active, customer active
- **Output:** application status becomes SUBMITTED
- **Related APIs:** `POST /applications/{id}/submit`
- **Acceptance Criteria:**
  - draft changes to SUBMITTED
  - audit log created
  - invalid draft cannot be submitted

#### APP-004 View application list and detail
- **Module:** Loan Application
- **Actor:** Admin, Loan Officer, Approver
- **Related APIs:** `GET /applications`, `GET /applications/{id}`
- **Acceptance Criteria:**
  - user can filter by status
  - application detail shows product, customer, requested terms, decision history

#### APP-005 Installment preview
- **Module:** Loan Application
- **Actor:** Admin, Loan Officer
- **Preconditions:** Valid product, amount, tenor
- **Output:** estimated monthly installment and totals
- **Related APIs:** `POST /applications/preview`
- **Acceptance Criteria:**
  - preview returns estimated installment
  - preview matches formula section

### 10.5 Approval Workflow

#### APR-001 View approval queue
- **Module:** Approval
- **Actor:** Approver, Admin
- **Preconditions:** Submitted applications exist
- **Related APIs:** `GET /approvals/queue`
- **Acceptance Criteria:**
  - only SUBMITTED applications appear in queue

#### APR-002 Approve application
- **Module:** Approval
- **Actor:** Approver, Admin
- **Preconditions:** Application status = SUBMITTED
- **Validation:** actor has permission
- **Output:** application status becomes APPROVED
- **Related APIs:** `POST /applications/{id}/approve`
- **Acceptance Criteria:**
  - submitted application can be approved
  - decision history stored
  - audit log created

#### APR-003 Reject application
- **Module:** Approval
- **Actor:** Approver, Admin
- **Preconditions:** Application status = SUBMITTED
- **Validation:** rejection reason required
- **Output:** application status becomes REJECTED
- **Related APIs:** `POST /applications/{id}/reject`
- **Acceptance Criteria:**
  - rejection reason is mandatory
  - rejected application recorded in decision history

### 10.6 Loan Account and Disbursement

#### LOAN-001 Create loan from approved application
- **Module:** Loan Account
- **Actor:** Admin, Loan Officer
- **Preconditions:** Application status = APPROVED
- **Output:** loan account with PENDING_DISBURSEMENT status
- **Related APIs:** `POST /applications/{id}/create-loan`
- **Acceptance Criteria:**
  - only approved application can create loan
  - loan_account_no is unique

#### LOAN-002 Disburse loan
- **Module:** Disbursement
- **Actor:** Admin, Loan Officer
- **Preconditions:** Loan status = PENDING_DISBURSEMENT
- **Input:** disbursement date, disbursed amount
- **Validation:** disbursed amount must equal approved amount in MVP
- **Output:** loan becomes ACTIVE, disbursement record created, repayment schedule generated
- **Related APIs:** `POST /loans/{id}/disburse`
- **Acceptance Criteria:**
  - disbursement only happens once
  - schedule generated after success
  - loan becomes ACTIVE

#### LOAN-003 View loan detail
- **Module:** Loan Account
- **Actor:** Admin, Loan Officer, Approver, Collector
- **Related APIs:** `GET /loans/{id}`
- **Acceptance Criteria:**
  - loan detail shows customer, product, status, amount, outstanding, schedule summary

### 10.7 Repayment Schedule

#### SCH-001 Generate repayment schedule
- **Module:** Schedule
- **Actor:** System
- **Preconditions:** Loan successfully disbursed
- **Output:** monthly schedule lines with principal_due, interest_due, due_date
- **Related APIs:** generated as side effect of disbursement
- **Acceptance Criteria:**
  - number of installments equals tenor
  - total principal equals disbursed amount
  - each installment has due date and status UNPAID

#### SCH-002 View repayment schedule
- **Module:** Schedule
- **Actor:** Admin, Loan Officer, Collector, Approver
- **Related APIs:** `GET /loans/{id}/schedule`
- **Acceptance Criteria:**
  - schedule visible in loan detail
  - installment status shown

### 10.8 Payment Posting

#### PAY-001 Record payment
- **Module:** Payment
- **Actor:** Admin, Loan Officer
- **Preconditions:** Loan status = ACTIVE or OVERDUE
- **Input:** payment date, amount, payment method, remarks
- **Validation:** amount > 0 and amount <= current outstanding due in MVP
- **Output:** payment record, updated installment balances, updated loan outstanding
- **Related APIs:** `POST /loans/{id}/payments`
- **Acceptance Criteria:**
  - payment creates transaction history
  - allocation follows formula section
  - installment status updates correctly

#### PAY-002 View payment history
- **Module:** Payment
- **Actor:** Admin, Loan Officer, Collector
- **Related APIs:** `GET /loans/{id}/payments`
- **Acceptance Criteria:**
  - payment history ordered by payment date descending

### 10.9 Overdue and Collections Lite

#### OD-001 Calculate overdue status
- **Module:** Overdue
- **Actor:** System
- **Preconditions:** Loan has schedule
- **Trigger:** Query-time calculation in MVP
- **Rule:** Loan becomes OVERDUE if any installment due_date < business_date and remaining_due_amount > 0
- **Acceptance Criteria:**
  - overdue loan flagged correctly
  - DPD computed correctly
  - bucket assigned correctly

#### OD-002 View overdue list
- **Module:** Overdue
- **Actor:** Admin, Loan Officer, Collector
- **Related APIs:** `GET /overdue`
- **Acceptance Criteria:**
  - overdue list shows customer, loan number, due amount, DPD, bucket

#### OD-003 Add collection note
- **Module:** Collections Lite
- **Actor:** Collector, Admin
- **Preconditions:** Loan is overdue
- **Input:** note text, note date
- **Related APIs:** `POST /loans/{id}/collection-notes`
- **Acceptance Criteria:**
  - note saved and visible in loan detail

### 10.10 Dashboard and Reports

#### RPT-001 Dashboard summary
- **Module:** Dashboard
- **Actor:** Admin, Loan Officer, Approver, Collector, Manager
- **Output:** active loans count, overdue loans count, due today amount, outstanding amount
- **Related APIs:** `GET /dashboard/summary`
- **Acceptance Criteria:**
  - values reflect current data set

#### RPT-002 Overdue bucket report
- **Module:** Report
- **Actor:** Admin, Collector, Manager
- **Related APIs:** `GET /reports/overdue-buckets`
- **Acceptance Criteria:**
  - counts and amounts grouped by bucket are correct

### 10.11 Audit Logging

#### AUD-001 Audit critical actions
- **Module:** Audit
- **Actor:** System
- **Trigger:** on critical business action
- **Logged actions:** create/update customer, create/update product, create/submit application, approve/reject, create loan, disburse, post payment, add collection note
- **Acceptance Criteria:**
  - all critical actions produce audit record
  - actor, timestamp, action, target entity stored

---

## 11. State Machine

## 11.1 Application Status
- DRAFT
- SUBMITTED
- APPROVED
- REJECTED

### Application transitions
- DRAFT -> SUBMITTED
- SUBMITTED -> APPROVED
- SUBMITTED -> REJECTED

### Application transition guards
| From | To | Guard |
|---|---|---|
| DRAFT | SUBMITTED | required fields complete; customer active; product active |
| SUBMITTED | APPROVED | actor role = APPROVER or ADMIN |
| SUBMITTED | REJECTED | actor role = APPROVER or ADMIN; rejection reason required |

## 11.2 Loan Status
- PENDING_DISBURSEMENT
- ACTIVE
- OVERDUE
- CLOSED

### Loan transitions
- PENDING_DISBURSEMENT -> ACTIVE
- ACTIVE -> OVERDUE
- OVERDUE -> ACTIVE
- ACTIVE -> CLOSED
- OVERDUE -> CLOSED

### Loan transition guards
| From | To | Guard |
|---|---|---|
| PENDING_DISBURSEMENT | ACTIVE | successful disbursement recorded |
| ACTIVE | OVERDUE | at least one installment overdue |
| OVERDUE | ACTIVE | all overdue installments fully paid |
| ACTIVE | CLOSED | all installments fully paid |
| OVERDUE | CLOSED | all installments fully paid |

## 11.3 Installment Status
- UNPAID
- PARTIAL
- PAID
- OVERDUE

### Installment transition guards
| From | To | Guard |
|---|---|---|
| UNPAID | PARTIAL | payment applied but remaining_due_amount > 0 |
| UNPAID | PAID | payment fully covers installment |
| UNPAID | OVERDUE | due_date < business_date and remaining_due_amount > 0 |
| PARTIAL | PAID | remaining_due_amount becomes 0 |
| PARTIAL | OVERDUE | due_date < business_date and remaining_due_amount > 0 |
| OVERDUE | PAID | remaining_due_amount becomes 0 |

---

## 12. Deterministic Business Rules

## 12.1 Product rules
1. Product name must be unique
2. Product min_amount must be > 0
3. Product max_amount must be >= min_amount
4. Allowed tenor list must contain one or more positive integers
5. Repayment frequency = MONTHLY only in MVP
6. Product must be ACTIVE to be selectable in application creation

## 12.2 Customer rules
1. Phone must be unique
2. ID number must be unique
3. Customer status defaults to ACTIVE
4. Inactive customer cannot be used for new applications

## 12.3 Application rules
1. Requested amount must be between product min and max
2. Requested tenor must exist in product allowed tenor list
3. Draft applications can be edited
4. Submitted, approved, or rejected applications cannot be edited by Loan Officer
5. Rejected applications cannot be re-submitted in MVP; create new application instead

## 12.4 Approval rules
1. Only APPROVER or ADMIN can approve or reject
2. Reject action requires reason text
3. Approved application cannot be rejected afterward in MVP
4. Rejected application cannot be approved afterward in MVP

## 12.5 Loan and disbursement rules
1. Approved application creates one loan only
2. One loan has one disbursement only in MVP
3. Disbursed amount must equal approved amount
4. Loan status becomes ACTIVE immediately after successful disbursement
5. Schedule generation happens immediately after successful disbursement

## 12.6 Schedule generation rules
1. Interest model = flat monthly interest
2. Repayment frequency = monthly
3. Number of installments = tenor in months
4. Principal due per installment = disbursed_amount / tenor
5. Monthly interest due per installment = disbursed_amount * monthly_interest_rate
6. Installment total due = principal_due + interest_due
7. First due date = same calendar day next month from disbursement date
8. Each next due date = previous due date + 1 calendar month
9. Final principal total must equal disbursed amount after rounding adjustment on last installment if needed

## 12.7 Payment rules
1. Payment allowed only for ACTIVE or OVERDUE loan
2. Payment amount must be > 0
3. Payment amount cannot exceed total currently due and outstanding in MVP
4. Allocation priority = oldest unpaid installment first, within installment interest first then principal
5. Payment can make installment PARTIAL or PAID
6. Payment updates loan outstanding immediately
7. Payment reversal is not supported in MVP

## 12.8 Overdue rules
1. Overdue is calculated query-time in MVP
2. Business date = current local date in Asia/Ho_Chi_Minh
3. An installment is overdue if due_date < business_date and remaining_due_amount > 0
4. A loan is overdue if at least one installment is overdue
5. Loan returns to ACTIVE only when all overdue installments are fully paid

## 12.9 Overdue bucket rules
- Bucket CURRENT: DPD <= 0
- Bucket 1-7: 1 <= DPD <= 7
- Bucket 8-30: 8 <= DPD <= 30
- Bucket 31+: DPD >= 31

---

## 13. Formula Section

## 13.1 Installment preview formula
Given:
- principal = requested_amount
- monthly_rate = product.monthly_interest_rate
- tenor = requested_tenor_months

For MVP flat model:
- monthly_principal = principal / tenor
- monthly_interest = principal * monthly_rate
- estimated_monthly_installment = monthly_principal + monthly_interest

## 13.2 Schedule formula
For each installment i from 1 to tenor:
- principal_due_i = rounded principal / tenor, except final adjustment in last installment
- interest_due_i = principal * monthly_rate
- total_due_i = principal_due_i + interest_due_i

## 13.3 Outstanding calculation
- loan_outstanding = sum(remaining_due_amount across all installments)

## 13.4 Remaining due calculation per installment
- remaining_due_amount = remaining_interest_due + remaining_principal_due

## 13.5 DPD formula
- DPD = business_date - due_date in calendar days
- if DPD < 0 then treat bucket as CURRENT

## 13.6 Payment allocation formula
1. sort unpaid/partial installments by oldest due_date ascending
2. within each installment allocate:
   - interest first
   - principal second
3. stop when payment amount reaches 0

## 13.7 Rounding
- all money values rounded to 2 decimal places using round half up
- final installment principal adjusted so total principal equals original disbursed amount exactly

---

## 14. Permission Matrix

| Action | Admin | Loan Officer | Approver | Collector | Manager |
|---|---|---|---|---|---|
| Login | Y | Y | Y | Y | Y |
| View dashboard | Y | Y | Y | Y | Y |
| Create customer | Y | Y | N | N | N |
| Edit customer | Y | Y | N | N | N |
| View customer list/detail | Y | Y | Y | N | Y |
| Create loan product | Y | Y | N | N | N |
| Edit loan product | Y | N | N | N | N |
| View loan products | Y | Y | Y | N | Y |
| Create application | Y | Y | N | N | N |
| Edit draft application | Y | Y | N | N | N |
| Submit application | Y | Y | N | N | N |
| View applications | Y | Y | Y | N | Y |
| View approval queue | Y | N | Y | N | N |
| Approve application | Y | N | Y | N | N |
| Reject application | Y | N | Y | N | N |
| Create loan from approved application | Y | Y | N | N | N |
| Disburse loan | Y | Y | N | N | N |
| View loan detail | Y | Y | Y | Y | Y |
| View repayment schedule | Y | Y | Y | Y | Y |
| Record payment | Y | Y | N | N | N |
| View payment history | Y | Y | N | Y | Y |
| View overdue list | Y | Y | N | Y | Y |
| Add collection note | Y | N | N | Y | N |
| View overdue report | Y | N | N | Y | Y |
| View audit logs | Y | N | N | N | Y |

---

## 15. Data Dictionary

## 15.1 users
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| username | String(100) | Y | unique | admin01 |
| password_hash | String(255) | Y | stored securely | ... |
| full_name | String(150) | Y | not empty | Nguyen Van A |
| role | Enum | Y | ADMIN/LOAN_OFFICER/APPROVER/COLLECTOR/MANAGER | ADMIN |
| status | Enum | Y | ACTIVE/INACTIVE | ACTIVE |
| created_at | Timestamp | Y | system generated | ... |
| updated_at | Timestamp | Y | system generated | ... |

## 15.2 customers
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| full_name | String(150) | Y | not empty | Tran Thi B |
| phone | String(20) | Y | unique | 0901234567 |
| id_number | String(50) | Y | unique | 079123456789 |
| address | String(255) | Y | not empty | HCMC |
| date_of_birth | Date | Y | valid date | 1990-01-01 |
| status | Enum | Y | ACTIVE/INACTIVE | ACTIVE |
| created_by | UUID | Y | FK users.id | uuid |
| created_at | Timestamp | Y | system generated | ... |
| updated_at | Timestamp | Y | system generated | ... |

## 15.3 loan_products
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| product_name | String(150) | Y | unique | Installment 12M |
| min_amount | Decimal(18,2) | Y | > 0 | 5000000 |
| max_amount | Decimal(18,2) | Y | >= min_amount | 50000000 |
| allowed_tenors_json | JSON | Y | list of ints | [6,12,18] |
| monthly_interest_rate | Decimal(10,6) | Y | >= 0 | 0.015 |
| processing_fee | Decimal(18,2) | Y | >= 0 | 300000 |
| repayment_frequency | Enum | Y | MONTHLY | MONTHLY |
| status | Enum | Y | ACTIVE/INACTIVE | ACTIVE |
| created_at | Timestamp | Y | system generated | ... |
| updated_at | Timestamp | Y | system generated | ... |

## 15.4 loan_applications
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| customer_id | UUID | Y | FK customers.id | uuid |
| product_id | UUID | Y | FK loan_products.id | uuid |
| requested_amount | Decimal(18,2) | Y | within product range | 20000000 |
| requested_tenor_months | Int | Y | allowed tenor | 12 |
| purpose | String(255) | N | nullable | Laptop installment |
| remarks | Text | N | nullable | ... |
| status | Enum | Y | DRAFT/SUBMITTED/APPROVED/REJECTED | DRAFT |
| created_by | UUID | Y | FK users.id | uuid |
| submitted_at | Timestamp | N | nullable | ... |
| created_at | Timestamp | Y | system generated | ... |
| updated_at | Timestamp | Y | system generated | ... |

## 15.5 application_decisions
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| application_id | UUID | Y | FK loan_applications.id | uuid |
| decision | Enum | Y | APPROVED/REJECTED | APPROVED |
| decision_reason | Text | N | required if REJECTED | income mismatch |
| decided_by | UUID | Y | FK users.id | uuid |
| decided_at | Timestamp | Y | system generated | ... |

## 15.6 loans
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| loan_account_no | String(50) | Y | unique | LN-2026-0001 |
| application_id | UUID | Y | FK loan_applications.id | uuid |
| customer_id | UUID | Y | FK customers.id | uuid |
| product_id | UUID | Y | FK loan_products.id | uuid |
| approved_amount | Decimal(18,2) | Y | > 0 | 20000000 |
| tenor_months | Int | Y | > 0 | 12 |
| monthly_interest_rate | Decimal(10,6) | Y | snapshot from product | 0.015 |
| processing_fee | Decimal(18,2) | Y | snapshot from product | 300000 |
| disbursement_date | Date | N | nullable until disbursement | 2026-04-10 |
| maturity_date | Date | N | derived after disbursement | 2027-04-10 |
| status | Enum | Y | PENDING_DISBURSEMENT/ACTIVE/OVERDUE/CLOSED | ACTIVE |
| outstanding_amount | Decimal(18,2) | Y | derived and stored | 18000000 |
| created_at | Timestamp | Y | system generated | ... |
| updated_at | Timestamp | Y | system generated | ... |

## 15.7 loan_disbursements
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| loan_id | UUID | Y | FK loans.id | uuid |
| disbursed_amount | Decimal(18,2) | Y | = approved_amount in MVP | 20000000 |
| disbursement_date | Date | Y | valid date | 2026-04-10 |
| recorded_by | UUID | Y | FK users.id | uuid |
| created_at | Timestamp | Y | system generated | ... |

## 15.8 repayment_schedules
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| loan_id | UUID | Y | FK loans.id | uuid |
| installment_no | Int | Y | >= 1 | 1 |
| due_date | Date | Y | monthly sequence | 2026-05-10 |
| principal_due | Decimal(18,2) | Y | >= 0 | 1666666.67 |
| interest_due | Decimal(18,2) | Y | >= 0 | 300000 |
| principal_paid | Decimal(18,2) | Y | default 0 | 0 |
| interest_paid | Decimal(18,2) | Y | default 0 | 0 |
| remaining_due_amount | Decimal(18,2) | Y | derived | 1966666.67 |
| status | Enum | Y | UNPAID/PARTIAL/PAID/OVERDUE | UNPAID |
| created_at | Timestamp | Y | system generated | ... |

## 15.9 payments
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| loan_id | UUID | Y | FK loans.id | uuid |
| payment_date | Date | Y | valid date | 2026-06-10 |
| amount | Decimal(18,2) | Y | > 0 | 1966666.67 |
| payment_method | Enum | Y | CASH/BANK_TRANSFER | CASH |
| remarks | Text | N | nullable | paid at branch |
| created_by | UUID | Y | FK users.id | uuid |
| created_at | Timestamp | Y | system generated | ... |

## 15.10 loan_transactions
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| loan_id | UUID | Y | FK loans.id | uuid |
| payment_id | UUID | N | nullable for non-payment events | uuid |
| transaction_type | Enum | Y | DISBURSEMENT/PAYMENT | PAYMENT |
| amount | Decimal(18,2) | Y | > 0 | 1966666.67 |
| reference_no | String(50) | N | nullable | PMT-0001 |
| created_at | Timestamp | Y | system generated | ... |

## 15.11 collection_notes
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| loan_id | UUID | Y | FK loans.id | uuid |
| note_date | Date | Y | valid date | 2026-07-11 |
| note_text | Text | Y | not empty | customer promised to pay next week |
| created_by | UUID | Y | FK users.id | uuid |
| created_at | Timestamp | Y | system generated | ... |

## 15.12 audit_logs
| Field | Type | Required | Rule | Example |
|---|---|---|---|---|
| id | UUID | Y | PK | uuid |
| actor_user_id | UUID | Y | FK users.id | uuid |
| action_code | String(100) | Y | standardized action id | APPLICATION_SUBMITTED |
| entity_type | String(50) | Y | CUSTOMER/APPLICATION/LOAN/PAYMENT | APPLICATION |
| entity_id | UUID | Y | target entity id | uuid |
| old_values_json | JSON | N | nullable | {...} |
| new_values_json | JSON | N | nullable | {...} |
| created_at | Timestamp | Y | system generated | ... |

---

## 16. Enum Catalog

### user_role
- ADMIN
- LOAN_OFFICER
- APPROVER
- COLLECTOR
- MANAGER

### record_status
- ACTIVE
- INACTIVE

### application_status
- DRAFT
- SUBMITTED
- APPROVED
- REJECTED

### loan_status
- PENDING_DISBURSEMENT
- ACTIVE
- OVERDUE
- CLOSED

### installment_status
- UNPAID
- PARTIAL
- PAID
- OVERDUE

### payment_method
- CASH
- BANK_TRANSFER

### repayment_frequency
- MONTHLY

### decision_type
- APPROVED
- REJECTED

### transaction_type
- DISBURSEMENT
- PAYMENT

---

## 17. API Contract Summary

## 17.1 Standard response envelope
```json
{
  "success": true,
  "data": {},
  "error": null,
  "meta": {}
}
```

## 17.2 Error response envelope
```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "APP-VAL-001",
    "message": "Requested amount is out of product range"
  },
  "meta": {}
}
```

## 17.3 Authentication APIs

### POST /auth/login
**Role:** Public  
**Request**
```json
{
  "username": "admin01",
  "password": "secret"
}
```
**Success**
```json
{
  "success": true,
  "data": {
    "accessToken": "jwt",
    "user": {
      "id": "uuid",
      "role": "ADMIN",
      "fullName": "System Admin"
    }
  },
  "error": null,
  "meta": {}
}
```

## 17.4 Customer APIs

### POST /customers
**Role:** ADMIN, LOAN_OFFICER  
**Request**
```json
{
  "fullName": "Nguyen Van A",
  "phone": "0901234567",
  "idNumber": "079123456789",
  "address": "HCMC",
  "dateOfBirth": "1990-01-01"
}
```
**Business errors**
- `CUS-VAL-001` Duplicate phone
- `CUS-VAL-002` Duplicate id number
- `CUS-VAL-003` Missing required field

### GET /customers
**Role:** ADMIN, LOAN_OFFICER, APPROVER, MANAGER

### GET /customers/{id}
**Role:** ADMIN, LOAN_OFFICER, APPROVER, MANAGER

### PATCH /customers/{id}
**Role:** ADMIN, LOAN_OFFICER

## 17.5 Product APIs

### POST /loan-products
**Role:** ADMIN, LOAN_OFFICER  
**Request**
```json
{
  "productName": "Installment 12M",
  "minAmount": 5000000,
  "maxAmount": 50000000,
  "allowedTenors": [6, 12, 18],
  "monthlyInterestRate": 0.015,
  "processingFee": 300000,
  "repaymentFrequency": "MONTHLY",
  "status": "ACTIVE"
}
```
**Business errors**
- `PRD-VAL-001` Invalid amount range
- `PRD-VAL-002` Invalid tenor list
- `PRD-VAL-003` Duplicate product name

### GET /loan-products
**Role:** ADMIN, LOAN_OFFICER, APPROVER, MANAGER

### PATCH /loan-products/{id}
**Role:** ADMIN

## 17.6 Application APIs

### POST /applications
**Role:** ADMIN, LOAN_OFFICER  
**Request**
```json
{
  "customerId": "uuid",
  "productId": "uuid",
  "requestedAmount": 20000000,
  "requestedTenorMonths": 12,
  "purpose": "Laptop installment",
  "remarks": "Demo application"
}
```
**Business errors**
- `APP-VAL-001` Requested amount out of product range
- `APP-VAL-002` Requested tenor not allowed
- `APP-VAL-003` Customer not found
- `APP-VAL-004` Product not active

### PATCH /applications/{id}
**Role:** ADMIN, LOAN_OFFICER  
**Constraint:** only if status = DRAFT

### POST /applications/{id}/submit
**Role:** ADMIN, LOAN_OFFICER  
**Business errors**
- `APP-SUB-001` Application not in DRAFT
- `APP-SUB-002` Required fields incomplete

### GET /applications
**Role:** ADMIN, LOAN_OFFICER, APPROVER, MANAGER

### GET /applications/{id}
**Role:** ADMIN, LOAN_OFFICER, APPROVER, MANAGER

### POST /applications/preview
**Role:** ADMIN, LOAN_OFFICER

## 17.7 Approval APIs

### GET /approvals/queue
**Role:** ADMIN, APPROVER

### POST /applications/{id}/approve
**Role:** ADMIN, APPROVER  
**Business errors**
- `APR-VAL-001` Application not in SUBMITTED status
- `APR-VAL-002` Permission denied

### POST /applications/{id}/reject
**Role:** ADMIN, APPROVER  
**Request**
```json
{
  "reason": "Income verification failed"
}
```
**Business errors**
- `APR-VAL-003` Rejection reason required

## 17.8 Loan APIs

### POST /applications/{id}/create-loan
**Role:** ADMIN, LOAN_OFFICER  
**Business errors**
- `LOAN-VAL-001` Application not approved
- `LOAN-VAL-002` Loan already created for application

### POST /loans/{id}/disburse
**Role:** ADMIN, LOAN_OFFICER  
**Request**
```json
{
  "disbursementDate": "2026-04-10",
  "disbursedAmount": 20000000
}
```
**Business errors**
- `DISB-VAL-001` Loan not pending disbursement
- `DISB-VAL-002` Invalid disbursement amount
- `DISB-VAL-003` Loan already disbursed

### GET /loans/{id}
**Role:** ADMIN, LOAN_OFFICER, APPROVER, COLLECTOR, MANAGER

### GET /loans/{id}/schedule
**Role:** ADMIN, LOAN_OFFICER, APPROVER, COLLECTOR, MANAGER

## 17.9 Payment APIs

### POST /loans/{id}/payments
**Role:** ADMIN, LOAN_OFFICER  
**Request**
```json
{
  "paymentDate": "2026-06-10",
  "amount": 1966666.67,
  "paymentMethod": "CASH",
  "remarks": "Paid at branch"
}
```
**Business errors**
- `PAY-VAL-001` Loan not payable
- `PAY-VAL-002` Payment amount invalid
- `PAY-VAL-003` Payment amount exceeds outstanding due in MVP

### GET /loans/{id}/payments
**Role:** ADMIN, LOAN_OFFICER, COLLECTOR, MANAGER

## 17.10 Overdue and Dashboard APIs

### GET /overdue
**Role:** ADMIN, LOAN_OFFICER, COLLECTOR, MANAGER

### POST /loans/{id}/collection-notes
**Role:** ADMIN, COLLECTOR  
**Request**
```json
{
  "noteDate": "2026-07-11",
  "noteText": "Customer promised to pay next week"
}
```

### GET /dashboard/summary
**Role:** ADMIN, LOAN_OFFICER, APPROVER, COLLECTOR, MANAGER

### GET /reports/overdue-buckets
**Role:** ADMIN, COLLECTOR, MANAGER

---

## 18. Screen-to-API Mapping

| Screen | Action | API | Role |
|---|---|---|---|
| Login | login | POST /auth/login | Public |
| Customer List | list/search customers | GET /customers | Admin, Loan Officer, Approver, Manager |
| Customer Create | create customer | POST /customers | Admin, Loan Officer |
| Customer Detail | view customer | GET /customers/{id} | Admin, Loan Officer, Approver, Manager |
| Customer Edit | update customer | PATCH /customers/{id} | Admin, Loan Officer |
| Product List | list products | GET /loan-products | Admin, Loan Officer, Approver, Manager |
| Product Create | create product | POST /loan-products | Admin, Loan Officer |
| Product Edit | update product | PATCH /loan-products/{id} | Admin |
| Application List | list applications | GET /applications | Admin, Loan Officer, Approver, Manager |
| Application Form | create application | POST /applications | Admin, Loan Officer |
| Application Form | update draft | PATCH /applications/{id} | Admin, Loan Officer |
| Application Form | preview installment | POST /applications/preview | Admin, Loan Officer |
| Application Detail | submit application | POST /applications/{id}/submit | Admin, Loan Officer |
| Approval Queue | list queue | GET /approvals/queue | Admin, Approver |
| Approval Queue | approve | POST /applications/{id}/approve | Admin, Approver |
| Approval Queue | reject | POST /applications/{id}/reject | Admin, Approver |
| Loan Detail | create loan | POST /applications/{id}/create-loan | Admin, Loan Officer |
| Loan Detail | disburse | POST /loans/{id}/disburse | Admin, Loan Officer |
| Loan Detail | view schedule | GET /loans/{id}/schedule | All permitted viewers |
| Loan Detail | record payment | POST /loans/{id}/payments | Admin, Loan Officer |
| Loan Detail | view payments | GET /loans/{id}/payments | Admin, Loan Officer, Collector, Manager |
| Overdue Dashboard | list overdue loans | GET /overdue | Admin, Loan Officer, Collector, Manager |
| Overdue Detail | add collection note | POST /loans/{id}/collection-notes | Admin, Collector |
| Main Dashboard | get dashboard summary | GET /dashboard/summary | All logged-in roles |
| Overdue Report | bucket report | GET /reports/overdue-buckets | Admin, Collector, Manager |

---

## 19. Exception and Error Catalog

| Error Code | HTTP | Description | User Message |
|---|---|---|---|
| CUS-VAL-001 | 409 | Duplicate phone | Phone number already exists |
| CUS-VAL-002 | 409 | Duplicate id number | ID number already exists |
| CUS-VAL-003 | 400 | Missing required field | Please fill all required customer fields |
| PRD-VAL-001 | 400 | Invalid amount range | Max amount must be greater than or equal to min amount |
| PRD-VAL-002 | 400 | Invalid tenor list | Product must have at least one valid tenor |
| PRD-VAL-003 | 409 | Duplicate product name | Product name already exists |
| APP-VAL-001 | 400 | Requested amount out of range | Requested amount is outside product range |
| APP-VAL-002 | 400 | Requested tenor invalid | Requested tenor is not allowed for this product |
| APP-VAL-003 | 404 | Customer not found | Customer not found |
| APP-VAL-004 | 400 | Product inactive | Product is not active |
| APP-SUB-001 | 400 | Invalid application state for submit | Only draft application can be submitted |
| APP-SUB-002 | 400 | Required data incomplete | Complete all required fields before submit |
| APR-VAL-001 | 400 | Invalid application state for decision | Only submitted application can be decided |
| APR-VAL-002 | 403 | Decision permission denied | You do not have permission for this action |
| APR-VAL-003 | 400 | Missing rejection reason | Rejection reason is required |
| LOAN-VAL-001 | 400 | Application not approved | Only approved application can create loan |
| LOAN-VAL-002 | 409 | Loan already exists for application | Loan already created for this application |
| DISB-VAL-001 | 400 | Loan not pending disbursement | Loan is not ready for disbursement |
| DISB-VAL-002 | 400 | Invalid disbursement amount | Disbursed amount must equal approved amount |
| DISB-VAL-003 | 409 | Duplicate disbursement | Loan already disbursed |
| PAY-VAL-001 | 400 | Loan not payable | Loan is not in payable status |
| PAY-VAL-002 | 400 | Invalid payment amount | Payment amount must be greater than zero |
| PAY-VAL-003 | 400 | Payment exceeds outstanding | Payment amount exceeds current outstanding due |
| COL-VAL-001 | 400 | Note text missing | Collection note cannot be empty |

---

## 20. Acceptance Criteria by Module

## 20.1 Customer
- customer can be created with valid unique phone and id number
- duplicate phone or id number is blocked
- customer list and search work
- customer detail shows linked applications and loans summary

## 20.2 Loan Product
- product can be created with valid min/max amount and tenor list
- inactive product cannot be used in new application
- product list displays active products

## 20.3 Loan Application
- draft application can be created and edited
- invalid amount or tenor is rejected
- submit moves status from DRAFT to SUBMITTED
- submitted application becomes read-only to Loan Officer

## 20.4 Approval
- approval queue only shows submitted applications
- approve changes status to APPROVED
- reject changes status to REJECTED
- reject requires reason

## 20.5 Loan and Disbursement
- loan can only be created from approved application
- loan account number is unique
- disbursement can only happen once
- schedule is generated after disbursement
- loan becomes ACTIVE after disbursement

## 20.6 Schedule
- number of schedule rows equals tenor
- total principal equals disbursed amount
- installment due dates are monthly

## 20.7 Payment
- payment recorded only on ACTIVE or OVERDUE loan
- allocation uses oldest installment first, interest before principal
- outstanding amount decreases correctly
- installment status updates to PARTIAL or PAID

## 20.8 Overdue
- overdue loan appears in overdue list
- DPD bucket is correct
- collection note can be added

## 20.9 Dashboard
- dashboard shows total active loans, overdue loans, due today, outstanding amount
- overdue report groups loans correctly

---

## 21. Test Scenario Catalog

| Test ID | Module | Scenario |
|---|---|---|
| TC-AUTH-001 | Auth | Login success with valid credentials |
| TC-AUTH-002 | Auth | Login fails with invalid credentials |
| TC-CUS-001 | Customer | Create customer success |
| TC-CUS-002 | Customer | Duplicate phone rejected |
| TC-CUS-003 | Customer | Duplicate id rejected |
| TC-PRD-001 | Product | Create product success |
| TC-PRD-002 | Product | Invalid min/max amount rejected |
| TC-APP-001 | Application | Create draft application success |
| TC-APP-002 | Application | Requested amount out of range rejected |
| TC-APP-003 | Application | Requested tenor invalid rejected |
| TC-APP-004 | Application | Submit draft application success |
| TC-APR-001 | Approval | Approve submitted application |
| TC-APR-002 | Approval | Reject submitted application with reason |
| TC-APR-003 | Approval | Reject without reason fails |
| TC-LOAN-001 | Loan | Create loan from approved application |
| TC-DISB-001 | Disbursement | Disburse loan success |
| TC-DISB-002 | Disbursement | Second disbursement rejected |
| TC-SCH-001 | Schedule | Generated installment count matches tenor |
| TC-PAY-001 | Payment | Full installment payment success |
| TC-PAY-002 | Payment | Partial payment success |
| TC-PAY-003 | Payment | Payment exceeds outstanding rejected |
| TC-OD-001 | Overdue | Loan becomes overdue when installment past due |
| TC-OD-002 | Overdue | DPD bucket 1-7 correct |
| TC-OD-003 | Overdue | Add collection note success |
| TC-RPT-001 | Dashboard | Dashboard totals render correctly |

---

## 22. Seed Data Specification

### Products
1. Installment 6M
   - min 5,000,000
   - max 20,000,000
   - tenor [6]
   - rate 1.5% monthly
   - fee 200,000

2. Installment 12M
   - min 5,000,000
   - max 50,000,000
   - tenor [12]
   - rate 1.5% monthly
   - fee 300,000

3. Installment 18M
   - min 10,000,000
   - max 100,000,000
   - tenor [18]
   - rate 1.7% monthly
   - fee 500,000

### Customers
- 10 valid customers with unique phone and id numbers

### Applications
- 5 DRAFT
- 4 SUBMITTED
- 3 APPROVED
- 3 REJECTED

### Loans
- 3 PENDING_DISBURSEMENT
- 5 ACTIVE
- 3 OVERDUE
- 1 CLOSED

### Payments
- at least 1 full payment case
- at least 1 partial payment case
- at least 1 overdue case with no payment

### Collection notes
- at least 3 overdue loans with collection notes

### Demo scenario data
- Customer: Nguyen Van A
- Product: Installment 12M
- Application: 20,000,000 VND, tenor 12, purpose laptop installment
- Approved -> disbursed -> payment posted -> one overdue example

---

## 23. Dependency Matrix

| Module / Item | Depends On |
|---|---|
| Authentication | none |
| Customer | Authentication |
| Loan Product | Authentication |
| Loan Application | Customer, Loan Product, Authentication |
| Approval | Loan Application, Authentication |
| Loan Account | Approved Loan Application, Authentication |
| Disbursement | Loan Account, Authentication |
| Repayment Schedule | Disbursement |
| Payment Posting | Active Loan, Repayment Schedule, Authentication |
| Overdue | Repayment Schedule, Payment Posting |
| Collection Notes | Overdue |
| Dashboard / Report | Loans, Schedules, Payments, Overdue |

---

## 24. Non-Functional Requirements (MVP)

1. All protected APIs require authentication
2. Role-based authorization enforced at backend
3. Critical actions must create audit logs
4. Money fields must use decimal, not float
5. Input validation enforced at backend even if frontend validates
6. System should support at least 20 concurrent internal users in MVP
7. Standard list endpoints should respond within 2 seconds on demo-sized data
8. Errors must be logged server-side
9. Sensitive fields such as password must never be returned by APIs
10. Timezone and business date logic must use Asia/Ho_Chi_Minh
11. Staging environment should have seed data and demo accounts
12. Backups of demo database should be possible via dump/export

---

## 25. Audit Logging Policy

### Actions that must be logged
- CUSTOMER_CREATED
- CUSTOMER_UPDATED
- PRODUCT_CREATED
- PRODUCT_UPDATED
- APPLICATION_CREATED
- APPLICATION_UPDATED
- APPLICATION_SUBMITTED
- APPLICATION_APPROVED
- APPLICATION_REJECTED
- LOAN_CREATED
- LOAN_DISBURSED
- PAYMENT_RECORDED
- COLLECTION_NOTE_ADDED

### Required audit fields
- actor_user_id
- action_code
- entity_type
- entity_id
- old_values_json if applicable
- new_values_json if applicable
- created_at

### Minimum policy
- audit logs are append-only
- audit logs cannot be edited from UI
- audit logs should be queryable by entity type and entity id

---

## 26. Architecture Constraints for AI and Dev

1. Use modular monolith structure, not microservices
2. Use TypeScript strict mode
3. Use PostgreSQL for relational data
4. Use decimal type for money
5. Do not hard-code business enum labels in multiple places
6. Enforce business rules in backend service layer
7. Keep controllers thin, business logic in services
8. All critical actions must call audit logging service
9. Do not add unsupported MVP features unless requirement is explicitly added
10. Prefer simple, deterministic implementation over premature abstraction

---

## 27. Open Questions and Assumptions to Confirm Later

1. Should Manager role be read-only, or also approve in future?
2. Should Collector be allowed to record payment in phase 2?
3. Should processing fee be collected upfront or shown only as product info in MVP?
4. Should due date shift when it falls on weekend or holiday in later phases?
5. Should late fee / penalty be introduced in phase 2?
6. Should payment reversal be introduced in phase 2?
7. Should product versioning be added when product rules change?
8. Should customer document upload be added in phase 2?
9. Should dashboard filters by date / officer be added later?

---

## 28. 8-Week Execution Plan

## Week 1 — Scope and Foundation
### BA
- finalize scope and out-of-scope
- finalize actors, glossary, statuses
- finalize business flow
- write initial business rules
### Dev
- setup repo, backend, frontend, database
- implement login skeleton and role model
- establish module structure
### Done
- scope approved
- login working
- base project created

## Week 2 — Customer and Loan Product
### BA
- finalize customer and product specs
- finalize field validation rules
- write acceptance criteria
### Dev
- build customer CRUD
- build loan product CRUD
- add search and validation
### Done
- customer and product modules usable

## Week 3 — Loan Application
### BA
- finalize application rules and preview logic
- write UAT scenarios for application
### Dev
- build application create/edit/list/detail
- build installment preview API
### Done
- draft and submit flow works

## Week 4 — Approval and Audit
### BA
- finalize approval rules and decision history requirements
### Dev
- build approval queue
- build approve/reject actions
- build audit logging service for critical flows
### Done
- submitted applications can be approved/rejected with logs

## Week 5 — Loan Account and Disbursement
### BA
- finalize create loan and disbursement rules
### Dev
- build loan creation from approved application
- build disbursement
- trigger schedule generation
### Done
- approved application becomes active loan

## Week 6 — Schedule and Payment
### BA
- finalize formulas and payment allocation examples
### Dev
- build schedule generation
- build payment posting and history
- update outstanding and installment status
### Done
- payment flow works end-to-end

## Week 7 — Overdue, Collections, Dashboard
### BA
- finalize overdue logic and dashboard KPIs
### Dev
- build overdue query logic
- build collection notes
- build dashboard and overdue report
### Done
- overdue flow visible and demo-ready

## Week 8 — UAT, Bug Fix, Demo Package
### BA
- execute UAT
- finalize demo script and demo dataset
- capture gap list
### Dev
- fix bugs
- polish UI
- prepare staging/demo environment
### Done
- end-to-end demo stable
- blocker bugs = 0

---

## 29. Backlog Structure for Jira / AI Tasking

| Task ID | Module | Description | Role | Priority | Dependency | Output |
|---|---|---|---|---|---|---|
| TASK-AUTH-001 | Auth | Build login API and auth guard | Dev | P0 | none | login working |
| TASK-CUS-001 | Customer | Define customer fields and validation | BA | P0 | none | customer spec |
| TASK-CUS-002 | Customer | Build create/list/detail/update customer | Dev | P0 | Auth | customer module |
| TASK-PRD-001 | Product | Define product parameter rules | BA | P0 | none | product spec |
| TASK-PRD-002 | Product | Build create/list/update product | Dev | P0 | Auth | product module |
| TASK-APP-001 | Application | Define application flow and rules | BA | P0 | Customer, Product | application spec |
| TASK-APP-002 | Application | Build application APIs and screens | Dev | P0 | Customer, Product | application module |
| TASK-APR-001 | Approval | Define approval and rejection rules | BA | P0 | Application | approval spec |
| TASK-APR-002 | Approval | Build approval queue and decision endpoints | Dev | P0 | Application | approval module |
| TASK-LOAN-001 | Loan | Define loan creation and disbursement rules | BA | P0 | Approval | loan spec |
| TASK-LOAN-002 | Loan | Build create loan and disbursement | Dev | P0 | Approval | loan module |
| TASK-SCH-001 | Schedule | Define formulas and due date rules | BA | P0 | Loan | schedule spec |
| TASK-SCH-002 | Schedule | Build schedule generation | Dev | P0 | Loan | schedule module |
| TASK-PAY-001 | Payment | Define payment allocation rules | BA | P0 | Schedule | payment spec |
| TASK-PAY-002 | Payment | Build payment posting and history | Dev | P0 | Schedule | payment module |
| TASK-OD-001 | Overdue | Define overdue and bucket rules | BA | P0 | Payment | overdue spec |
| TASK-OD-002 | Overdue | Build overdue query and collection notes | Dev | P0 | Payment | overdue module |
| TASK-RPT-001 | Report | Define dashboard KPIs | BA | P1 | Loans, Payments | report spec |
| TASK-RPT-002 | Report | Build dashboard and overdue report | Dev | P1 | Loans, Payments | report module |
| TASK-UAT-001 | UAT | Write and execute UAT scenarios | BA | P1 | all modules | UAT result |
| TASK-DEMO-001 | Demo | Seed demo data and deploy staging | Dev | P1 | all modules | demo environment |

---

## 30. Demo Scenario

### Scenario A — Happy path
1. Create customer Nguyen Van A
2. Create or select product Installment 12M
3. Create application for 20,000,000 VND / 12 months
4. Preview installment
5. Submit application
6. Approver approves application
7. Create loan
8. Disburse full amount
9. View generated schedule
10. Record first payment
11. View updated outstanding

### Scenario B — Overdue path
1. Use seeded overdue loan
2. Open overdue dashboard
3. Show DPD and bucket
4. Open loan detail
5. Add collection note

---

## 31. Definition of Done

A module is considered done when:
1. BA rules are finalized
2. API and UI implemented
3. Backend validation enforced
4. Acceptance criteria passed
5. Critical actions logged if required
6. Demo data available
7. No blocker defects remain for that module

---

## 32. Recommended File Split for Future Maintenance

If the document grows further, split into:
1. `01-business-context-and-scope.md`
2. `02-functional-spec.md`
3. `03-data-and-api-spec.md`
4. `04-execution-plan.md`
5. `05-test-and-demo-plan.md`

This current document is intentionally combined as a single master planning doc for AI-readable onboarding and initial build execution.
