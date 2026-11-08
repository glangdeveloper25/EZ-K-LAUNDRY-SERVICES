# 06 — Development Timeline and Gantt Input

Prepared September 30, 2026. The user confirmed **September 2026 plus earlier project history** as the coverage. This is an evidence-based reconstruction, not a recovered Git commit log or an adviser-signed attendance sheet.

## 6.1 How to interpret the dates

The conversation records many requests but does not expose a separate timestamp for every message. The project also has copied files whose modification dates are earlier than their creation dates. Consequently, a single exact start/end date cannot honestly be supplied for every completed feature.

Use the following classifications:

| Code | Meaning | Appropriate Gantt use |
| --- | --- | --- |
| F | Filesystem timestamp read on September 30, 2026. | Supporting evidence/milestone note, not an automatic actual-completion date. |
| C | Concern, decision, or visible UI from the supplied conversation/screenshots. | Requirement/change description. Use the date only if the screenshot or record establishes it. |
| S | Implementation verified in the current source. | “Implemented in inspected source”; exact implementation date may still be unknown. |
| U | Date/window explicitly supplied by the user. | Consultation/planning window, subject to confirming the meeting and topics. |
| D | Documentation authored during this request. | Actual documentation preparation date: September 30, 2026. |
| TBC | To be confirmed using the user's notes, original files, or submission records. | Leave actual start/end blank rather than fabricate a duration. |

**A booking date is not a development date.** For example, a booking reference containing `20260925` demonstrates an order-date convention, not that a feature was coded on September 25. Likewise, a displayed “Tuesday, Sep 29” helps contextualize a screenshot but is not a version-control record.

Dates below are written in the local project/environment context (+08:00). Filesystem timestamps were observed as local times. This does not verify historical server time settings or database connection time zones.

## 6.2 Earlier project history: from the beginning available in evidence

### June 17–20, 2026 — older source timestamp anchors (F)

Several project files retain modification dates from June 2026. `Data/DbConnection.cs` and the project file have June 17 dates; `HomeController.cs` has a June 19 date; the owner login view and shared layout have June 20 dates. Most of these files were created in this local folder on July 27.

This supports the existence of older source material, but it does **not** establish the first day of project planning, original coding, or who authored those files. Do not write “the project officially started June 17” based solely on this metadata.

Relevant foundational activities, whose actual dates remain TBC, are requirements identification, role definition, the initial MVC project, database connection design, and the first role-based UI prototype.

### July 27, 2026 — local project-file creation cluster (F)

The main controllers, models, views, configuration, and project file share creation timestamps around 14:24–14:25 on July 27. This is consistent with a copy/extraction/setup event in the current folder. It is not evidence that all modules were implemented within those two minutes.

For a Gantt chart, use this as **“local project baseline present”** if you need an evidence anchor, while retaining TBC for the true project start.

### July 30, 2026 — older generated runtime metadata (F)

The `bin/Debug/net10.0` dependency/runtime configuration files have July 30 timestamps. They indicate generated build artifacts existed, not a verified successful test run. Current binaries also have later timestamps, so these files cannot reconstruct the complete build history.

### August 6, 2026 — database/documentation/security artifact cluster (F, S)

The earlier `dictionary.docx` was created at 16:07:44 and last modified at 17:35:17 on August 6. It describes five tables: Roles, Users, Services, Item_Type, and Bookings. The current schema is substantially more detailed and should replace this earlier draft when updating the manuscript.

`laundry_final_3nf.sql` has an August 6 creation timestamp, while `Data/PasswordSecurity.cs` has an August 6 creation timestamp and later September modifications. These are evidence anchors for database and password-handling work, not proof that the current 15-table schema or current hash implementation was complete on August 6.

Suggested historical task descriptions:

- Prepare the initial data dictionary and identify system entities.
- Develop/refine relational lookup tables, users, bookings, service lines, and payment relationships.
- Introduce a password-security helper and role-aware login structure.

Actual task start/end dates: TBC. Current implementation: inspect Chapters 02 and 03.

## 6.3 September 2026 development reconstruction

### September 2 — account validation and access-related artifact cluster (F, S)

Observed evidence includes the creation of `CustomerRegistration.cs` and `StrongPasswordAttribute.cs`, plus modifications to `AccountController.cs`, `PasswordSecurity.cs`, `User.cs`, customer registration validation, and the owner registration view.

Current source provides separate first/last-name customer registration, email/contact validation, password confirmation, password-strength checks, PBKDF2 hashing, Pending customer accounts, login checks, and role-specific redirection. The metadata suggests relevant account work around September 2, but it cannot tell which lines changed that day.

Gantt wording: **Account registration, validation, and authentication refinement — September 2 artifact anchor; actual work duration TBC.**

### September 7–11 — user-supplied consultation window (U)

The user previously supplied this window while requesting consultation records. No signed consultation sheet or timestamped assignment of specific features to this week was available. Record it as a consultation window to confirm, not as a completed implementation phase with invented adviser agreements.

### September 15 — application startup/session configuration artifact (F, S)

`Program.cs` has a September 15, 16:57:18 modification timestamp. Its current configuration includes MVC, a 30-minute session timeout, an HTTP-only session cookie, routing, static assets, and the session middleware. HTTPS redirection is commented out for the local setup.

The exact September 15 change cannot be reconstructed without a diff or prior version. Gantt wording: **Local application startup/session configuration — September 15 file-modification anchor.**

### September 24–25 — dashboard and booking-revision evidence cluster (C, F, S)

Screenshots display September 24/25 orders and show successive UI states: staff order cards, single versus multiple customer tracking cards, owner dashboard spacing, pricing screens, customer history, and operations tables. These show the development context, but most image capture/message times are not independently recorded.

The clearest file anchors are on September 25:

- `ReviewBooking.cshtml` was created and modified at 11:06:30.
- `PaymentQR.cshtml` was modified at 11:06:51.
- `BookingReceipt.cshtml` was modified at 11:07:25.
- `laundry_final_3nf.sql` was modified at 11:54:06.
- `Views/Account/Register.cshtml` was modified at 12:14:44.

The associated current-source changes and conversation concerns are:

1. **Booking review before submission.** The adviser objected to immediate booking creation. A session draft and review/edit flow are now present; persistence occurs at the relevant final branch, not when merely viewing the review.
2. **Cancel/back behavior.** Pre-submission cancellation should discard or leave the draft rather than create an active booking. Browser navigation and duplicate submission still require acceptance testing.
3. **Correct receipt content.** The receipt uses actual selected service lines and calculated values rather than the earlier sample service/amount text.
4. **Payment-method distinction.** Cash on Pickup and GCash take different paths. GCash remains a manual declaration/verification prototype, not automated payment confirmation.
5. **Catalog and add-ons.** Washing, Drying, Folding, and Self Service are retained; soap, softener, Rush Fee, and Delivery Fee are represented as selectable charge lines.
6. **Password visibility.** The registration UI includes the requested eye/eye-slash behavior for password fields.

Only the five file timestamps above are direct date observations. The exact days for pricing, print buttons, dashboard spacing, tracking, and navigation adjustments are TBC because their containing files were modified again later.

### September 29 — operations/payment/weight/notification artifact cluster (F, C, S)

`CustomerController.cs`, `StaffController.cs`, `Views/Staff/Index.cshtml`, and `Views/Customer/Index.cshtml` have September 29 evening modification times. The supplied discussion also shows September 29 staff screens and concerns about actual-weight entry and customer notifications.

Current-source deliverables associated with this cluster include:

- Flexible staff booking lookup for recording a payment using numeric ID, BK-style ID, or full booking reference.
- Staff record/verify-payment controls, balance handling, and recording actor information.
- Paid-payment-based cash collection instead of a fixed display amount.
- Staff actual-weight entry with recalculation of per-kilogram service quantities.
- Preservation of recorded unit prices during weight recalculation.
- A guard against reducing the order charge below the already Paid amount.
- Status-history notes describing weight/amount changes.
- Customer notifications created for actual-weight and payment events.
- Reading customer-specific notifications from storage and a Mark All as Read action.

Boundaries: actual weight overwrites the single structured item-weight field; notification cards are not order-detail links; tracking/notifications do not have automatic push refresh; generic processing-status changes do not consistently generate notifications. These are not complete production-ready modules merely because source code exists.

Gantt wording: **Staff payment/weight handling and customer-notification refinement — September 29 source-modification anchor; acceptance testing outstanding.**

### September 30 — transaction-report artifact cluster and documentation (F, C, S, D)

`OwnerController.cs` was modified at 10:23:00 and `Views/Owner/Index.cshtml` at 10:26:22. The application DLL/EXE/PDB have 10:26:27 timestamps. The latter is an artifact timestamp, not a captured zero-warning build log or end-to-end test result.

The owner-report implementation includes payment-level transaction rows; All, Today, Yesterday, Specific Date, Monthly, Yearly, and Custom periods; payment-method/status filters; paid-only summary totals; and printing of the current rendered transaction report. Existing monthly income reporting remains separate.

Source review also identified an important report UI defect: date-field visibility uses a CSS/JavaScript combination that keeps period-specific controls hidden. This is documented as a pending fix, not silently corrected during documentation work. Large-table print layout and filter behavior need browser acceptance testing.

This documentation package was authored on September 30. Inventory remains a discussed, deferred enhancement.

## 6.4 Undated refinements preserved from the conversation

These should appear in the project history, but their exact dates must not be invented:

| Change/concern | Current evidence | Exact implementation date |
| --- | --- | --- |
| Reduce owner header/logo spacing | Owner view and successive supplied screenshots. | TBC |
| Move Add New Staff to Staff Management | Owner view and user request. | TBC |
| Remove duplicate customer top-right Book Now | Customer view; sidebar Book Now retained. | TBC |
| Show multiple customer active bookings | Customer model/view iterate over ActiveBookings. | TBC |
| Add printable booking history | Customer print action/styles. | TBC |
| Add printable owner operations/customer records | Owner print handlers/styles. | TBC |
| Keep Self Service and add owner pricing | Customer catalog and Owner pricing action/view. | TBC; current schema last modified September 25 |
| Include soap/softener quantities and optional fees | Customer draft/pricing/review code. | TBC |
| Paginate owner operations at 22 rows | Owner view JavaScript. | TBC |
| Replace static customer notifications | Current DB query and view. | September 29 file cluster; exact change time TBC |
| Clarify completed bookings versus paid income | Conversation, payment/report queries. | TBC |
| Add date-filtered transaction reporting | Owner controller/view. | September 30 file cluster; exact work duration TBC |
| Consider basic consumable inventory | Conversation only; no inventory tables/module. | Deferred; not a completed task |

## 6.5 Previously supplied consultation windows

The user supplied the month/day ranges below. They are normalized to 2026 here to match the confirmed project coverage; confirm the year and attendance against school records. The windows alone do not prove which topics were discussed or what the adviser agreed to.

| Consultation ID | Start | End | Evidence/status |
| --- | --- | --- | --- |
| CW-01 | 2026-08-03 | 2026-08-07 | User-supplied window; topics/sign-off TBC. |
| CW-02 | 2026-08-10 | 2026-08-14 | User-supplied window; topics/sign-off TBC. |
| CW-03 | 2026-08-17 | 2026-08-21 | User-supplied window; topics/sign-off TBC. |
| CW-04 | 2026-08-31 | 2026-09-04 | User-supplied window; topics/sign-off TBC. |
| CW-05 | 2026-09-07 | 2026-09-11 | User-supplied window; topics/sign-off TBC. |

Do not fill these with invented signatures, approvals, completion percentages, or meeting minutes. Booking confirmation/review and moving Book Now to the left navigation were concerns reported by the user, but their assignment to a specific consultation window is unconfirmed.

## 6.6 Work breakdown structure for the Gantt chart

The predecessor column shows logical implementation dependencies, not recovered historical sequencing. “Present” means implementation is visible in current source; it does not assert complete testing. The date column is deliberately an **evidence anchor**, not an actual start/end range.

| ID | Work package | Predecessor(s) | Date evidence | Current classification |
| --- | --- | --- | --- | --- |
| W01 | Identify booking-system scope, users, and requirements | None | TBC | Scope reconstructed from discussion. |
| W02 | Prepare initial UI/MVC project structure | W01 | June 17–20 / July 27 metadata | Present; actual dates TBC. |
| W03 | Draft initial entities/data dictionary | W01 | August 6 dictionary metadata | Earlier draft exists. |
| W04 | Design normalized schema, keys, constraints, and seeds | W03 | August 6 creation; September 25 latest schema edit | Current 15-table script present. |
| W05 | Connect MVC application to database | W02, W04 | June 17 / July 27 metadata | Direct MySQL access present. |
| W06 | Implement authentication, password hashing, and roles | W05 | August 6 / September 2 metadata | Present; security validation pending. |
| W07 | Implement customer registration and staff approval | W06 | September 2 account-file cluster | Present. |
| W08 | Build role dashboards and navigation | W06 | Screenshots; date TBC | Present; some static metrics remain. |
| W09 | Implement owner staff management | W06, W08 | TBC | Present. |
| W10 | Implement owner service catalog/pricing | W04, W08 | TBC; schema edit September 25 | Present. |
| W11 | Build customer service/weight/payment form | W07, W10 | September screenshots; exact date TBC | Present. |
| W12 | Add Self Service and selectable add-ons/quantities | W10, W11 | TBC | Present; fulfillment metadata gaps. |
| W13 | Add session draft and review/edit/cancel step | W11, W12 | September 25 11:06 review view | Present; full navigation tests pending. |
| W14 | Separate cash confirmation and GCash acknowledgement | W13 | September 25 payment-view metadata | Present; GCash is prototype. |
| W15 | Render actual booking receipt and history | W13, W14 | September 25 receipt metadata | Present; some labels need care. |
| W16 | Build staff acceptance, queue, processing status | W08, W15 | September screenshots; date TBC | Present; transition gaps. |
| W17 | Support multiple customer tracking cards | W15, W16 | September screenshots; date TBC | Present; page-load based. |
| W18 | Implement staff walk-in intake | W10, W16 | TBC | Present; add-on mismatch defect. |
| W19 | Record/verify payments and calculate balances | W14, W16 | September 29 controller/view cluster | Present; reconciliation tests pending. |
| W20 | Record actual weight and recalculate charges | W12, W16, W19 | September 29 controller/view cluster | Present; estimate overwritten. |
| W21 | Load customer notifications and mark read | W19, W20 | September 29 customer-file cluster | Present; no push/detail opening. |
| W22 | Aggregate monthly income from paid transactions | W19 | Screenshots; exact first date TBC | Present. |
| W23 | Add record/report printing | W15, W16, W22 | Screenshots; exact dates TBC | Present; print QA pending. |
| W24 | Refine layout, button placement, password eyes, pagination | W08, W09, W11 | September 25 registration file; other dates TBC | Present; UI refinements. |
| W25 | Add transaction detail report and filters | W19, W22 | September 30 owner-file cluster | Present; filter visibility defect. |
| W26 | Test permissions, calculations, workflows, and print output | W13–W25 | Execution dates TBC | Test plan prepared; tests not executed here. |
| W27 | Reconcile manuscript/schema and prepare handover docs | W03–W25; coordinate with W26 | September 30, 2026 | This documentation package authored. |
| W28 | Evaluate basic inventory scope with adviser | W01, W10, W20 | Discussion only; dates TBC | Deferred; not implemented. |

W26 is not a claim that testing only begins at the end. Tests should also be run alongside each implementation task; the final package groups regression/acceptance testing for Gantt convenience.

W27 overlaps W26: the test plan can be documented before execution, and actual test results should be added afterward. The existence of this documentation does not mark W26 accepted.

## 6.7 Building an honest Gantt chart from these records

Use separate columns for **planned dates**, **actual dates**, and **evidence**. Do not put a filesystem timestamp into both actual start and actual end just to make a task look completed.

Suggested spreadsheet columns:

| Column | What to enter |
| --- | --- |
| Task ID / task name | Copy W01–W28 and adjust wording to your school's format. |
| Predecessor | Copy the logical dependency; revise if your work actually happened in a different order. |
| Planned start / finish | Your approved or reconstructed plan, explicitly labeled planned. |
| Actual start / finish | Confirm from dated notes, screenshots, submissions, or version history; otherwise leave blank/TBC. |
| Evidence date | Use the date anchors in this chapter and identify them as filesystem/user-record dates. |
| Status | Planned, source implemented, needs correction, acceptance pending, accepted, or deferred. |
| Evidence/reference | File, screenshot, consultation record, test ID, or adviser sign-off. |

For a weekly September chart, the calendar groupings are September 1–6, 7–13, 14–20, 21–27, and 28–30. These are display buckets, **not** inferred work durations. Put a milestone on September 25/29/30 only with its evidence label unless your records support a completion milestone.

Do not assign “100%” based solely on a visible button. For example, W25 has source code but a known date-control issue, so “implemented; correction/acceptance pending” is more accurate than “completed and fully tested.”

## 6.8 Ready-to-adapt monthly accomplishment narrative

During the September 2026 review period, the EZ-K Laundry System was refined around customer booking confirmation, staff processing, payment recording, and owner reporting. The current implementation includes a draft-and-review booking workflow, database-backed service lines and receipts, multiple active-order tracking cards, owner-configurable service and add-on pricing, printable records, staff actual-weight recalculation, customer notifications, and payment-level transaction reporting. Source-file date clusters provide supporting anchors on September 2, 15, 25, 29, and 30. Earlier database and security artifacts are also present. Exact task durations and acceptance dates require confirmation from project records. Inventory was discussed but deferred, and identified functional/security/print limitations remain documented for follow-up.

This paragraph is safe as a retrospective summary when accompanied by the evidence qualifications. It does not assert measured performance, completed deployment, successful live GCash integration, or adviser acceptance.

## 6.9 Evidence still needed to finalize historical dates

- The actual project proposal/start date and the date the initial design was approved.
- Dated consultation sheets for CW-01 through CW-05.
- Original screenshots/files with capture dates, rather than date text displayed inside a page.
- Any Git history, backup snapshots, or dated submissions not present in the inspected folder.
- Test execution dates and actual outcomes for Chapter 07.
- Confirmation of when printable outputs, 22-row pagination, layout changes, and Self Service pricing were first introduced.
- The adviser-approved scope decision for any future inventory work.

Until those records are supplied, retain TBC entries. A transparent reconstructed timeline is preferable to a precise but unsupported activity log.
