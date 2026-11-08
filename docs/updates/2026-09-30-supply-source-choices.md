# Detergent and Fabric-Softener Source Choices

Implemented September 30, 2026, after the original documentation snapshot.

## Scope

The customer now explicitly chooses a source independently for detergent and fabric softener:

- **Buy from shop:** select 1–20 pieces; the corresponding Powder/Downy service line is charged using the catalog price.
- **I'll bring my own:** no shop-product service line or charge.
- **Not needed:** no shop-product service line or charge. This is distinct from bringing a personal product.

An empty selection is not interpreted as customer-provided. Both source choices are required before review. Quantities are displayed/enabled only for shop products. Customers may mix sources, such as their own detergent and shop-provided Downy. Washing, Drying, Folding, Self Service, Rush Fee, and Delivery selections are retained.

## Persistence and staff visibility

Source choices remain in the session draft through review/edit and the GCash review branch. On final confirmation, a generated two-line instruction is saved to the existing `bookings.customer_notes` field in the same transaction as the booking. No schema migration is needed.

Review and receipt show the instructions. Per-item lines show quantity multiplied by unit price. Staff sees the saved customer instructions in pending bookings, active cards, and Active Queue. Owner sees them in Operations and the individual customer's booking details, including the existing print areas.

Historical bookings are not rewritten or labeled as customer-provided. Old unconfirmed drafts without explicit source choices must be reviewed again. A previously selected Powder/Downy line can preselect Shop on edit; absence of that line cannot preselect a personal product.

The notes are human-readable instructions, not a structured stock ledger. A future inventory feature should model product issuance/consumption separately rather than parse these sentences.

## Server checks

- Accept only `Shop`, `Customer`, or `None` source values.
- Validate shop quantities from 1 through 20 whole pieces.
- Accept only the four core laundry services in the core service field; add-ons are derived from their separate choices. This prevents bypassing an own-product choice by injecting Powder/Downy into that field.
- Before final confirmation, verify that Shop has exactly one matching charge line and Customer/None has none.
- Preserve the existing draft-first submission/payment flow.

## Verification performed

- Application compilation into an isolated temporary output folder: **passed, 0 errors and 0 warnings**.
- **179 isolated assertions passed** for source combinations, amount/model calculations, serialization, review/payment-page models, legacy-draft guards, invalid quantities/duplicate supply lines, direct invalid submissions, and draft cancellation. These use constructed drafts and in-memory session state; they are not 179 end-to-end shop transactions.
- Read-only database metadata check: `bookings.customer_notes` exists, permits nulls, and has sufficient capacity for generated instructions.
- No customer account, booking, payment, service price, or stock record was inserted/changed during verification.
- Browser interactions, final booking writes, and print layout were not exercised end-to-end. The currently running app was not restarted during verification; restart the project and reload Book now to use the new compiled source.

## Manual follow-up checks

1. Open Book now and select Washing. Leaving either supply source blank should prevent review.
2. Choose personal detergent and Shop Downy, quantity 2. Review should show no Powder charge and a two-piece Downy charge.
3. Use Edit Order; confirm both source choices remain selected. Change Downy to Not needed; its quantity control should disappear and its charge should be removed from the next review.
4. Test Shop/Shop, Customer/Customer, and None/None with controlled test bookings.
5. Confirm a test booking through the intended cash or GCash branch, then check the receipt and Staff/Owner customer instructions against the selected choices.
6. Confirm historical orders without notes remain unlabeled rather than being shown as customer-provided.

**Inventory was not implemented by this source-choice update.** The subsequent [per-piece inventory update](2026-09-30-piece-inventory.md) now records shop pieces when staff saves a walk-in or confirms issuance through the existing online Accept & Start/Update Status workflow. Online booking submission or payment alone does not deduct stock. There is no automatic kilogram-based dosing rule, and removed standalone issue/return/count forms remain absent.

## Files

- `Models/LaundrySupplyChoices.cs`
- `Controllers/CustomerController.cs`
- `Controllers/StaffController.cs`
- `Controllers/OwnerController.cs`
- `Views/Customer/Index.cshtml`
- `Views/Customer/ReviewBooking.cshtml`
- `Views/Customer/BookingReceipt.cshtml`
- `Views/Staff/Index.cshtml`
- `Views/Owner/Index.cshtml`
