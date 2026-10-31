namespace StajEvrakSistemi.Core;

/// <summary>
/// Staj başvurusunun statüsü. Internships.Status sütununda metin olarak saklanır.
/// Şartname akışı: Yüklendi → İnceleniyor → Hatalı/Eksik → (düzeltilip yeniden yüklenir) → Onaylandı → Tamamlandı
/// </summary>
public enum ApplicationStatus
{
    Uploaded,         // Yüklendi
    UnderReview,      // İnceleniyor
    FaultyOrMissing,  // Hatalı / Eksik
    Approved,         // Onaylandı
    Completed         // Tamamlandı
}
