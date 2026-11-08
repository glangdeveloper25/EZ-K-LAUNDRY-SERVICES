# Architecture, Setup, Security, and Maintenance

Documentation baseline: **30 September 2026**. This chapter describes the inspected repository and provides setup instructions for a separate development/test environment. Commands shown below are instructions, not claims that a fresh installation, production deployment, or security assessment was performed during documentation.

## 1. Application architecture

EZ-K Laundry System is an ASP.NET Core MVC web application. The browser receives server-rendered Razor views. JavaScript handles section switching, modals, form calculations, password visibility, pagination, and browser printing. Controllers receive HTTP requests, validate inputs, query/update MySQL through `MySql.Data`, prepare view models, and return views or redirects.

It is a single application, not a microservice system or a separate JavaScript single-page application. Most role-dashboard tabs are sections of one Razor view; revealing a tab does not automatically reload its underlying data. Current tracking and notification data is obtained during the dashboard request, without an implemented push or polling service.

The main request sequence is: browser form or navigation request; conventional MVC route; role/session check in a controller; parameterized SQL through a MySQL connection; view-model construction; Razor rendering; browser display. Updates commonly use the POST-redirect-GET pattern. Database transactions are explicitly used around several multi-table operations, including final booking creation, payment recording, actual-weight updates, and status-history writes.

### 1.1 Technology baseline

| Component | Evidence in repository | Documentation qualification |
| --- | --- | --- |
| Web framework | `Microsoft.NET.Sdk.Web`, target `net10.0` | Requires a compatible .NET 10 SDK/runtime for development/execution |
| Language | C# controllers/models and Razor `.cshtml` views | Nullable reference checking and implicit usings enabled |
| Database client | NuGet `MySql.Data` version `9.7.0` | Connector version is not the installed database server version |
| Database design | `laundry_final_3nf.sql` | Script identifies MySQL 8.0 / MariaDB-compatible intent; actual target-version compatibility still needs testing |
| Styling/UI | Local Bootstrap plus page-specific CSS | Much dashboard styling remains embedded in views |
| Browser scripts | JavaScript, local jQuery and Bootstrap bundle | No separate frontend build pipeline is declared in the project file |
| Authentication state | ASP.NET session plus custom controller checks | Not an ASP.NET Identity implementation |
| Printing | Browser `window.print()` and print CSS | Not a server-side PDF generation service |

The local SDK inspection during this documentation reported `10.0.300`; that is an environment observation, not a requirement to use precisely that patch version. Source: [project file](../EZKLaundrySystem.csproj), [Program.cs](../Program.cs), [shared layout](../Views/Shared/_Layout.cshtml).

## 2. Project map and responsibilities

| Location | Responsibility |
| --- | --- |
| `Program.cs` | MVC services, session settings, middleware, static assets, default route |
| `Controllers/AccountController.cs` | Customer registration, general/Staff login, session logout |
| `Controllers/CustomerController.cs` | Customer dashboard, session booking draft, review/payment/receipt flow, notifications, account updates, reward redemption |
| `Controllers/StaffController.cs` | Customer approval, queues, walk-ins, processing status, actual weight, payment recording, pickup data |
| `Controllers/OwnerController.cs` | Owner setup/login, staff administration, pricing, customer/booking records, income/transaction reports |
| `Controllers/HomeController.cs` | Landing/privacy views and error page |
| `Data/DbConnection.cs` | Creates `MySqlConnection` instances using the configured connection string |
| `Data/PasswordSecurity.cs` | Password policy, PBKDF2 hash generation and verification |
| `Models/` | Registration/user models, custom password validation attribute, error model |
| `Views/` | Razor pages grouped by controller; shared layout and validation partial |
| `wwwroot/` | Public assets, local libraries, CSS/JavaScript, laundry logo |
| `Properties/launchSettings.json` | Local development HTTP/HTTPS profiles |
| `appsettings*.json` | Logging and host settings; database configuration is not wired here in the current helper |
| `laundry_final_3nf.sql` | Tables, reference data, constraints, indexes, and reporting views |
| `docs/` | This documentation package; not an application module |
| `bin/`, `obj/` | Build outputs/intermediate artifacts; not authoritative application source |

Several dashboard/view-model classes are declared in their controller files rather than in separate `Models` files. Each main controller creates its own `DbConnection` helper. There is no implemented ORM/Entity Framework layer, repository/service abstraction, or schema-migration framework in the inspected project. The code uses synchronous database commands. These are maintainability characteristics, not automatically defects in a small educational application.

## 3. Startup, routing, session, and configuration

`Program.cs` registers MVC views, session, and an HTTP-context accessor. Session is configured for a **30-minute idle timeout**, with its cookie marked HttpOnly and essential. Session middleware runs after routing and before authorization middleware. The conventional route defaults to `{controller=Home}/{action=Index}/{id?}`. Static files are served through the configured static-file/static-asset calls.

The project does not explicitly register an authentication scheme or use `UseAuthentication`. Role protection is implemented by controller session checks, not by assuming `UseAuthorization()` alone signs in or protects users. The entry point also does not explicitly show a distributed session-cache registration; verify session-store service availability when reproducing deployment rather than assuming a production-ready distributed store exists.

For non-Development environments, the app uses `/Home/Error` and HSTS. HTTPS redirection is commented out. The HTTP development profile points to `http://localhost:5210`; the HTTPS profile includes `https://localhost:7128` and the HTTP address. A URL in `launchSettings.json` is a development convenience, not an internet deployment configuration.

The database connection string currently resides in source in `Data/DbConnection.cs`. Do not copy its actual credentials into manuscripts, screenshots, repository exports, or troubleshooting messages. A documentation-only placeholder representation is:

```text
Server=<database-host>;Database=<isolated-test-database>;Uid=<application-user>;Pwd=<secret>;
```

This placeholder is not a claim that environment-variable configuration is already implemented. Moving connection settings into protected configuration and using a restricted application database account are recommended hardening tasks, not changes performed here.

## 4. Safe local setup on a separate environment

### 4.1 Preparation

Use a separate project copy and an isolated database server/instance for installation testing. Preserve the working project and current database first. Do not troubleshoot by deleting database tables or importing over real records. Required tools are a compatible .NET 10 SDK, a compatible MySQL/MariaDB server, and an SQL administration client; use the actual server/version approved for the project and test its constraint behavior.

The SQL file contains both `CREATE DATABASE IF NOT EXISTS laundry_final` and `USE laundry_final`. Selecting a different database in a graphical SQL client does **not** override that embedded `USE` command. Either run the original script only on an isolated server intended for this installation, or prepare/review an explicitly renamed copy before import. Do not change/import the live schema merely to follow these notes.

### 4.2 Schema initialization

Inspect the script before executing it in the isolated environment. It creates InnoDB tables, uses `utf8mb4`, inserts reference data, and defines three aggregate views. It does not create a default login user because application-generated password hashes are expected.

Although the script does not drop tables and uses `CREATE TABLE IF NOT EXISTS`, it is **not read-only or a harmless schema checker**. Its `ON DUPLICATE KEY UPDATE` seed statements can replace existing reference descriptions, service prices, and other lookup properties. `CREATE OR REPLACE VIEW` also rewrites view definitions. Re-importing can reset configured prices. `IF NOT EXISTS` will not migrate the columns of an already-existing incompatible table.

The script's `SET time_zone = '+08:00'` affects that SQL session. Do not assume it permanently configures every connection the application later opens. See the reporting chapter for date-boundary implications.

### 4.3 Application startup

In the separate project copy, point the connection helper at the isolated database using safe local credentials. Keep secrets out of documentation and screenshots. Then execute the following commands individually from that copy's project directory:

```powershell
dotnet --info
dotnet restore
dotnet build
dotnet run --launch-profile http
```

`restore` obtains declared packages; `build` compiles C# and Razor; `run` starts the development application. These steps write build artifacts and can use network access. They were not run as part of this chapter's creation. If port 5210 is occupied, identify the existing application before stopping anything; do not terminate unrelated processes.

Open the displayed local application address and verify the home and login pages. A successful login-page response proves reachability only, not correct booking or financial processing. Test database connectivity and the isolated account setup before broader workflow tests.

### 4.4 Initial accounts and usable services

On a fresh database, use the Owner setup route while no Owner account exists. Sign in as Owner, create Staff through Staff Management, and verify the active service prices. Self Service is inserted/activated by the owner's pricing update and requires a positive price to be available to customers.

Register a dedicated test Customer through the application. The new account is Pending until Staff approves it. After approval, use separate browser profiles/sessions for Owner, Staff, and Customer when testing cross-role behavior. Do not seed plaintext passwords or reuse personal credentials. Record test references separately from real shop transactions.

## 5. Security controls present in source

### 5.1 Password handling

`PasswordSecurity` requires 8–128 characters with uppercase, lowercase, numeric, and non-alphanumeric characters. Hashing uses PBKDF2-SHA256 with **100,000 iterations**, a random **16-byte salt**, and a **32-byte derived hash**. The stored format includes algorithm prefix, iteration count, Base64 salt, and Base64 hash. Verification derives the candidate and compares using `CryptographicOperations.FixedTimeEquals`.

This is a description of the implementation, not a certification that its settings satisfy every current security standard. Registration, Staff creation, and password-change flows call password-policy/hash helpers. The password eye control only changes browser visibility. Stored hashes and salts should still be treated as sensitive account data and excluded from published documentation.

### 5.2 Request and database protections

Account, Customer, Staff, and Owner controllers carry `AutoValidateAntiforgeryToken`. MVC form Tag Helpers are available through `_ViewImports`, and mutating workflows generally use POST forms. Queries use parameters for user-supplied values. Some query structure is assembled from server-controlled whitelists; this is distinct from concatenating raw user input into SQL.

Customer booking/history/notification queries restrict records by the logged-in customer ID. Staff/Owner mutations check the relevant role. Payment recording and actual-weight changes lock selected bookings while calculating amounts and writing related records. Database foreign keys and check constraints add another integrity layer. A transaction protects participating statements, not every business rule everywhere in the application.

### 5.3 Limits and hardening needs

No login rate limiting, MFA, email verification, forgotten-password delivery, or full login/audit monitoring was found. Session role checks do not re-query account status on each request, so deactivating Staff does not demonstrate immediate revocation of an existing session. Logout is a GET-reachable session-clearing action. Owner initial registration is guarded by an existence query, not a demonstrated concurrency-safe setup lock.

Some endpoints enforce only a subset of expected validations. General status updating can bypass stricter dedicated completion/cancellation rules; profile editing has weaker validation than registration; duplicate partial payment submissions are not fully idempotent. Details appear in the workflow and limitations chapters. Do not describe the project as penetration-tested, production-hardened, or error-free without separate evidence.

Before public deployment, review HTTPS enforcement, cookie security settings, trusted host/reverse-proxy setup, secret storage, database privileges, account/session revocation, centralized authorization, input validation, and safe error logging. These recommendations do not authorize changing the current application during this documentation task.

## 6. External resources and offline behavior

Bootstrap CSS/JavaScript, jQuery, and validation libraries are referenced locally under `wwwroot/lib`. The shared layout loads Bootstrap Icons from a CDN, Google Fonts, and SweetAlert2 from a CDN. The payment screen loads an external GCash logo and QR image. An offline classroom demo can therefore lose icons, fonts, alerts, or payment-screen imagery even if the local app/database is running.

The current QR encodes sample text, not a verified merchant payment request. No payment gateway SDK/callback or SMS-provider integration was found. Public layout/FAQ copy mentions SMS, real-time tracking, automatic rush prioritization, and no delivery, while implemented screens include a Delivery Fee and database-backed refresh-on-load tracking. These text claims require reconciliation with actual scope. Queue-loading SQL currently orders by booking time, not an implemented rush-priority scheduling algorithm.

## 7. Maintenance, backups, and handoff

Back up source, the schema, and a controlled database export before migrations or major changes. A source-code archive is not a database backup. Store backups with limited access because customer names, contact details, payment records, notifications, and password hashes are sensitive. Test restoring to an isolated server; a backup is not verified solely because a file exists. No automated backup/restore module is implemented in this repository.

Maintain a change log with actual dates, affected files, database changes, test results, and known limitations. Do not use copied file timestamps as guaranteed development dates. Keep screenshots and test data anonymized when used in a school manuscript. Package only the intended source/documentation, not credentials, personal data exports, or arbitrary `tmp`/build outputs.

The application currently has no automated test project or migration history framework. Use the supplied manual test plan, record outcomes, and add automated coverage incrementally. A clean build, successful page load, and source review are useful checks but do not establish complete payment, report, role-security, browser-print, or multi-user correctness.

Source references: [Program](../Program.cs), [project](../EZKLaundrySystem.csproj), [launch profiles](../Properties/launchSettings.json), [database script](../laundry_final_3nf.sql), [password helper](../Data/PasswordSecurity.cs), [shared layout](../Views/Shared/_Layout.cshtml), [view imports](../Views/_ViewImports.cshtml), and the role controllers mapped above.
