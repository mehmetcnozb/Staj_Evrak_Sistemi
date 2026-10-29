namespace StajEvrakSistemi.Core;

/// <summary>
/// Staj başvurusunun statüsü. Internships.Status sütununda metin olarak saklanır.
/// Akış: Yüklendi -> İnceleniyor -> (Revizyon Gerekli -> Yüklendi ...) -> Tamamlandı / Reddedildi
/// </summary>
public enum ApplicationStatus
{
    Uploaded,          // Yüklendi
    UnderReview,       // İnceleniyor
    RevisionRequired,  // Revizyon Gerekli
    Rejected,          // Reddedildi
    Completed          // Tamamlandı
}
