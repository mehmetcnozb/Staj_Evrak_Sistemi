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
    /// Tablolar yoksa oluşturur, eksik sütunları ekler. Birden fazla kez çağrılması güvenlidir.
    /// </summary>
    /// <exception cref="InvalidOperationException">Veritabanı dosyası projenin eski bir şemasıyla oluşturulmuş.</exception>
    public void InitializeDatabase()
    {
        using var connection = GetConnection();
        using var transaction = connection.BeginTransaction();

        EnsureNotLegacySchema(connection, transaction);

        foreach (var sql in new[]
                 {
                     CreateUsersTable, CreateInternshipsTable, CreateDocumentsTable, CreateOfficialStudentRecordsTable,
                     CreateStatusTransitionLogsTable, CreateStatusTransitionLogsIndex,
                     CreateFailedLoginLogsTable, CreateFailedLoginLogsIndex
                 })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        // Eski veritabanı dosyalarına sonradan eklenen sütunlar (yeni dosyalarda zaten vardır).
        EnsureColumn(connection, transaction, "Users", "FailedLoginAttempts", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "Users", "LockoutEnd", "TEXT");

        EnsureColumn(connection, transaction, "Internships", "Department", "TEXT");
        EnsureColumn(connection, transaction, "Internships", "Ek16Verified", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "Internships", "Ek16VerifiedAt", "TEXT");
        EnsureColumn(connection, transaction, "Internships", "Semester", "TEXT");
        EnsureColumn(connection, transaction, "Internships", "Address", "TEXT");
        EnsureColumn(connection, transaction, "Internships", "Phone", "TEXT");
        EnsureColumn(connection, transaction, "Internships", "Email", "TEXT");
        EnsureColumn(connection, transaction, "Internships", "Fax", "TEXT");
        EnsureColumn(connection, transaction, "Internships", "WorkDays", "TEXT");
        EnsureColumn(connection, transaction, "Internships", "EngineerCount", "INTEGER NOT NULL DEFAULT 0");
        EnsureColumn(connection, transaction, "Internships", "CreatedAt", "TEXT");

        transaction.Commit();
    }

    // Bu markerlardan biri tablo tanımında geçiyorsa dosya eski şemayla oluşturulmuştur. SQLite'ta CHECK kısıtı
    // sonradan değiştirilemediği için sessizce devam etmek yerine kullanıcıyı açık bir mesajla uyarırız.
    private static void EnsureNotLegacySchema(SqliteConnection connection, SqliteTransaction transaction)
    {
        var legacyMarkers = new (string Table, string Marker)[]
        {
            ("Users", "'Commission'"),
            ("Internships", "'RevisionRequired'"),
            ("OfficialStudentRecords", "InternshipStatus")
        };

        foreach (var (table, marker) in legacyMarkers)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = $name;";
            command.Parameters.AddWithValue("$name", table);

            if (command.ExecuteScalar() is string sql && sql.Contains(marker, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    $"Veritabanı dosyası eski şemayla oluşturulmuş ({table} tablosu güncel değil). " +
                    "Test verisi içeren geliştirme dosyasını (staj_evrak.db) silip uygulamayı yeniden başlatın.");
        }
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
            Role            TEXT    NOT NULL CHECK (Role IN ('Student', 'Admin')),
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
                CHECK (Status IN ('Uploaded', 'UnderReview', 'FaultyOrMissing', 'Approved', 'Completed')),
            GoogleFormUrl   TEXT,
            Department      TEXT,
            Ek16Verified    INTEGER NOT NULL DEFAULT 0 CHECK (Ek16Verified IN (0, 1)),
            Ek16VerifiedAt  TEXT,
            Semester        TEXT,
            Address         TEXT,
            Phone           TEXT,
            Email           TEXT,
            Fax             TEXT,
            WorkDays        TEXT,
            EngineerCount   INTEGER NOT NULL DEFAULT 0,
            CreatedAt       TEXT,
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

    // Admin'in yüklediği resmi öğrenci veri seti (şartname 4.3 sütunları).
    // Bir öğrencinin bir öğretim yılında yalnızca bir kaydı olabilir.
    private const string CreateOfficialStudentRecordsTable = """
        CREATE TABLE IF NOT EXISTS OfficialStudentRecords (
            Id                     INTEGER PRIMARY KEY AUTOINCREMENT,
            StudentNumber          TEXT    NOT NULL,
            IdentityNumber         TEXT    NOT NULL,
            FullName               TEXT    NOT NULL,
            Nationality            TEXT    NOT NULL,
            Department             TEXT    NOT NULL,
            AcademicYear           TEXT    NOT NULL,
            BirthDate              TEXT,
            Address                TEXT,
            PreviousInternshipDays INTEGER,
            Email                  TEXT,
            Phone                  TEXT,
            HealthAidStatus        TEXT,
            ImportedAt             TEXT    NOT NULL DEFAULT (datetime('now')),
            ImportedBy             INTEGER,
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

    // Şartname 4.1: başarısız giriş denemeleri loglanır. Parola hiçbir zaman loga yazılmaz.
    private const string CreateFailedLoginLogsTable = """
        CREATE TABLE IF NOT EXISTS FailedLoginLogs (
            Id           INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId       INTEGER,
            Identifier   TEXT    NOT NULL,
            Reason       TEXT    NOT NULL CHECK (Reason IN ('UnknownUser', 'WrongPassword', 'LockedOut')),
            AttemptedAt  TEXT    NOT NULL,
            FOREIGN KEY (UserId) REFERENCES Users(Id) ON DELETE SET NULL
        );
        """;

    private const string CreateFailedLoginLogsIndex =
        "CREATE INDEX IF NOT EXISTS IX_FailedLoginLogs_UserId ON FailedLoginLogs (UserId);";
}
