using CHTLManager.DAL;
using Microsoft.EntityFrameworkCore;

namespace CHTLManager.BLL;

/// <summary>Kết quả nâng cấp CSDL khi mở ứng dụng.</summary>
public sealed record UpgradeResult(bool IsNew, bool WasLegacy, string? BackupPath);

/// <summary>
/// Đưa CSDL về phiên bản mới nhất mà không mất dữ liệu: tạo mới nếu chưa có, nhận diện CSDL cũ
/// (tạo trước khi có Migrations), và sao lưu trước mỗi lần thay đổi cấu trúc.
/// </summary>
public static class DatabaseUpgrader
{
    private const int BackupsToKeep = 5;

    public static UpgradeResult Upgrade()
    {
        string path = AppDbContext.DbPath;
        bool fileExists = File.Exists(path) && new FileInfo(path).Length > 0;
        string? backupPath = null;

        try
        {
            using var db = new AppDbContext();
            if (!fileExists)
            {
                db.Database.Migrate();
                return new UpgradeResult(IsNew: true, WasLegacy: false, BackupPath: null);
            }

            bool isLegacy = TableExists(db, "Users") && !TableExists(db, "__EFMigrationsHistory");
            if (isLegacy)
            {
                backupPath = Backup(db, path);
                MarkLegacyAsBaseline(db);
            }

            if (db.Database.GetPendingMigrations().Any())
            {
                backupPath ??= Backup(db, path);
                db.Database.Migrate();
            }

            return new UpgradeResult(IsNew: false, WasLegacy: isLegacy, BackupPath: backupPath);
        }
        catch (Exception ex)
        {
            string backupNote = backupPath == null ? string.Empty : $"\nBản sao lưu: {backupPath}";
            throw new BusinessException($"Không nâng cấp được cơ sở dữ liệu tại {path}. Dữ liệu chưa bị thay đổi thêm.{backupNote}\nChi tiết: {ex.Message}");
        }
    }

    private static bool TableExists(AppDbContext db, string table)
        => db.Database
            .SqlQuery<int>($"SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type = 'table' AND name = {table}")
            .ToList()[0] > 0;

    /// <summary>Sao lưu nhất quán (kể cả phần còn trong WAL) ra file cùng thư mục, chỉ giữ vài bản mới nhất.</summary>
    private static string Backup(AppDbContext db, string path)
    {
        string directory = Path.GetDirectoryName(path)!;
        string name = Path.GetFileNameWithoutExtension(path);
        string target = Path.Combine(directory, $"{name}.backup-{DateTime.Now:yyyyMMdd-HHmmss}.db");
        for (int i = 1; File.Exists(target); i++)
            target = Path.Combine(directory, $"{name}.backup-{DateTime.Now:yyyyMMdd-HHmmss}-{i}.db");

        db.Database.ExecuteSqlRaw("VACUUM INTO '" + target.Replace("'", "''") + "'");

        // Tên file chứa thời gian nên sắp xếp theo tên cũng là theo thời gian.
        var outdated = Directory.GetFiles(directory, $"{name}.backup-*.db")
            .OrderByDescending(f => f, StringComparer.Ordinal)
            .Skip(BackupsToKeep);
        foreach (string file in outdated) File.Delete(file);

        return target;
    }

    /// <summary>CSDL cũ đã có đủ bảng: bù phần còn thiếu, rồi đánh dấu migration đầu tiên là đã chạy.</summary>
    private static void MarkLegacyAsBaseline(AppDbContext db)
    {
        string baseline = db.Database.GetMigrations().First(m => m.EndsWith("_InitialCreate"));

        using var transaction = db.Database.BeginTransaction();
        RepairStockMovements(db);
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE "__EFMigrationsHistory" (
                "MigrationId" TEXT NOT NULL CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY,
                "ProductVersion" TEXT NOT NULL
            )
            """);
        db.Database.ExecuteSql($"INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") VALUES ({baseline}, '8.0.11')");
        transaction.Commit();
    }

    /// <summary>Các bản cũ có thể chưa có bảng nhật ký kho hoặc hai cột nhà cung cấp, số hóa đơn.</summary>
    private static void RepairStockMovements(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "StockMovements" (
                "Id" INTEGER NOT NULL CONSTRAINT "PK_StockMovements" PRIMARY KEY AUTOINCREMENT,
                "ProductId" INTEGER NOT NULL,
                "UserId" INTEGER NOT NULL,
                "OrderId" INTEGER NULL,
                "CreatedAt" TEXT NOT NULL,
                "Type" TEXT NOT NULL,
                "BeforeStock" INTEGER NOT NULL,
                "AfterStock" INTEGER NOT NULL,
                "Delta" INTEGER NOT NULL,
                "Reason" TEXT NOT NULL,
                "UnitPrice" decimal(18,0) NOT NULL,
                "Supplier" TEXT NULL,
                "InvoiceNo" TEXT NULL,
                CONSTRAINT "FK_StockMovements_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_StockMovements_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE RESTRICT,
                CONSTRAINT "FK_StockMovements_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE RESTRICT
            )
            """);

        bool hasSupplierColumn = db.Database
            .SqlQueryRaw<int>("SELECT COUNT(*) AS \"Value\" FROM pragma_table_info('StockMovements') WHERE name = 'Supplier'")
            .ToList()[0] > 0;
        if (!hasSupplierColumn)
        {
            db.Database.ExecuteSqlRaw("ALTER TABLE \"StockMovements\" ADD COLUMN \"Supplier\" TEXT NULL");
            db.Database.ExecuteSqlRaw("ALTER TABLE \"StockMovements\" ADD COLUMN \"InvoiceNo\" TEXT NULL");
        }

        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_StockMovements_OrderId\" ON \"StockMovements\" (\"OrderId\")");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_StockMovements_ProductId\" ON \"StockMovements\" (\"ProductId\")");
        db.Database.ExecuteSqlRaw("CREATE INDEX IF NOT EXISTS \"IX_StockMovements_UserId\" ON \"StockMovements\" (\"UserId\")");
    }
}
