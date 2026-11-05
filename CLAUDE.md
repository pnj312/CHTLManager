# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

CHTLManager is a single-machine WPF point-of-sale and inventory app for a small convenience store (UI text and comments are in Vietnamese).

## Commands

```bash
dotnet build CHTLManager.slnx                      # build all 3 projects
dotnet test CHTLManager.Tests                      # BLL tests (xUnit, temp SQLite files)
dotnet test CHTLManager.Tests --filter "FullyQualifiedName~DatabaseUpgraderTests"   # one class
dotnet run --project CHTLManager.WPF               # run the app (real store: only an Admin account)
dotnet run --project CHTLManager.WPF -- --demo     # with sample data, when the store has no data yet
dotnet tool restore                                # then: dotnet ef migrations add <Name> --project CHTLManager.DAL
dotnet publish CHTLManager.WPF -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

- Solution file is `.slnx` (needs VS 2022 17.13+ or a recent .NET SDK).
- No linter configured. Tests cover the BLL only; UI is checked by hand or UI Automation.
- First run creates `admin / admin123`. `--demo` also creates `thungan / 123456`, products and sample orders.

## Architecture

Three layers, referenced strictly downward: `CHTLManager.WPF` → `CHTLManager.BLL` → `CHTLManager.DAL`.

- **DAL**: EF Core 8 + SQLite. `AppDbContext` configures itself from the static `AppDbContext.DbPath` (default `%LocalAppData%\CHTLManager\chtlmanager.db`, i.e. one DB per Windows user). Entities: `User`, `Product`, `Order`, `OrderDetail`.
- **BLL**: `ProductService`, `OrderService`, `UserService` behind interfaces in `BLL/Interfaces`. Each service takes a `Func<AppDbContext>` (parameterless ctor uses the default DB) and opens a short-lived context per call. Point `AppDbContext.DbPath` at a temp file to exercise services against a throwaway DB.
- **WPF**: code-behind (no MVVM). `AppServices` is a static service locator holding the service instances and `CurrentUser`. `MainWindow` hosts five tabs: `POSUserControl` (sales), `ProductUserControl` (inventory, Admin only; opens `ImportStockWindow` and `StockCountWindow`), `OrderHistoryUserControl` (order history + revenue via `OrderService.GetRevenueReport`, Admin only), `StockLogUserControl` (stock log + loss via `ProductService.GetStockLog`, read-only, Admin only) and `UserUserControl` (accounts, Admin only); the header has a "Đổi mật khẩu" button opening `ChangePasswordWindow` for any role. Tab views reload data in their `Loaded` handler, which fires every time the tab is shown. A `SelectionChanged` from a DataGrid/ComboBox inside a tab bubbles up to `tabMain`; `MainWindow` filters by `e.Source`.
- `UserService.Login` returns null for wrong credentials but throws `BusinessException` for a locked account (only after the password matched). `SetActive` refuses self-lock and locking the last active Admin.

## Project workflow

- Plan and checklist live in `docs/PLAN.md`; tick items as they are done. Demo script and defense Q&A live in `docs/DEMO.md`.
- Work part by part; after each part, build + verify, then wait for the user's approval before committing (one commit per part). Pushing is a separate decision.

## Conventions and gotchas

- Throw `BusinessException` for user-facing validation errors; the UI shows its message directly and treats other exceptions as system errors.
- `OrderService.CreateOrder` is the core sale path: one transaction that re-reads prices from the DB (never trust UI prices), checks/decrements stock, and generates the code `HDyyyyMMdd-NNNN` from a count of today's orders.
- Products already referenced by orders are soft-deleted (`IsActive = false`); `ProductService.Add` with that barcode reactivates the same row. `ProductService.Update` never changes `Stock` — stock only moves through `ImportStock`, `SetCountedStock` (stock count), `Add` (initial/reactivate) and sales.
- `Stock` may be negative by design: it is a ledger number, not shelf quantity. `CreateOrder(..., allowOversellProductIds)` lets the cashier sell beyond recorded stock for products they explicitly confirmed; the negative value lets a later stock count compute the true size of the unrecorded lot. `ImportStock` refuses while `Stock < 0` (and requires supplier + invoice number): those units belong to an earlier lot, so the UI sends the user through `StockCountWindow` first, then `ImportStockWindow`, keeping each lot's logged quantity equal to its invoice. Users never see a negative number: show `Product.StockDisplay`/`FormatStock` ("0 (bán ngoài sổ: N)"), never `Stock` directly. `Product.NeedsStockCheck` drives the red `StockCellStyle` and the "cần kiểm kê" filter. Never validate `Stock >= 0` on update paths; only `Add` and `SetCountedStock` require non-negative input.
- Every stock change except a normal sale must write a `StockMovement` row in the same `SaveChanges` (`ProductService.ChangeStock`, out-of-book branch of `CreateOrder`); there is deliberately no API to edit or delete them. Products with movements are soft-deleted. `SetCountedStock` requires a reason matching the direction of the difference. Loss = `StockMovement.IsLoss` (Lost/Damaged/Expired), valued at sale price because there is no cost price.
- `DbInitializer.Initialize(seedDemoData)` creates the first Admin; sample data only with `--demo` on a store with no products and no orders.
- Money is `decimal` mapped to `decimal(18,0)` (VND, no fractions). **SQLite cannot `Sum` or `OrderBy` decimals server-side** (EF throws `NotSupportedException`) — materialize first and aggregate in memory.
- Schema changes go through EF Migrations, never by deleting the DB: real stores keep their data. `DatabaseUpgrader.Upgrade()` runs at startup: `VACUUM INTO` backup before any change (keeps 5), marks DB files from before migrations as baseline `InitialCreate`. `CHTLManager.Tests/LegacySchema.sql` is the frozen old schema; do not regenerate it.
- Authorization is UI-only (Admin tabs hidden for Cashier); BLL services do not check roles.
- Passwords are hashed with PBKDF2 (`BLL/Security/PasswordHasher`), stored as `iterations.salt.hash`.
- `App.OnStartup` forces `vi-VN` culture; money input goes through `WPF/Helpers/MoneyParser` (positive integers only, `.`/`,` accepted only as thousand separators).
- Tests use `TestDb` (`CHTLManager.Tests/TestDb.cs`): a temp DB per test via the static `AppDbContext.DbPath`, so parallelization is disabled.
