using System.Globalization;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Öğrencinin sisteme girdiği staj başvuru bilgilerini kaydeder, günceller ve listeler.
/// Güncelleme/silme kuralları (Tamamlandı'da öğrenci için kilit vb.) StatusWorkflowEngine'den gelir.
/// Alan biçimi doğrulamaları (adres, telefon, tarih/gün hesabı...) bu serviste değil, ayrı doğrulama katmanındadır.
/// </summary>
public class InternshipService
{
    private const string DateFormat = "yyyy-MM-dd";

    private const string SelectColumns = """
        Id, StudentId, AcademicYear, Semester, StartDate, EndDate, CompanyName, Department, Address, Phone, Email, Fax,
        WorkDays, EngineerCount, GoogleFormUrl, Status, Ek16Verified, CreatedAt
        """;

    private readonly DatabaseManager _database;
    private readonly StatusWorkflowEngine _engine;
    private readonly Func<DateTime> _utcNow;

    public InternshipService(DatabaseManager database, StatusWorkflowEngine engine, Func<DateTime>? utcNow = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Öğrencinin girdiği başvuru bilgilerini kaydeder ve oluşan başvuru Id'sini döndürür.
    /// Başvuru "Yüklendi" statüsüyle başlar. Yalnızca öğrenci kendi adına başvuru oluşturabilir.
    /// </summary>
    public int CreateApplication(User student, InternshipApplication application)
    {
        if (student is null)
            throw new LoginRequiredException("Başvuru oluşturmak için giriş yapmalısınız.");
        ArgumentNullException.ThrowIfNull(application);

        if (!student.HasPermission(Permission.UploadDocument))
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Staj başvurusunu yalnızca öğrenci oluşturabilir.");

        ValidateBasics(application);

        using var connection = _database.GetConnection();
        UserGuard.EnsureUserInDatabase(connection, null, student);

        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Internships
                (StudentId, AcademicYear, Semester, StartDate, EndDate, CompanyName, Department, Address, Phone, Email, Fax,
                 WorkDays, EngineerCount, HasEngineer, GoogleFormUrl, Status, CreatedAt)
            VALUES
                ($studentId, $year, $semester, $start, $end, $company, $department, $address, $phone, $email, $fax,
                 $workDays, $engineers, $hasEngineer, $googleForm, 'Uploaded', $createdAt);
            SELECT last_insert_rowid();
            """;
        BindApplication(command, application);
        command.Parameters.AddWithValue("$studentId", student.Id);
        command.Parameters.AddWithValue("$createdAt", _utcNow().ToString("o", CultureInfo.InvariantCulture));

        application.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        application.StudentId = student.Id;
        application.Status = ApplicationStatus.Uploaded;
        return application.Id;
    }

    /// <summary>
    /// Başvuru bilgilerini günceller. Öğrenci yalnızca kendi başvurusunu ve yalnızca Yüklendi / Hatalı-Eksik
    /// statüsünde güncelleyebilir; Admin her statüde güncelleyebilir. Bilgiler değiştiği için Ek-16
    /// doğrulaması sıfırlanır ve yeniden yapılmalıdır.
    /// </summary>
    public void UpdateApplication(User user, InternshipApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        _engine.ValidateUpdateOperation(application.Id, user);
        ValidateBasics(application);

        using var connection = _database.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE Internships SET
                AcademicYear = $year, Semester = $semester, StartDate = $start, EndDate = $end, CompanyName = $company,
                Department = $department, Address = $address, Phone = $phone, Email = $email, Fax = $fax,
                WorkDays = $workDays, EngineerCount = $engineers, HasEngineer = $hasEngineer, GoogleFormUrl = $googleForm,
                Ek16Verified = 0, Ek16VerifiedAt = NULL
            WHERE Id = $id;
            """;
        BindApplication(command, application);
        command.Parameters.AddWithValue("$id", application.Id);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Başvuruyu (ve bağlı evraklarını) siler. Güncellemeyle aynı kurallara tabidir.
    /// </summary>
    public void DeleteApplication(User user, int applicationId)
    {
        _engine.ValidateDeleteOperation(applicationId, user);

        using var connection = _database.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Internships WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", applicationId);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// Tek bir başvuruyu getirir. Öğrenci yalnızca kendi başvurusunu, Admin hepsini görebilir.
    /// </summary>
    public InternshipApplication GetApplication(User user, int applicationId)
    {
        if (user is null)
            throw new LoginRequiredException("Başvuruyu görmek için giriş yapmalısınız.");

        using var connection = _database.GetConnection();
        UserGuard.EnsureUserInDatabase(connection, null, user);

        var application = Query(connection, "WHERE Id = $id", ("$id", applicationId)).SingleOrDefault()
            ?? throw new KeyNotFoundException($"#{applicationId} numaralı staj başvurusu bulunamadı.");

        if (!user.HasPermission(Permission.ViewAllInternships) && application.StudentId != user.Id)
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Yalnızca kendi staj kayıtlarınızı görüntüleyebilirsiniz.");

        return application;
    }

    /// <summary>
    /// Öğrencinin kendi başvuruları.
    /// </summary>
    public IReadOnlyList<InternshipApplication> GetMyApplications(User student)
    {
        if (student is null)
            throw new LoginRequiredException("Başvurularınızı görmek için giriş yapmalısınız.");
        if (!student.HasPermission(Permission.ViewOwnInternships))
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Bu liste yalnızca öğrenciler içindir.");

        using var connection = _database.GetConnection();
        UserGuard.EnsureUserInDatabase(connection, null, student);
        return Query(connection, "WHERE StudentId = $studentId ORDER BY Id", ("$studentId", student.Id));
    }

    /// <summary>
    /// Tüm başvurular (isteğe bağlı statü filtresiyle). Yalnızca Admin.
    /// </summary>
    public IReadOnlyList<InternshipApplication> GetApplications(User admin, ApplicationStatus? statusFilter = null)
    {
        if (admin is null)
            throw new LoginRequiredException("Başvuruları görmek için giriş yapmalısınız.");
        if (!admin.HasPermission(Permission.ViewAllInternships))
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Tüm başvuruları yalnızca yönetici görebilir.");

        using var connection = _database.GetConnection();
        UserGuard.EnsureUserInDatabase(connection, null, admin);

        return statusFilter is null
            ? Query(connection, "ORDER BY Id")
            : Query(connection, "WHERE Status = $status ORDER BY Id", ("$status", statusFilter.Value.ToString()));
    }

    /// <summary>
    /// Admin sayfasındaki "Tamamlandı" sekmesi: tamamlanan başvurular.
    /// </summary>
    public IReadOnlyList<InternshipApplication> GetCompletedApplications(User admin) =>
        GetApplications(admin, ApplicationStatus.Completed);

    private static void ValidateBasics(InternshipApplication application)
    {
        if (string.IsNullOrWhiteSpace(application.AcademicYear))
            throw new ArgumentException("Öğretim yılı boş olamaz.", nameof(application));
        if (string.IsNullOrWhiteSpace(application.CompanyName))
            throw new ArgumentException("İşyeri adı boş olamaz.", nameof(application));
        if (application.EndDate < application.StartDate)
            throw new ArgumentException("Staj bitiş tarihi başlangıç tarihinden önce olamaz.", nameof(application));
        if (application.EngineerCount < 0)
            throw new ArgumentException("Mühendis sayısı negatif olamaz.", nameof(application));
    }

    private static void BindApplication(SqliteCommand command, InternshipApplication a)
    {
        static object Db(string? value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

        command.Parameters.AddWithValue("$year", a.AcademicYear.Trim());
        command.Parameters.AddWithValue("$semester", Db(a.Semester));
        command.Parameters.AddWithValue("$start", a.StartDate.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$end", a.EndDate.ToString(DateFormat, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$company", a.CompanyName.Trim());
        command.Parameters.AddWithValue("$department", Db(a.Department));
        command.Parameters.AddWithValue("$address", Db(a.Address));
        command.Parameters.AddWithValue("$phone", Db(a.Phone));
        command.Parameters.AddWithValue("$email", Db(a.Email));
        command.Parameters.AddWithValue("$fax", Db(a.Fax));
        command.Parameters.AddWithValue("$workDays",
            a.WorkDays.Count == 0 ? DBNull.Value : string.Join(',', a.WorkDays.Distinct().OrderBy(d => d)));
        command.Parameters.AddWithValue("$engineers", a.EngineerCount);
        command.Parameters.AddWithValue("$hasEngineer", a.EngineerCount >= 1 ? 1 : 0);
        command.Parameters.AddWithValue("$googleForm", Db(a.GoogleFormUrl));
    }

    private static List<InternshipApplication> Query(
        SqliteConnection connection, string clause, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT {SelectColumns} FROM Internships {clause};";
        foreach (var (name, value) in parameters)
            command.Parameters.AddWithValue(name, value);

        var result = new List<InternshipApplication>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            string? Str(int i) => reader.IsDBNull(i) ? null : reader.GetString(i);

            result.Add(new InternshipApplication
            {
                Id = reader.GetInt32(0),
                StudentId = reader.GetInt32(1),
                AcademicYear = reader.GetString(2),
                Semester = Str(3),
                StartDate = DateTime.ParseExact(reader.GetString(4), DateFormat, CultureInfo.InvariantCulture),
                EndDate = DateTime.ParseExact(reader.GetString(5), DateFormat, CultureInfo.InvariantCulture),
                CompanyName = reader.GetString(6),
                Department = Str(7),
                Address = Str(8),
                Phone = Str(9),
                Email = Str(10),
                Fax = Str(11),
                WorkDays = Str(12) is { } days
                    ? days.Split(',').Select(Enum.Parse<DayOfWeek>).ToList()
                    : new List<DayOfWeek>(),
                EngineerCount = reader.GetInt32(13),
                GoogleFormUrl = Str(14),
                Status = Enum.Parse<ApplicationStatus>(reader.GetString(15)),
                Ek16Verified = reader.GetInt32(16) == 1,
                CreatedAt = Str(17) is { } created
                    ? DateTime.Parse(created, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
                    : null
            });
        }
        return result;
    }
}
