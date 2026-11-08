# 10 — Source Evidence and Documentation Handover

Prepared September 30, 2026. This chapter records the evidence used and explains how to maintain the package without changing business data.

Inline source paths are relative to the project root; clickable links are relative to their document location. The original dictionary is one level above the project root.

## 10.1 Evidence register

| Evidence ID | Source | What it establishes | What it does not establish |
| --- | --- | --- | --- |
| E01 | `EZKLaundrySystem.csproj` | ASP.NET web project; net10.0 target; MySql.Data 9.7.0 reference. | Production server version, deployment status, successful package restore on another machine. |
| E02 | `Program.cs` | MVC/session/static-file/routing configuration. | Full security certification or reliable production session infrastructure. |
| E03 | `Data/DbConnection.cs` | Direct MySQL connection helper and locally embedded configuration. | That secrets are safely externalized; credentials are deliberately not reproduced here. |
| E04 | `Data/PasswordSecurity.cs` and account models | Password hashing/validation and registration field constraints. | Password-reset, MFA, lockout, or email-verification features. |
| E05 | `Controllers/AccountController.cs` | Registration, approval-dependent login, session role routing. | Successful tests for every invalid input/concurrency case. |
| E06 | `Controllers/CustomerController.cs` and Customer views | Draft/review/confirmation, receipt/history, tracking, notifications, account/reward-related behavior. | A working payment gateway or automatic push tracking. |
| E07 | `Controllers/StaffController.cs` and Staff view | Intake, processing, actual-weight, payment and notification handlers. | Correct behavior for every state transition or amount edge case. |
| E08 | `Controllers/OwnerController.cs` and Owner views | Owner setup, staff/pricing management, monthly income, transaction filters and printing. | Passed print QA or absence of date-control defects. |
| E09 | `laundry_final_3nf.sql` | Current schema specification: 15 tables, lookup seeds, constraints, three views. | That an existing local/live DB has been migrated to match every definition. |
| E10 | `Properties/launchSettings.json` | Local HTTP 5210 and HTTPS 7128 profiles. | That a server is currently running at those addresses. |
| E11 | `Views/Shared/_Layout.cshtml`, `wwwroot` | Layout and local/external frontend assets. | Guaranteed offline availability of external fonts/icons/scripts/images. |
| E12 | `../dictionary.docx` | Earlier five-table data dictionary. | A complete approved manuscript or current database specification. |
| E13 | Supplied user conversation/screenshots | Requested changes, adviser concerns reported by the user, visible historical UI. | Exact message dates for every task, code diffs, test results, or adviser sign-off. |
| E14 | Filesystem metadata inspected September 30 | File creation/modification anchors used in Chapter 06. | Exact actual development start/finish or responsible author. |

The original Word dictionary remains outside this folder and was not replaced. The numbered Markdown documents are new documentation, not edits to that original manuscript.

## 10.2 Representative timestamp observations

The following dates are file metadata, observed in the local environment. **Created** can indicate copying; **modified** indicates the retained file timestamp, not a line-by-line history.

| File | Created | Last modified |
| --- | --- | --- |
| `EZKLaundrySystem.csproj` | 2026-07-27 14:24:58 | 2026-06-17 17:51:04 |
| `Data/DbConnection.cs` | 2026-07-27 14:24:58 | 2026-06-17 17:52:56 |
| `Controllers/HomeController.cs` | 2026-07-27 14:24:58 | 2026-06-19 14:25:36 |
| `Views/Owner/Login.cshtml` | 2026-07-27 14:25:00 | 2026-06-20 07:09:58 |
| `../dictionary.docx` | 2026-08-06 16:07:44 | 2026-08-06 17:35:17 |
| `laundry_final_3nf.sql` | 2026-08-06 16:33:15 | 2026-09-25 11:54:06 |
| `Data/PasswordSecurity.cs` | 2026-08-06 17:22:31 | 2026-09-02 13:20:13 |
| `Models/CustomerRegistration.cs` | 2026-09-02 10:46:59 | 2026-09-02 13:20:21 |
| `Models/StrongPasswordAttribute.cs` | 2026-09-02 13:20:15 | 2026-09-02 13:20:15 |
| `Controllers/AccountController.cs` | 2026-07-27 14:24:58 | 2026-09-02 10:47:02 |
| `Program.cs` | 2026-07-27 14:24:58 | 2026-09-15 16:57:18 |
| `Views/Customer/ReviewBooking.cshtml` | 2026-09-25 11:06:30 | 2026-09-25 11:06:30 |
| `Views/Customer/PaymentQR.cshtml` | 2026-07-27 14:25:00 | 2026-09-25 11:06:51 |
| `Views/Customer/BookingReceipt.cshtml` | 2026-07-27 14:25:00 | 2026-09-25 11:07:25 |
| `Views/Account/Register.cshtml` | 2026-07-27 14:25:00 | 2026-09-25 12:14:44 |
| `Views/Staff/Index.cshtml` | 2026-07-27 14:25:00 | 2026-09-29 18:38:17 |
| `Controllers/CustomerController.cs` | 2026-07-27 14:24:58 | 2026-09-29 18:49:42 |
| `Controllers/StaffController.cs` | 2026-07-27 14:24:58 | 2026-09-29 18:52:22 |
| `Views/Customer/Index.cshtml` | 2026-07-27 14:25:00 | 2026-09-29 18:52:37 |
| `Controllers/OwnerController.cs` | 2026-07-27 14:24:58 | 2026-09-30 10:23:00 |
| `Views/Owner/Index.cshtml` | 2026-07-27 14:25:00 | 2026-09-30 10:26:22 |

Do not reinterpret this table as a recovered commit history. No `.git` directory was present in the inspected project root. Histories in other backups, repositories, or school submissions may still exist and can be added later.

## 10.3 What was checked for this documentation task

- Read current source/schema/view files and compared described behavior with implemented conditions.
- Read the earlier Word dictionary's text without modifying the file.
- Checked local project configuration and observed the installed `dotnet --version` response, 10.0.300.
- Read creation/modification metadata to support, not fabricate, historical dates.
- Cross-reviewed database/payment/report semantics across the authored chapters.
- Created editable chapters, a timeline/work breakdown, a test plan, a limitations register, and a combined printable HTML document under `docs/`.

Application builds, authenticated browser workflows, physical printing, real payments, and database migration were not executed as part of this documentation request. Existing generated binaries are not substituted for test execution evidence.

The combined documentation's HTML structure, chapter links, and referenced local files were checked separately from application testing. A browser preview was attempted, but its runtime connection failed before opening the document. Consequently, visual layout and print pagination are not certified; inspect the browser print preview before submitting. This does not change the NOT RUN status of application test cases.

## 10.4 Reading and printing the package

Open `EZK_System_Documentation.html` locally in a browser. Use the chapter contents links to navigate. The Print / Save as PDF action opens the browser print dialog; select a printer or Save as PDF, inspect preview, and choose the desired page range. Browser-added headers/footers can be disabled if they display local file paths.

The document uses a print stylesheet to hide reading controls and format tables for paper. Print pagination can vary by browser, paper size, margins, and installed fonts, so inspect preview before submission. The generated HTML does not execute the application's booking or payment logic and has no network dependencies.

If editing the manuscript in Word, copy the relevant content from the rendered document or individual Markdown previews into your school template, then check headings and table widths. This package does not claim to be a `.docx` export or a finished school-formatted thesis.

## 10.5 Maintaining the documentation

1. Make functional changes only under a separately authorized development task.
2. Update the corresponding chapter with the actual change and exact source reference.
3. Add the confirmed implementation/test date to Chapter 06 rather than replacing historical evidence with guesses.
4. Execute the relevant acceptance cases and record actual outcomes in Chapter 07 or a separate dated test-results document.
5. Change a Chapter 08 limitation to resolved only after verifying the fix. Preserve a dated resolution note.
6. Regenerate the combined HTML using `docs/tools/Build-Documentation.ps1`.
7. Verify all links, check print preview, and keep a dated backup before adviser submission.

For future dated logs, use a consistent record containing: date/time, task ID, requested change, files changed, business-rule impact, tests run, actual result, remaining issues, and reviewer. Do not put credentials or customer personal information into these logs.

## 10.6 Handover checklist

- [ ] Confirm the real project start date and consultation windows.
- [ ] Reconcile current database tables with the deployed database, without destructive re-import.
- [ ] Replace outdated dictionary terminology in the manuscript.
- [ ] Run acceptance tests in an isolated test database.
- [ ] Address known report-filter, processing-transition, and walk-in add-on defects before relying on them in a demonstration.
- [ ] Verify printable outputs with realistic multi-page data.
- [ ] Clearly label prototype GCash behavior and do not use it to solicit actual payment.
- [ ] Remove/anonymize personal data in school evidence.
- [ ] Confirm the adviser-approved scope before implementing inventory.
- [ ] Back up application source and the actual database separately; the schema file alone is not a data backup.
- [ ] Confirm that planned, implemented, tested, and accepted dates are not conflated in the Gantt chart.

These are follow-up activities, not claims of actions already performed.
