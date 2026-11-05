using CHTLManager.BLL;
using CHTLManager.DAL;
using Microsoft.Data.Sqlite;

// AppDbContext.DbPath là static nên các test không được chạy song song.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace CHTLManager.Tests;

/// <summary>Database tạm cho một test: trỏ AppDbContext.DbPath sang thư mục riêng, dọn khi xong.</summary>
public sealed class TestDb : IDisposable
{
    private readonly string _previousPath = AppDbContext.DbPath;
    public string Directory { get; }
    public string Path { get; }

    public TestDb()
    {
        Directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "chtl_test_" + Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Path = System.IO.Path.Combine(Directory, "test.db");
        AppDbContext.DbPath = Path;
    }

    public ProductService Products { get; } = new();
    public OrderService Orders { get; } = new();
    public UserService Users { get; } = new();

    /// <summary>Tạo database mới như lần đầu mở app.</summary>
    public TestDb Initialized(bool demo = false)
    {
        DatabaseUpgrader.Upgrade();
        DbInitializer.Initialize(demo);
        return this;
    }

    /// <summary>Tạo file database như bản cũ (trước khi có Migrations), bằng schema đã đóng băng.</summary>
    public void CreateLegacyFile()
    {
        using var connection = new SqliteConnection($"Data Source={Path}");
        connection.Open();
        Execute(connection, File.ReadAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "LegacySchema.sql")));
        Execute(connection, """
            INSERT INTO Users (Id, Username, PasswordHash, FullName, Role, IsActive) VALUES (1, 'chu', 'x', 'Chủ quán', 'Admin', 1);
            INSERT INTO Orders (Id, Code, CreatedAt, CashierId, Total, CustomerPaid, "Change") VALUES (1, 'HD20260101-0001', '2026-01-01 08:00:00', 1, 1000, 1000, 0);
            """);
    }

    public void ExecuteSql(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={Path}");
        connection.Open();
        Execute(connection, sql);
    }

    public long Scalar(string sql)
    {
        using var connection = new SqliteConnection($"Data Source={Path}");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)command.ExecuteScalar()!;
    }

    private static void Execute(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        AppDbContext.DbPath = _previousPath;
        try { System.IO.Directory.Delete(Directory, recursive: true); }
        catch (IOException) { /* file tạm, hệ điều hành sẽ dọn sau */ }
    }
}
