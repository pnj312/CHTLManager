using CHTLManager.BLL;
using Microsoft.Data.Sqlite;

namespace CHTLManager.Tests;

public class DatabaseUpgraderTests
{
    private const string HasBaseline = "SELECT COUNT(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%_InitialCreate'";

    [Fact]
    public void NewFile_CreatesSchema_NoBackup()
    {
        using var db = new TestDb();

        var result = DatabaseUpgrader.Upgrade();

        Assert.True(result.IsNew);
        Assert.False(result.WasLegacy);
        Assert.Null(result.BackupPath);
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM Users"));
        Assert.Equal(1, db.Scalar(HasBaseline));
    }

    [Fact]
    public void LegacyFile_KeepsData_MakesBackup_MarksBaseline()
    {
        using var db = new TestDb();
        db.CreateLegacyFile();

        var result = DatabaseUpgrader.Upgrade();

        Assert.True(result.WasLegacy);
        Assert.False(result.IsNew);
        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM Orders"));
        Assert.Equal(1, db.Scalar(HasBaseline));
        Assert.NotNull(result.BackupPath);
        Assert.True(File.Exists(result.BackupPath));

        using var backup = new SqliteConnection($"Data Source={result.BackupPath};Mode=ReadOnly");
        backup.Open();
        using var command = backup.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Orders";
        Assert.Equal(1L, command.ExecuteScalar());
    }

    [Fact]
    public void LegacyFile_MissingStockMovementColumns_IsRepaired()
    {
        using var db = new TestDb();
        db.CreateLegacyFile();
        db.ExecuteSql("""
            ALTER TABLE StockMovements DROP COLUMN Supplier;
            ALTER TABLE StockMovements DROP COLUMN InvoiceNo;
            """);

        DatabaseUpgrader.Upgrade();

        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM pragma_table_info('StockMovements') WHERE name = 'Supplier'"));
        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM pragma_table_info('StockMovements') WHERE name = 'InvoiceNo'"));
    }

    [Fact]
    public void LegacyFile_MissingStockMovementsTable_IsRepaired()
    {
        using var db = new TestDb();
        db.CreateLegacyFile();
        db.ExecuteSql("DROP TABLE StockMovements");

        DatabaseUpgrader.Upgrade();
        DbInitializer.Initialize(seedDemoData: false);

        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM StockMovements"));
    }

    [Fact]
    public void UpToDateFile_NoBackup_NoError()
    {
        using var db = new TestDb();
        DatabaseUpgrader.Upgrade();

        var second = DatabaseUpgrader.Upgrade();

        Assert.False(second.IsNew);
        Assert.False(second.WasLegacy);
        Assert.Null(second.BackupPath);
        Assert.Empty(Directory.GetFiles(db.Directory, "*.backup-*"));
    }

    [Fact]
    public void BrokenFile_Throws_LeavesFileUnchanged()
    {
        using var db = new TestDb();
        byte[] garbage = Enumerable.Range(0, 5000).Select(i => (byte)(i % 251)).ToArray();
        File.WriteAllBytes(db.Path, garbage);

        Assert.Throws<BusinessException>(() => DatabaseUpgrader.Upgrade());

        SqliteConnection.ClearAllPools();
        Assert.Equal(garbage, File.ReadAllBytes(db.Path));
    }

    [Fact]
    public void Backups_KeepOnlyFiveNewest()
    {
        using var db = new TestDb();
        db.CreateLegacyFile();
        for (int i = 0; i < 7; i++)
            File.WriteAllText(Path.Combine(db.Directory, $"test.backup-2020010{i + 1}-000000.db"), "cũ");

        var result = DatabaseUpgrader.Upgrade();

        var backups = Directory.GetFiles(db.Directory, "test.backup-*.db");
        Assert.Equal(5, backups.Length);
        Assert.Contains(result.BackupPath, backups);
        Assert.DoesNotContain(backups, b => b.EndsWith("20200101-000000.db"));
    }

    [Fact]
    public void Initialize_WithoutDemo_OnlyAdminAccount()
    {
        using var db = new TestDb();
        db.Initialized(demo: false);

        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM Users"));
        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM Users WHERE Role = 'Admin'"));
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM Products"));
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM Orders"));
    }

    [Fact]
    public void Initialize_WithDemo_HasSampleData()
    {
        using var db = new TestDb();
        db.Initialized(demo: true);

        Assert.Equal(2, db.Scalar("SELECT COUNT(*) FROM Users"));
        Assert.Equal(12, db.Scalar("SELECT COUNT(*) FROM Products"));
        Assert.True(db.Scalar("SELECT COUNT(*) FROM Orders") > 0);
    }

    [Fact]
    public void Initialize_WithDemo_OnStoreWithData_AddsNothing()
    {
        using var db = new TestDb();
        db.Initialized(demo: false);
        var admin = db.Users.Login("admin", "admin123")!;
        db.Products.Add(new CHTLManager.DAL.Entities.Product { Barcode = "1", Name = "Thật", Price = 1000, Stock = 1 }, admin.Id);

        DbInitializer.Initialize(seedDemoData: true);

        Assert.Equal(1, db.Scalar("SELECT COUNT(*) FROM Products"));
        Assert.Equal(0, db.Scalar("SELECT COUNT(*) FROM Orders"));
    }
}
