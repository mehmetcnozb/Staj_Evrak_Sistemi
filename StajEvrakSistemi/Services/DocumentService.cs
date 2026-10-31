using System.Globalization;
using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Core;
using StajEvrakSistemi.Data;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Ek-16 belgesinin yüklenmesi, listelenmesi ve silinmesi. Yükleme önce belge kurallarından
/// (kabul edilen format: .doc/.docx, PDF, JPEG, PNG) ve statü kısıtlarından geçer; geçen dosya depolama
/// klasörüne kopyalanır ve Documents tablosuna yazılır.
/// </summary>
public class DocumentService
{
    private readonly DatabaseManager _database;
    private readonly StatusWorkflowEngine _engine;
    private readonly string _storageRoot;
    private readonly Func<DateTime> _utcNow;

    public DocumentService(
        DatabaseManager database, StatusWorkflowEngine engine, string storageRoot, Func<DateTime>? utcNow = null)
    {
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
        if (string.IsNullOrWhiteSpace(storageRoot))
            throw new ArgumentException("Depolama klasörü boş olamaz.", nameof(storageRoot));

        _storageRoot = Path.GetFullPath(storageRoot);
        _utcNow = utcNow ?? (() => DateTime.UtcNow);
    }

    /// <summary>
    /// Öğrencinin kendi başvurusuna Ek-16 belgesi yükler.
    /// Başvuru Hatalı/Eksik statüsündeyse yükleme sonrası başvuru otomatik olarak Yüklendi statüsüne döner.
    /// </summary>
    /// <exception cref="DocumentValidationException">Dosya kurallara uymuyor (ör. kabul edilmeyen format).</exception>
    /// <exception cref="ForbiddenAccessException">Öğrenci değil ya da başkasının başvurusu.</exception>
    /// <exception cref="InvalidOperationException">Başvurunun statüsü değişikliğe izin vermiyor.</exception>
    public Ek16Document UploadDocument(User student, int internshipId, string sourceFilePath)
    {
        if (student is null)
            throw new LoginRequiredException("Evrak yüklemek için giriş yapmalısınız.");
        if (!student.HasPermission(Permission.UploadDocument))
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Evrağı yalnızca öğrenci yükleyebilir.");

        // Rol veritabanından doğrulanır; sahiplik ve statü kısıtı (kilitli statüde yükleme yok) kontrol edilir.
        _engine.ValidateUpdateOperation(internshipId, student);

        var candidate = new Ek16Document(internshipId, sourceFilePath);
        var errors = candidate.GetValidationErrors();
        if (errors.Count > 0)
            throw new DocumentValidationException(errors);

        string directory = Path.Combine(_storageRoot, internshipId.ToString(CultureInfo.InvariantCulture));
        Directory.CreateDirectory(directory);
        string destination = Path.Combine(directory, $"{Guid.NewGuid():N}.{candidate.FileFormat}");
        File.Copy(sourceFilePath, destination);

        var document = new Ek16Document(internshipId, destination);
        try
        {
            using var connection = _database.GetConnection();
            using var command = connection.CreateCommand();
            command.CommandText = """
                INSERT INTO Documents (InternshipId, DocumentType, FilePath, FileFormat, IsApproved, UploadedAt)
                VALUES ($internshipId, $type, $path, $format, 0, $uploadedAt);
                SELECT last_insert_rowid();
                """;
            command.Parameters.AddWithValue("$internshipId", internshipId);
            command.Parameters.AddWithValue("$type", document.DocumentType.ToString());
            command.Parameters.AddWithValue("$path", destination);
            command.Parameters.AddWithValue("$format", document.FileFormat);
            command.Parameters.AddWithValue("$uploadedAt", document.UploadedAt.ToString("o", CultureInfo.InvariantCulture));
            document.Id = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
        }
        catch
        {
            File.Delete(destination);   // kayıt yazılamadıysa yetim dosya bırakma
            throw;
        }

        // Düzeltilip yeniden yüklendi: Hatalı/Eksik -> Yüklendi.
        if (GetStatus(internshipId) == ApplicationStatus.FaultyOrMissing)
            _engine.ChangeStatus(internshipId, ApplicationStatus.Uploaded, student, "Belge yeniden yüklendi.");

        return document;
    }

    /// <summary>
    /// Başvurunun belgeleri. Öğrenci yalnızca kendi başvurusunun belgelerini, Admin hepsini görebilir.
    /// </summary>
    public IReadOnlyList<Ek16Document> GetDocuments(User user, int internshipId)
    {
        if (user is null)
            throw new LoginRequiredException("Evrakları görmek için giriş yapmalısınız.");

        using var connection = _database.GetConnection();
        UserGuard.EnsureUserInDatabase(connection, null, user);

        int ownerId = GetOwnerId(connection, internshipId);
        if (!user.HasPermission(Permission.ViewAllInternships) && ownerId != user.Id)
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Yalnızca kendi evraklarınızı görüntüleyebilirsiniz.");

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, FilePath, UploadedAt, IsApproved, RejectionReason
            FROM Documents WHERE InternshipId = $id ORDER BY Id;
            """;
        command.Parameters.AddWithValue("$id", internshipId);

        var documents = new List<Ek16Document>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var document = new Ek16Document(internshipId, reader.GetString(1))
            {
                Id = reader.GetInt32(0),
                UploadedAt = DateTime.Parse(reader.GetString(2), CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal)
            };
            document.RestoreState(reader.GetInt32(3) == 1, reader.IsDBNull(4) ? null : reader.GetString(4));
            documents.Add(document);
        }
        return documents;
    }

    /// <summary>
    /// Belgeyi siler (kayıt ve dosya). Başvuruyu silmeyle aynı kurallara tabidir: öğrenci Tamamlandı'da silemez, Admin silebilir.
    /// </summary>
    public void DeleteDocument(User user, int documentId)
    {
        if (user is null)
            throw new LoginRequiredException("Evrak silmek için giriş yapmalısınız.");

        using var connection = _database.GetConnection();

        using var find = connection.CreateCommand();
        find.CommandText = "SELECT InternshipId, FilePath FROM Documents WHERE Id = $id;";
        find.Parameters.AddWithValue("$id", documentId);

        int internshipId;
        string filePath;
        using (var reader = find.ExecuteReader())
        {
            if (!reader.Read())
                throw new KeyNotFoundException($"#{documentId} numaralı belge bulunamadı.");
            internshipId = reader.GetInt32(0);
            filePath = reader.GetString(1);
        }

        _engine.ValidateDeleteOperation(internshipId, user);

        using var delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM Documents WHERE Id = $id;";
        delete.Parameters.AddWithValue("$id", documentId);
        delete.ExecuteNonQuery();

        try { File.Delete(filePath); }
        catch (IOException) { /* kayıt silindi; dosya başka işlem tarafından kullanılıyorsa yetim kalabilir */ }
    }

    private ApplicationStatus GetStatus(int internshipId)
    {
        using var connection = _database.GetConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Status FROM Internships WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", internshipId);
        return Enum.Parse<ApplicationStatus>((string)command.ExecuteScalar()!);
    }

    private static int GetOwnerId(SqliteConnection connection, int internshipId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT StudentId FROM Internships WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", internshipId);

        return command.ExecuteScalar() is long owner
            ? (int)owner
            : throw new KeyNotFoundException($"#{internshipId} numaralı staj başvurusu bulunamadı.");
    }
}
