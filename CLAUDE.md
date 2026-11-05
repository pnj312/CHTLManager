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

- **DAL**: EF Core 8 + SQLite. `AppDbContext` configures itself from the static `AppDbContext.DbPath` (default `%LocalAppData%\CHTLManager\chtlmanager.db`, i.e. one DB per Windows user). Entities: `User`, `Product`, `Order`, `OrderDetail`, `StockMovement`, `Supplier`, `PurchaseReceipt(Line)`, `SupplierPayment`, `StockDiscrepancy`, `CompensationPayment`.
- **BLL**: `ProductService`, `OrderService`, `PurchaseService`, `StockCountService`, `UserService` behind interfaces in `BLL/Interfaces`. Each service takes a `Func<AppDbContext>` (parameterless ctor uses the default DB) and opens a short-lived context per call. Point `AppDbContext.DbPath` at a temp file to exercise services against a throwaway DB.
- **WPF**: code-behind (no MVVM). `AppServices` is a static service locator holding the service instances and `CurrentUser`. `MainWindow` hosts tabs: `POSUserControl` (sales) for everyone, and Admin only `ProductUserControl`, `DiscrepancyUserControl`, `PurchaseUserControl`, `SupplierDebtUserControl`, `OrderHistoryUserControl`, `StockLogUserControl`, `UserUserControl`. The header has `btnReceive` (cashier only, opens `PurchaseReceiptWindow`), `btnReminder` (Admin, pending work; refresh it via `AppServices.NotifyChanged()`) and the change-password button. Tab views reload data in their `Loaded` handler, which fires every time the tab is shown. A `SelectionChanged` from a DataGrid/ComboBox inside a tab bubbles up to `tabMain`; `MainWindow` filters by `e.Source`.
- `UserService.Login` returns null for wrong credentials but throws `BusinessException` for a locked account (only after the password matched). `SetActive` refuses self-lock and locking the last active Admin.

## Project workflow

- Plan and checklist live in `docs/PLAN.md`; tick items as they are done. Demo script and defense Q&A live in `docs/DEMO.md`.
- Work part by part, one commit per part, after build and verification. Ask before pushing.

## Conventions and gotchas

- Throw `BusinessException` for user-facing validation errors; the UI shows its message directly and treats other exceptions as system errors.
- `OrderService.CreateOrder` is the core sale path: one transaction that re-reads prices from the DB (never trust UI prices), checks/decrements stock, and generates the code `HDyyyyMMdd-NNNN` from a count of today's orders.
- Products with stock cannot be deleted; with history they are soft-deleted. `ProductService.Add` with a soft-deleted barcode reactivates the row and keeps its stock (no back door around counting). `ProductService.Update` never changes `Stock`.
- `Stock` moves only through sales, `CancelOrder`, receipts (`PurchaseService`), `StockCountService.Count` and `Add` (initial). Every move except a normal sale writes a `StockMovement` in the same `SaveChanges` (`StockLedger.Apply`); there is deliberately no API to edit or delete them.
- `Stock` may be negative by design: a ledger number, not shelf quantity. `CreateOrder(..., allowOversellProductIds)` sells beyond recorded stock for confirmed products, capped at `OrderService.MaxOutOfBookPerLine`. Receipts refuse while `Stock < 0`: those units belong to an earlier lot, so the UI sends the user to `StockCountWindow` first. Users never see a negative number: show `Product.StockDisplay`, never `Stock`. Never validate `Stock >= 0` on update paths.
- Costing: `Product.AverageCost` is the moving average (`CostCalculator`), updated only when a receipt has prices. `OrderDetail.UnitCost` is frozen at sale time; `CostIsEstimated` flags out-of-book or no-cost lines. Cost must never reach the cashier UI. Counted surplus already sits in the average at the old cost, so resolving an unrecorded lot uses `ReplaceInAverage`, not `AddToAverage`. `AverageCost` 0 with stock on hand means cost unknown (data from before costing): the next receipt sets the average to its own cost instead of diluting it. Removing or re-pricing a lot works on the stock at the moment that lot entered the average (`PurchaseService.CostPool`), not current stock: sales do not change unit cost.
- Debt is per supplier: confirmed receipts (received × cost) minus non-cancelled `SupplierPayment`s. Mistakes are fixed by cancelling with a reason, never by deleting. A wrong price on a confirmed receipt goes through `CorrectReceiptCosts` (debt and average change, stock does not, logged as `CostCorrected`, sold lines keep their frozen cost); cancel and re-enter would create phantom stock once goods are sold. Cancelling a receipt or lowering its price is blocked if payments would exceed purchases; receipts from a stock count cannot be cancelled but can be re-priced. Invoice numbers compare with all whitespace removed and case ignored.
- Stock count is two steps: `Count` changes stock now and opens a pending `StockDiscrepancy`; `ResolveShortage`/`ResolveSurplus` decide where the difference goes. Miscount needs a note and is reported separately so it cannot hide shrinkage. `ProfitReport.NetAfterStockDifferences` is not net profit: no operating costs.
- `DbInitializer.Initialize(seedDemoData)` creates the first Admin; `--demo` adds 7 days of chronological sample activity through real documents, only on a store with no products and no orders. `DemoSeedTests` reconciles it, so keep it consistent.
- Money is `decimal` mapped to `decimal(18,0)` (VND); costs use `decimal(18,2)` because averages have fractions. **SQLite cannot `Sum` or `OrderBy` decimals server-side** (EF throws `NotSupportedException`) — materialize first and aggregate in memory.
- Schema changes go through EF Migrations, never by deleting the DB: real stores keep their data. `DatabaseUpgrader.Upgrade()` runs at startup: `VACUUM INTO` backup before any change (keeps 5), marks DB files from before migrations as baseline `InitialCreate` after repairing their stock-log schema. `CHTLManager.Tests/LegacySchema.sql` is the frozen old schema; do not regenerate it.
- Authorization is UI-only (Admin tabs hidden for Cashier); BLL services do not check roles.
- Passwords are hashed with PBKDF2 (`BLL/Security/PasswordHasher`), stored as `iterations.salt.hash`.
- `App.OnStartup` forces `vi-VN` culture; money input goes through `WPF/Helpers/MoneyParser` (positive integers only, `.`/`,` accepted only as thousand separators).
- Tests use `TestDb` (`CHTLManager.Tests/TestDb.cs`): a temp DB per test via the static `AppDbContext.DbPath`, so parallelization is disabled.
