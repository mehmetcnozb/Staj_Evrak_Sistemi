using Microsoft.Data.Sqlite;

namespace StajEvrakSistemi.Data;

/// <summary>
/// SQLite bağlantısını yönetir ve uygulama ilk çalıştığında temel tabloları oluşturur.
/// </summary>
public class DatabaseManager
{
    public const string DefaultDatabaseFile = "staj_evrak.db";

    private readonly string _connectionString;

    public DatabaseManager(string databaseFile = DefaultDatabaseFile)
    {
        if (string.IsNullOrWhiteSpace(databaseFile))
            throw new ArgumentException("Veritabanı dosya yolu boş olamaz.", nameof(databaseFile));

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true
        }.ToString();
    }

    /// <summary>
    /// Açılmış yeni bir bağlantı döndürür. Çağıran taraf <c>using</c> ile kapatmalıdır.
    /// </summary>
    public SqliteConnection GetConnection()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        return connection;
    }

    /// <summary>
    /// Tablolar yoksa oluşturur. Birden fazla kez çağrılması güvenlidir.
    /// </summary>
    public void InitializeDatabase()
    {
        using var connection = GetConnection();
        using var transaction = connection.BeginTransaction();

        foreach (var sql in new[]
                 {
                     CreateUsersTable, CreateInternshipsTable, CreateDocumentsTable, CreateOfficialStudentRecordsTable,
                     CreateStatusTransitionLogsTable, CreateStatusTransitionLogsIndex
                 })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        // CORE-1'de oluşturulmuş eski veritabanlarına kilit sütunlarını ekler.
        EnsureColumn(connection, transaction, "Users", "FailedLoginAttempts", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "Users", "LockoutEnd", "TEXT");

        // CORE-4 Ek-16 karşılaştırması için başvuruya eklenen sütunlar.
        EnsureColumn(connection, transaction, "Internships", "Department", "TEXT");
        EnsureColumn(connection, transaction, "Internships", "Ek16Verified", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "Internships", "Ek16VerifiedAt", "TEXT");

        // CORE-5 öncesi başvurular 'Pending' statüsüyle oluşuyordu; yeni akışta karşılığı 'Uploaded'.
        using (var migrateStatus = connection.CreateCommand())
        {
            migrateStatus.Transaction = transaction;
            migrateStatus.CommandText = "UPDATE Internships SET Status = 'Uploaded' WHERE Status = 'Pending';";
            migrateStatus.ExecuteNonQuery();
        }

        transaction.Commit();
    }

    private static void EnsureColumn(
        SqliteConnection connection, SqliteTransaction transaction, string table, string column, string definition)
    {
        using (var check = connection.CreateCommand())
        {
            check.Transaction = transaction;
            check.CommandText = $"PRAGMA table_info({table});";

            using var reader = check.ExecuteReader();
            while (reader.Read())
            {
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                    return;
            }
        }

        using var alter = connection.CreateCommand();
        alter.Transaction = transaction;
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition};";
        alter.ExecuteNonQuery();
    }

    private const string CreateUsersTable = """
        CREATE TABLE IF NOT EXISTS Users (
            Id              INTEGER PRIMARY KEY AUTOINCREMENT,
            IdentityNumber  TEXT    NOT NULL UNIQUE,
            StudentNumber   TEXT    UNIQUE,
            FullName        TEXT    NOT NULL,
            Email           TEXT    NOT NULL UNIQUE,
            Phone           TEXT,
            PasswordHash    TEXT    NOT NULL,
            Role            TEXT    NOT NULL CHECK (Role IN ('Student', 'Admin', 'Commission')),
            CreatedAt       TEXT    NOT NULL DEFAULT (datetime('now')),
            FailedLoginAttempts INTEGER NOT NULL DEFAULT 0,
            LockoutEnd      TEXT
        );
        """;

    private const string CreateInternshipsTable = """
        CREATE TABLE IF NOT EXISTS Internships (
            Id              INTEGER PRIMARY KEY AUTOINCREMENT,
            StudentId       INTEGER NOT NULL,
            AcademicYear    TEXT    NOT NULL,
            StartDate       TEXT    NOT NULL,
            EndDate         TEXT    NOT NULL,
            NetWorkDays     INTEGER NOT NULL DEFAULT 0,
            CompanyName     TEXT    NOT NULL,
            HasEngineer     INTEGER NOT NULL DEFAULT 0 CHECK (HasEngineer IN (0, 1)),
            Status          TEXT    NOT NULL DEFAULT 'Uploaded'
                CHECK (Status IN ('Uploaded', 'UnderReview', 'RevisionRequired', 'Rejected', 'Completed')),
            GoogleFormUrl   TEXT,
            Department      TEXT,
            Ek16Verified    INTEGER NOT NULL DEFAULT 0 CHECK (Ek16Verified IN (0, 1)),
            Ek16VerifiedAt  TEXT,
            FOREIGN KEY (StudentId) REFERENCES Users(Id) ON DELETE CASCADE
        );
        """;

    private const string CreateDocumentsTable = """
        CREATE TABLE IF NOT EXISTS Documents (
            Id              INTEGER PRIMARY KEY AUTOINCREMENT,
            InternshipId    INTEGER NOT NULL,
            DocumentType    TEXT    NOT NULL,
            FilePath        TEXT    NOT NULL,
            FileFormat      TEXT    NOT NULL,
            IsApproved      INTEGER NOT NULL DEFAULT 0 CHECK (IsApproved IN (0, 1)),
            RejectionReason TEXT,
            UploadedAt      TEXT    NOT NULL DEFAULT (datetime('now')),
            FOREIGN KEY (InternshipId) REFERENCES Internships(Id) ON DELETE CASCADE
        );
        """;

    // Admin'in yüklediği resmi öğrenci/staj veri seti.
    // Bir öğrencinin bir öğretim yılında yalnızca bir kaydı olabilir.
    private const string CreateOfficialStudentRecordsTable = """
        CREATE TABLE IF NOT EXISTS OfficialStudentRecords (
            Id               INTEGER PRIMARY KEY AUTOINCREMENT,
            StudentNumber    TEXT    NOT NULL,
            IdentityNumber   TEXT    NOT NULL,
            FullName         TEXT    NOT NULL,
            Department       TEXT    NOT NULL,
            AcademicYear     TEXT    NOT NULL,
            InternshipStatus TEXT    NOT NULL
                CHECK (InternshipStatus IN ('NotStarted', 'InProgress', 'Completed', 'Failed')),
            ImportedAt       TEXT    NOT NULL DEFAULT (datetime('now')),
            ImportedBy       INTEGER,
            UNIQUE (StudentNumber, AcademicYear),
            UNIQUE (IdentityNumber, AcademicYear),
            FOREIGN KEY (ImportedBy) REFERENCES Users(Id) ON DELETE SET NULL
        );
        """;

    // Başvuru statü değişim geçmişi; her geçiş bir satır olarak eklenir, güncellenmez.
    private const string CreateStatusTransitionLogsTable = """
        CREATE TABLE IF NOT EXISTS StatusTransitionLogs (
            Id               INTEGER PRIMARY KEY AUTOINCREMENT,
            ApplicationId    INTEGER NOT NULL,
            OldStatus        TEXT    NOT NULL,
            NewStatus        TEXT    NOT NULL,
            ChangedByUserId  INTEGER,
            ChangedAt        TEXT    NOT NULL,
            Note             TEXT,
            FOREIGN KEY (ApplicationId) REFERENCES Internships(Id) ON DELETE CASCADE,
            FOREIGN KEY (ChangedByUserId) REFERENCES Users(Id) ON DELETE SET NULL
        );
        """;

    private const string CreateStatusTransitionLogsIndex =
        "CREATE INDEX IF NOT EXISTS IX_StatusTransitionLogs_ApplicationId ON StatusTransitionLogs (ApplicationId);";
}
