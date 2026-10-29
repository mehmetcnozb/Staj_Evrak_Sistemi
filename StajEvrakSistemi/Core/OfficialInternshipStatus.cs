namespace StajEvrakSistemi.Core;

/// <summary>
/// Admin veri setindeki resmi staj durumu.
/// OfficialStudentRecords.InternshipStatus sütununda metin olarak saklanır.
/// </summary>
public enum OfficialInternshipStatus
{
    NotStarted,  // Başlamadı
    InProgress,  // Devam Ediyor
    Completed,   // Tamamlandı
    Failed       // Başarısız
}
