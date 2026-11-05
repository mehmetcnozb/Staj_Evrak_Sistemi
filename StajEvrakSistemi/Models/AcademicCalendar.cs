namespace StajEvrakSistemi.Models;

public sealed class AcademicCalendar
{
    public string AcademicYear { get; }
    public DateTime FallStartDate { get; }
    public DateTime FallEndDate { get; }
    public DateTime SpringStartDate { get; }
    public DateTime SpringEndDate { get; }

    public AcademicCalendar(
        string academicYear,
        DateTime fallStartDate,
        DateTime fallEndDate,
        DateTime springStartDate,
        DateTime springEndDate)
    {
        if (string.IsNullOrWhiteSpace(academicYear))
            throw new ArgumentException("Ogretim yili bos olamaz.", nameof(academicYear));

        if (fallEndDate.Date < fallStartDate.Date)
            throw new ArgumentException("Guz donemi bitisi baslangictan once olamaz.", nameof(fallEndDate));

        if (springEndDate.Date < springStartDate.Date)
            throw new ArgumentException("Bahar donemi bitisi baslangictan once olamaz.", nameof(springEndDate));

        if (springStartDate.Date <= fallEndDate.Date)
            throw new ArgumentException("Bahar donemi guz donemi bittikten sonra baslamali.", nameof(springStartDate));

        AcademicYear = academicYear.Trim();
        FallStartDate = fallStartDate.Date;
        FallEndDate = fallEndDate.Date;
        SpringStartDate = springStartDate.Date;
        SpringEndDate = springEndDate.Date;
    }
}
