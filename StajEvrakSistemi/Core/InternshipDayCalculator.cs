namespace StajEvrakSistemi.Core;

public static class InternshipDayCalculator
{
    // Baslangic ve bitis gunleri dahil net staj gununu hesaplar.
    public static decimal CalculateWorkDays(
        DateTime startDate,
        DateTime endDate,
        IEnumerable<DayOfWeek> workDays,
        IReadOnlyDictionary<DateOnly, decimal> publicHolidays)
    {
        ArgumentNullException.ThrowIfNull(workDays);
        ArgumentNullException.ThrowIfNull(publicHolidays);

        DateOnly firstDay = DateOnly.FromDateTime(startDate);
        DateOnly lastDay = DateOnly.FromDateTime(endDate);

        if (lastDay < firstDay)
            throw new ArgumentException("Staj bitisi baslangictan once olamaz.", nameof(endDate));

        var selectedDays = workDays.ToHashSet();
        if (selectedDays.Any(day => !Enum.IsDefined(day)))
            throw new ArgumentException("Gecersiz bir calisma gunu secildi.", nameof(workDays));

        if (selectedDays.Count == 0)
            throw new ArgumentException("En az bir calisma gunu secilmeli.", nameof(workDays));

        // Tam gun tatil 1, yarim gun tatil 0.5 olarak verilir.
        if (publicHolidays.Values.Any(days => days != 1m && days != 0.5m))
            throw new ArgumentException("Tatil suresi 1 veya 0.5 gun olmali.", nameof(publicHolidays));

        decimal totalDays = 0;
        DateOnly currentDay = firstDay;

        while (true)
        {
            if (selectedDays.Contains(currentDay.DayOfWeek))
            {
                publicHolidays.TryGetValue(currentDay, out decimal holidayDays);
                totalDays += 1m - holidayDays;
            }

            // Son gunden sonra ilerlemeyerek tarih sinirinin asilmasini onler.
            if (currentDay == lastDay)
                break;

            currentDay = currentDay.AddDays(1);
        }

        return totalDays;
    }

    // Hesaplanan gun sayisini Ek-16 belgesinde belirtilen gun sayisiyla karsilastirir.
    public static bool MatchesDeclaredDays(decimal netWorkDays, decimal declaredDays)
    {
        if (netWorkDays < 0)
            throw new ArgumentOutOfRangeException(nameof(netWorkDays), "Net staj gunu negatif olamaz.");

        if (declaredDays < 0)
            throw new ArgumentOutOfRangeException(nameof(declaredDays), "Belgedeki staj gunu negatif olamaz.");

        return netWorkDays == declaredDays;
    }
}
