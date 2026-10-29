using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Admin'in yüklediği resmi öğrenci/staj veri setini (CSV veya JSON) okur ve
/// mükerrer kayıtları atlayarak OfficialStudentRecords tablosuna aktarır.
/// </summary>
public class DatasetImportService
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024;

    private enum Field
    {
        StudentNumber,
        IdentityNumber,
        FullName,
        Department,
        AcademicYear,
        InternshipStatus
    }

    // Sütun / alan adları normalize edilerek eşleştirilir (küçük harf, Türkçe karakter ve boşluk yok).
    private static readonly Dictionary<string, Field> FieldAliases = BuildAliases(new Dictionary<Field, string[]>
    {
        [Field.StudentNumber] = new[] { "StudentNumber", "StudentNo", "Öğrenci No", "Öğrenci Numarası", "Numara", "Okul No" },
        [Field.IdentityNumber] = new[]
        {
            "IdentityNumber", "TC", "TCKN", "TC No", "TC Kimlik No", "Kimlik No", "Kimlik Numarası",
            "Yabancı Kimlik No", "TC/Yabancı Kimlik No"
        },
        [Field.FullName] = new[] { "FullName", "Name", "Ad Soyad", "Adı Soyadı", "Ad Soyadı" },
        [Field.Department] = new[] { "Department", "Bölüm", "Bölümü", "Program" },
        [Field.AcademicYear] = new[] { "AcademicYear", "Öğretim Yılı", "Eğitim Öğretim Yılı", "Akademik Yıl", "Yıl" },
        [Field.InternshipStatus] = new[] { "InternshipStatus", "Status", "Staj Durumu", "Durum" }
    });

    private static readonly Dictionary<string, OfficialInternshipStatus> StatusAliases =
        BuildAliases(new Dictionary<OfficialInternshipStatus, string[]>
    {
        [OfficialInternshipStatus.NotStarted] = new[] { "NotStarted", "Başlamadı", "Yapılmadı", "Yapmadı", "Beklemede" },
        [OfficialInternshipStatus.InProgress] = new[] { "InProgress", "Devam Ediyor", "Devam", "Sürüyor" },
        [OfficialInternshipStatus.Completed] = new[] { "Completed", "Tamamlandı", "Tamam", "Başarılı" },
        [OfficialInternshipStatus.Failed] = new[] { "Failed", "Başarısız", "Kaldı" }
    });

    private readonly DatabaseManager _database;

    public DatasetImportService(DatabaseManager database)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
    }

    /// <summary>
    /// Dosyayı okuyup kayıtları veritabanına aktarır. Yalnızca Admin çalıştırabilir.
    /// Dosya içinde veya veritabanında zaten bulunan (aynı öğretim yılında aynı öğrenci no
    /// ya da kimlik no) kayıtlar atlanır; hatalı satırlar raporlanır, diğerleri aktarılır.
    /// </summary>
    public DatasetImportResult ImportDatasetToDatabase(string filePath, User currentUser)
    {
        EnsureAdmin(currentUser);

        var parsed = Parse(filePath);

        var seenStudents = new HashSet<(string, string)>();
        var seenIdentities = new HashSet<(string, string)>();
        var unique = new List<OfficialStudentRecord>();
        int duplicatesInFile = 0;

        foreach (var record in parsed.Records)
        {
            // & (kısa devresiz) ile iki anahtar da her zaman kaydedilir.
            bool isNew = seenStudents.Add(record.Key) & seenIdentities.Add((record.IdentityNumber, record.AcademicYear));
            if (isNew)
                unique.Add(record);
            else
                duplicatesInFile++;
        }

        int imported = 0;
        int duplicatesInDatabase = 0;
        DateTime now = DateTime.UtcNow;

        using var connection = _database.GetConnection();
        using var transaction = connection.BeginTransaction();

        EnsureAdminInDatabase(connection, transaction, currentUser.Id);

        using var insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = """
            INSERT OR IGNORE INTO OfficialStudentRecords
                (StudentNumber, IdentityNumber, FullName, Department, AcademicYear, InternshipStatus, ImportedAt, ImportedBy)
            VALUES ($studentNumber, $identity, $fullName, $department, $year, $status, $importedAt, $importedBy);
            """;
        var pStudent = insert.Parameters.Add("$studentNumber", SqliteType.Text);
        var pIdentity = insert.Parameters.Add("$identity", SqliteType.Text);
        var pFullName = insert.Parameters.Add("$fullName", SqliteType.Text);
        var pDepartment = insert.Parameters.Add("$department", SqliteType.Text);
        var pYear = insert.Parameters.Add("$year", SqliteType.Text);
        var pStatus = insert.Parameters.Add("$status", SqliteType.Text);
        insert.Parameters.AddWithValue("$importedAt", now.ToString("o", CultureInfo.InvariantCulture));
        insert.Parameters.AddWithValue("$importedBy", currentUser.Id);

        foreach (var record in unique)
        {
            pStudent.Value = record.StudentNumber;
            pIdentity.Value = record.IdentityNumber;
            pFullName.Value = record.FullName;
            pDepartment.Value = record.Department;
            pYear.Value = record.AcademicYear;
            pStatus.Value = record.InternshipStatus.ToString();

            // INSERT OR IGNORE: UNIQUE kısıtına takılan (zaten var olan) kayıt 0 satır etkiler.
            if (insert.ExecuteNonQuery() == 0)
            {
                duplicatesInDatabase++;
                continue;
            }

            record.ImportedAt = now;
            record.ImportedByUserId = currentUser.Id;
            imported++;
        }

        transaction.Commit();

        return new DatasetImportResult(parsed.TotalRows, imported, duplicatesInFile, duplicatesInDatabase, parsed.Errors);
    }

    /// <summary>
    /// Uzantıya göre ParseCsv ya da ParseJson çağırır.
    /// </summary>
    public DatasetParseResult Parse(string filePath)
    {
        var file = new FileInfo(filePath ?? throw new ArgumentNullException(nameof(filePath)));
        if (!file.Exists)
            throw new FileNotFoundException("Veri seti dosyası bulunamadı.", filePath);
        if (file.Length == 0 || file.Length > MaxFileSizeBytes)
            throw new InvalidDataException($"Veri seti dosyası boş olamaz ve en fazla {MaxFileSizeBytes / 1024 / 1024} MB olabilir.");

        return file.Extension.ToLowerInvariant() switch
        {
            ".csv" => ParseCsv(filePath),
            ".json" => ParseJson(filePath),
            _ => throw new InvalidDataException("Yalnızca .csv ve .json veri setleri desteklenir.")
        };
    }

    /// <summary>
    /// İlk satırı başlık olan CSV dosyasını okur. Ayırıcı virgül ya da noktalı virgül olabilir
    /// (Türkçe Excel noktalı virgül kullanır); tırnak içindeki alanlar desteklenir.
    /// </summary>
    public DatasetParseResult ParseCsv(string filePath)
    {
        var lines = File.ReadAllLines(filePath, Encoding.UTF8);
        var records = new List<OfficialStudentRecord>();
        var errors = new List<DatasetRowError>();

        int headerIndex = Array.FindIndex(lines, l => !string.IsNullOrWhiteSpace(l));
        if (headerIndex < 0)
            throw new InvalidDataException("CSV dosyası boş.");

        string headerLine = lines[headerIndex];
        char delimiter = headerLine.Count(c => c == ';') > headerLine.Count(c => c == ',') ? ';' : ',';

        var headers = SplitCsvLine(headerLine, delimiter);
        var columns = new Dictionary<Field, int>();
        for (int i = 0; i < headers.Count; i++)
        {
            if (FieldAliases.TryGetValue(Normalize(headers[i]), out var field))
                columns.TryAdd(field, i);
        }

        var missing = Enum.GetValues<Field>().Where(f => !columns.ContainsKey(f)).ToList();
        if (missing.Count > 0)
            throw new InvalidDataException($"CSV başlığında eksik sütun(lar): {string.Join(", ", missing)}");

        for (int i = headerIndex + 1; i < lines.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
                continue;

            int rowNumber = i + 1;
            var cells = SplitCsvLine(lines[i], delimiter);
            if (cells.Count != headers.Count)
            {
                errors.Add(new DatasetRowError(rowNumber, $"{headers.Count} sütun bekleniyordu, {cells.Count} bulundu."));
                continue;
            }

            var values = columns.ToDictionary(c => c.Key, c => (string?)cells[c.Value]);
            AddRecord(values, rowNumber, records, errors);
        }

        return new DatasetParseResult(records, errors);
    }

    /// <summary>
    /// Kökü kayıt dizisi olan JSON dosyasını okur: [ { "StudentNumber": "...", ... }, ... ].
    /// Alan adları CSV başlıklarıyla aynı şekilde (Türkçe karşılıkları dahil) eşleştirilir.
    /// </summary>
    public DatasetParseResult ParseJson(string filePath)
    {
        var records = new List<OfficialStudentRecord>();
        var errors = new List<DatasetRowError>();

        using var document = JsonDocument.Parse(File.ReadAllText(filePath, Encoding.UTF8), new JsonDocumentOptions
        {
            AllowTrailingCommas = true,
            CommentHandling = JsonCommentHandling.Skip
        });

        if (document.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("JSON veri setinin kökü kayıt dizisi ( [ ... ] ) olmalıdır.");

        int rowNumber = 0;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            rowNumber++;
            if (item.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new DatasetRowError(rowNumber, "Kayıt bir JSON nesnesi değil."));
                continue;
            }

            var values = new Dictionary<Field, string?>();
            foreach (var property in item.EnumerateObject())
            {
                if (!FieldAliases.TryGetValue(Normalize(property.Name), out var field))
                    continue;

                values[field] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    _ => null
                };
            }

            AddRecord(values, rowNumber, records, errors);
        }

        return new DatasetParseResult(records, errors);
    }

    private static void AddRecord(
        Dictionary<Field, string?> values, int rowNumber, List<OfficialStudentRecord> records, List<DatasetRowError> errors)
    {
        var missing = Enum.GetValues<Field>().Where(f => string.IsNullOrWhiteSpace(values.GetValueOrDefault(f))).ToList();
        if (missing.Count > 0)
        {
            errors.Add(new DatasetRowError(rowNumber, $"Eksik alan(lar): {string.Join(", ", missing)}"));
            return;
        }

        if (!StatusAliases.TryGetValue(Normalize(values[Field.InternshipStatus]!), out var status))
        {
            errors.Add(new DatasetRowError(rowNumber, $"Bilinmeyen staj durumu: {values[Field.InternshipStatus]}"));
            return;
        }

        try
        {
            records.Add(new OfficialStudentRecord(
                values[Field.StudentNumber]!,
                values[Field.IdentityNumber]!,
                values[Field.FullName]!,
                values[Field.Department]!,
                values[Field.AcademicYear]!,
                status));
        }
        catch (ArgumentException ex)
        {
            errors.Add(new DatasetRowError(rowNumber, ex.Message.Split(" (Parameter")[0]));
        }
    }

    private static void EnsureAdmin(User? currentUser)
    {
        if (currentUser is null)
            throw new LoginRequiredException("Veri seti yüklemek için giriş yapmalısınız.");

        if (!currentUser.HasPermission(Permission.ImportDataset))
            throw new ForbiddenAccessException(
                $"403 Yetkisiz Erişim: Veri seti yalnızca {RolePermissions.RoleAdmin} tarafından yüklenebilir.");
    }

    // Bellekte oluşturulmuş bir Admin nesnesiyle yetki atlatılamasın diye rol veritabanından da doğrulanır.
    private static void EnsureAdminInDatabase(SqliteConnection connection, SqliteTransaction transaction, int userId)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Role FROM Users WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", userId);

        if (command.ExecuteScalar() as string != UserRole.Admin.ToString())
            throw new ForbiddenAccessException(
                $"403 Yetkisiz Erişim: Veri seti yalnızca kayıtlı bir {RolePermissions.RoleAdmin} tarafından yüklenebilir.");
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var cells = new List<string>();
        var current = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    current.Append(c);
                }
            }
            else if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == delimiter)
            {
                cells.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        cells.Add(current.ToString().Trim());
        return cells;
    }

    /// <summary>
    /// "Öğrenci No" -> "ogrencino": küçük harf, Türkçe karakterler sadeleştirilmiş, yalnızca harf ve rakam.
    /// </summary>
    private static string Normalize(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (char raw in value.Trim().ToLowerInvariant())
        {
            char c = raw switch
            {
                'ç' => 'c',
                'ğ' => 'g',
                'ı' => 'i',
                'ö' => 'o',
                'ş' => 's',
                'ü' => 'u',
                _ => raw
            };
            if (char.IsAsciiLetterOrDigit(c))
                builder.Append(c);
        }
        return builder.ToString();
    }

    private static Dictionary<string, T> BuildAliases<T>(Dictionary<T, string[]> source) where T : notnull =>
        source.SelectMany(pair => pair.Value.Select(alias => (Alias: Normalize(alias), Value: pair.Key)))
              .ToDictionary(x => x.Alias, x => x.Value);
}
