# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

CHTLManager is a single-machine WPF point-of-sale and inventory app for a small convenience store (UI text and comments are in Vietnamese).

## Commands

```bash
dotnet build CHTLManager.slnx                      # build all 3 projects
dotnet run --project CHTLManager.WPF               # run the app
dotnet publish CHTLManager.WPF -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
```

- Solution file is `.slnx` (needs VS 2022 17.13+ or a recent .NET SDK).
- There is no test project and no linter configured.
- Sample accounts (seeded on first run): `admin / admin123` (Admin), `thungan / 123456` (Cashier).

## Architecture

Three layers, referenced strictly downward: `CHTLManager.WPF` → `CHTLManager.BLL` → `CHTLManager.DAL`.

- **DAL**: EF Core 8 + SQLite. `AppDbContext` configures itself from the static `AppDbContext.DbPath` (default `%LocalAppData%\CHTLManager\chtlmanager.db`, i.e. one DB per Windows user). Entities: `User`, `Product`, `Order`, `OrderDetail`.
- **BLL**: `ProductService`, `OrderService`, `UserService` behind interfaces in `BLL/Interfaces`. Each service takes a `Func<AppDbContext>` (parameterless ctor uses the default DB) and opens a short-lived context per call. Point `AppDbContext.DbPath` at a temp file to exercise services against a throwaway DB.
- **WPF**: code-behind (no MVVM). `AppServices` is a static service locator holding the service instances and `CurrentUser`. `MainWindow` hosts three tabs: `POSUserControl` (sales), `ProductUserControl` (inventory, Admin only) and `OrderHistoryUserControl` (order history + revenue via `OrderService.GetRevenueReport`, Admin only). Tab views reload data in their `Loaded` handler, which fires every time the tab is shown.

## Project workflow

- Plan and checklist live in `docs/PLAN.md`; tick items as they are done. Demo script and defense Q&A live in `docs/DEMO.md`.
- Work part by part; after each part, build + verify, then wait for the user's approval before committing (one commit per part). Pushing is a separate decision.

## Conventions and gotchas

- Throw `BusinessException` for user-facing validation errors; the UI shows its message directly and treats other exceptions as system errors.
- `OrderService.CreateOrder` is the core sale path: one transaction that re-reads prices from the DB (never trust UI prices), checks/decrements stock, and generates the code `HDyyyyMMdd-NNNN` from a count of today's orders.
- Products already referenced by orders are soft-deleted (`IsActive = false`); `ProductService.Add` with that barcode reactivates the same row. `ProductService.Update` never changes `Stock` — stock only moves through `UpdateStock` (import), `SetCountedStock` (stock count) and sales.
- Stock may be negative by design: `CreateOrder(..., allowOversellProductIds)` lets the cashier sell beyond recorded stock for products they explicitly confirmed (real shelf stock differs from the software). `Product.NeedsStockCheck` (`Stock < 0`) drives the red `StockCellStyle` in `App.xaml` and the "cần kiểm kê" filter. Never validate `Stock >= 0` on update paths; only `Add` and `SetCountedStock` require non-negative input.
- `DbInitializer` seeds users/products when empty and ~20 sample orders over the last 7 days only when `EnsureCreated()` just created the DB.
- Money is `decimal` mapped to `decimal(18,0)` (VND, no fractions). **SQLite cannot `Sum` or `OrderBy` decimals server-side** (EF throws `NotSupportedException`) — materialize first and aggregate in memory.
- Schema is created with `Database.EnsureCreated()` in `DbInitializer` (no migrations). Schema changes will not apply to existing DB files; delete the `.db` during development or introduce migrations first.
- Authorization is UI-only (Admin tabs hidden for Cashier); BLL services do not check roles.
- Passwords are hashed with PBKDF2 (`BLL/Security/PasswordHasher`), stored as `iterations.salt.hash`.
- `App.OnStartup` forces `vi-VN` culture; money input goes through `WPF/Helpers/MoneyParser` (positive integers only, `.`/`,` accepted only as thousand separators).
- Verification without the UI: a scratch console project referencing `CHTLManager.BLL` can set `AppDbContext.DbPath` to a temp file, call `DbInitializer.Initialize()`, then exercise the services.
