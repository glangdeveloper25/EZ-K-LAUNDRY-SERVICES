# Basic Rewards Rules and Verification

Implemented October 1, 2026 after the user approved the basic points-and-discount proposal. This note records the implementation and supersedes the rewards prototype descriptions in the September 30 documentation snapshot. It does not claim adviser approval or amend the original Word manuscript.

## Customer rules

- Customers can earn from their first eligible order. There is no separate regular-customer status, membership tier, welcome bonus, referral bonus, or free-wash reward.
- An order earns one point per full PHP 10 actually paid, only when it is both Completed and fully paid. PHP 200 earns 20 points; PHP 209.99 also earns 20. Unverified GCash submissions and partial payments do not qualify by themselves.
- Each booking earns once. Points use the paid amount capped at the net order charge, so discounts and accidental overpayments do not create extra points.
- A customer may use 100 points for PHP 50 off a new online booking. The estimated subtotal must be at least PHP 50, and only one reward discount can be used per booking.
- Clicking Redeem Discount selects the option and opens Book now. It does not spend points. Reviewing, editing, or abandoning the draft also does not spend points. The 100 points are deducted atomically with final booking confirmation.
- If staff changes the actual weight, the discount remains capped at the final order total. The amount due cannot become negative, and there is no cash change. A fully reward-covered order earns zero points.
- Cancelling a discounted booking restores its 100 points once. This restoration is not a cash refund or an inventory return.

The dashboard, Rewards page, and account card show the real points balance. Points History shows the latest 30 ledger entries with dates and booking references. A customer without recorded points sees zero rather than the previous sample 1,250.

## Simple explanation for a demonstration

“May points ang bawat fully paid at completed na order. Bawat PHP 10 na binayaran ay may isang point. Kapag nakaipon ng 100 points, puwede itong gamitin para sa PHP 50 discount sa susunod na booking.”

## Saved data and safeguards

The existing `reward_transactions` table remains the balance source. Two additive tables support the workflow: `reward_accounts` serializes changes for the same customer, while `booking_rewards` records the discount, earning, and cancellation-return markers for each booking. `v_booking_totals` now exposes the currency-rounded net charge after an active reward discount.

Booking, payment, reward, and points changes use one transaction where applicable. Booking locks and customer-specific reward locks prevent duplicate earning and concurrent overspending. Booking drafts and reward selections are bound to the signed-in customer. Server-side checks do not rely on the displayed balance or disabled button.

Staff payment validation, actual-weight totals, customer receipts/history, and owner booking totals use the discount. Income reports still sum actual Paid payment records; the discount is not recorded as income or as a fictitious payment.

Local rewards setup was applied using the additive schema. Existing user, booking, payment, and reward-ledger row counts were unchanged. No historical orders were automatically awarded points. Future qualifying payment/completion events can earn points; simply opening a page cannot backfill history. The rerunnable setup script is `migrations/20261001_basic_rewards.sql`.

## Verification

The application build passed with zero warnings and zero errors. Fifty-two checks passed against a newly created, isolated MySQL test database with synthetic accounts and orders. They covered earning before/after payment, partial/unverified payments, rounding, zero totals, redemption, cancellation restoration, repeated confirmation, concurrent earning/redemption, missing setup, cross-account drafts, stale reward balances, and unchanged income-report semantics. No live business transactions were created by the tests.

Test code is under `obj/rewards-checks/`. Test balances and payments belong only to isolated `ezk_rewards_test_` databases, not the shop database. The application was not automatically restarted; restart the local app to load the new compiled code, then refresh the browser.

The offline browser fixture passed 14 scenarios and 219 assertions using compiled Razor views and synthetic models. It checked zero/99/100/150-point balances, unavailable setup, real history rendering, disabled-button visibility, booking selection, review/receipt/GCash totals, zero-payment wording, antiforgery fields, and HTML encoding. All POST requests and real controller routes were blocked in that fixture. Desktop screenshots were inspected; the 600-pixel review page was also checked, but a 390-pixel customer layout was not certified. The in-app browser connection was unavailable, so these checks used an isolated local browser preview. Results are under `obj/rewards-ui-checks/`.

For a demonstration, use disposable test accounts: complete and fully pay a PHP 200 order to earn 20 points, verify that repeated completion does not award again, then use a test account that has legitimately accumulated 100 points to check a PHP 50 discount. Do not fabricate real customer points or payments just to demonstrate redemption.

## Existing limitations outside this change

The GCash QR screen remains the pre-existing placeholder/payment-declaration flow, not a live payment gateway. Reward availability is checked before showing its instructions and again at final confirmation, but points are not reserved while the customer remains on that page. The existing customer dashboard's narrow-screen sidebar layout is not redesigned by this change. No notification, inventory, unrelated dashboard metric, or original manuscript feature was added.
