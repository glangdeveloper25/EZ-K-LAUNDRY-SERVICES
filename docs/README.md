# EZ-K Laundry System — Documentation Package

Prepared: **September 30, 2026**  
Main reporting period: **September 1–30, 2026**, with earlier project history  
Language: English for manuscript reuse, with selected Filipino explanations  
Basis: the current project source, SQL schema, earlier `dictionary.docx`, and the supplied development conversation/screenshots

## Start here

This folder documents the system as it exists at the review cutoff. It is not a claim that every screen has passed user acceptance testing. It separates implemented source behavior, prototype content, known defects, proposed enhancements, and dates requiring confirmation.

For a convenient reading/printing copy, open **[EZK_System_Documentation.html](EZK_System_Documentation.html)** in a browser. It combines the numbered chapters and has a Print / Save as PDF button. The editable originals are the Markdown (`.md`) files below. No running application or internet connection is required to read the compiled document.

For your Gantt chart, start with **Chapter 06**. For updating the database section of your manuscript, use **Chapter 03** and the comparison in **Chapter 09**. Do not copy a date marked “to confirm” into an actual-completion column without checking your own records.

## Document index

| File | Purpose |
| --- | --- |
| [01 — System Overview and Scope](01_System_Overview_and_Scope.md) | Background, objectives, roles, scope, business concepts, and end-to-end overview. |
| [02 — Architecture, Setup, and Security](02_Architecture_Setup_and_Security.md) | Technology stack, project structure, local setup, authentication, dependencies, and maintenance. |
| [03 — Database Design and Data Dictionary](03_Database_Design_and_Data_Dictionary.md) | Tables, columns, keys, relationships, views, seed values, normalization, and database caveats. |
| [04 — Functional Modules and Workflows](04_Functional_Modules_and_Workflows.md) | Customer, Staff, and Owner features; booking review; payments; weight; notifications; navigation. |
| [05 — Reporting and Printing](05_Reporting_and_Printing.md) | Monthly income, transaction filters, date rules, report totals, and printable outputs. |
| [06 — Development Timeline and Gantt Input](06_Development_Timeline_and_Gantt_Input.md) | Dated evidence, historical reconstruction, consultation windows, work breakdown, dependencies, and Gantt guidance. |
| [07 — Requirements Traceability and Test Plan](07_Requirements_Traceability_and_Test_Plan.md) | Requirement-to-code mapping, manual acceptance cases, expected results, and validation records. |
| [08 — Known Limitations and Future Work](08_Known_Limitations_and_Future_Work.md) | Known gaps, implementation risks, priorities, and deferred inventory discussion. |
| [09 — Manuscript Update and Defense Guide](09_Manuscript_Update_and_Defense_Guide.md) | Reusable explanation, vocabulary, original-document differences, and evidence to collect. |
| [10 — Source Evidence and Documentation Handover](10_Source_Evidence_and_Documentation_Handover.md) | Source register, chronology rules, handover checklist, and documentation update procedure. |

## Important interpretation rules

### Follow-up after the original snapshot

[October 1 - Basic rewards rules and verification](updates/2026-10-01-basic-rewards.md) records real points balances, earning on completed and fully paid orders, the 100-point/PHP 50 booking discount, cancellation restoration, and isolated regression tests. This supersedes the earlier rewards-prototype status; the original manuscript was not changed.

[September 30 - Explicit detergent/softener source choices](updates/2026-09-30-supply-source-choices.md) records the later customer-form change, saved instructions, and its actual verification results.

[September 30 - Per-piece supplies inventory](updates/2026-09-30-piece-inventory.md) records the subsequent Owner/Staff inventory implementation, walk-in integration, additive database tables, setup steps, and isolated test results. This supersedes the earlier paused-inventory status for the features specifically described there. The original numbered chapters and combined HTML remain a historical baseline; read the follow-up updates for later changes.

Inline source paths (for example, `Controllers/OwnerController.cs` or `../dictionary.docx`) are relative to the project root unless stated otherwise. Clickable links use paths relative to the document so they resolve from this folder.

- **Source-verified** means the implementation can be found in the inspected files. It does not mean an authenticated end-to-end test passed.
- **Filesystem date** means the operating system reports a creation/modification timestamp. A copied file can retain an older modification date; this is not a commit or an adviser-approved completion date.
- **Conversation evidence** records what was requested or shown. Many messages have no individual timestamps, so their exact dates remain unconfirmed.
- **Proposed / deferred** means discussion or recommended future work, not an implemented feature.
- **Not run** in a test table is intentional. This documentation pass did not create real bookings, collect payments, change prices, or alter business records.

## What this package does not change

The original documentation-only pass was confined to `docs/` and did not change application code, database records, credentials, or the original `dictionary.docx`. Inventory was paused at that cutoff. Later implementation changes are recorded separately in the follow-up updates above. No consultation dates, adviser signatures, approval statements, financial transactions, or successful test results have been invented.

## Quick use for school documentation

1. Read Chapters 01 and 04 to describe the system and actual process.
2. Replace the old five-table dictionary with the current schema description in Chapter 03 after comparing it with the actual database used for your defense.
3. Use Chapter 06 task IDs and predecessor IDs for your Gantt chart. Confirm missing actual start/end dates yourself.
4. Describe reports using Chapter 05: an order total, a verified payment, and monthly income are different things.
5. Run the tests in Chapter 07 in an isolated test database, recording actual results and dates.
6. Use Chapter 08 as an honest limitations/future-work section, not as a statement that those fixes are already done.
7. Anonymize names, phone numbers, email addresses, and payment references before submitting screenshots.

## Rebuilding the combined document

The numbered Markdown chapters are the source of truth. After editing them, run `docs/tools/Build-Documentation.ps1` with PowerShell 7 to regenerate the single HTML document. The script writes only the named generated document under `docs/`; it does not connect to the system database. Use the README links to read the chapters directly if HTML has not yet been generated.

The combined document's content and links were structurally checked. Browser visual/print-preview QA could not be completed because the preview connection was unavailable; inspect print preview before school submission.
