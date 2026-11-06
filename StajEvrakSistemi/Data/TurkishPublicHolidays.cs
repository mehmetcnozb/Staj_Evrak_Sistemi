namespace StajEvrakSistemi.Data;

public static class TurkishPublicHolidays
{
    public const int FirstSupportedYear = 2026;
    public const int LastSupportedYear = 2027;

    // Kaynak: https://vakithesaplama.diyanet.gov.tr/2429_kanun.php
    // 2026: https://vakithesaplama.diyanet.gov.tr/icerik.php?icerik=158
    // 2027: https://vakithesaplama.diyanet.gov.tr/icerik.php?icerik=159
    public static IReadOnlyDictionary<DateOnly, decimal> GetForRange(DateTime startDate, DateTime endDate)
    {
        DateOnly firstDay = DateOnly.FromDateTime(startDate);
        DateOnly lastDay = DateOnly.FromDateTime(endDate);

        if (lastDay < firstDay)
            throw new ArgumentException("Staj bitisi baslangictan once olamaz.", nameof(endDate));

        if (firstDay.Year < FirstSupportedYear || lastDay.Year > LastSupportedYear)
            throw new ArgumentOutOfRangeException(nameof(startDate),
                "Resmi tatil takvimi yalnizca 2026 ve 2027 yillari icin hazir.");

        var holidays = new Dictionary<DateOnly, decimal>();
        for (int year = firstDay.Year; year <= lastDay.Year; year++)
        {
            AddHoliday(holidays, new DateOnly(year, 1, 1), 1m);
            AddHoliday(holidays, new DateOnly(year, 4, 23), 1m);
            AddHoliday(holidays, new DateOnly(year, 5, 1), 1m);
            AddHoliday(holidays, new DateOnly(year, 5, 19), 1m);
            AddHoliday(holidays, new DateOnly(year, 7, 15), 1m);
            AddHoliday(holidays, new DateOnly(year, 8, 30), 1m);
            AddHoliday(holidays, new DateOnly(year, 10, 28), 0.5m);
            AddHoliday(holidays, new DateOnly(year, 10, 29), 1m);

            if (year == 2026)
            {
                AddReligiousHoliday(holidays, new DateOnly(2026, 3, 20), 3);
                AddReligiousHoliday(holidays, new DateOnly(2026, 5, 27), 4);
            }
            else
            {
                AddReligiousHoliday(holidays, new DateOnly(2027, 3, 9), 3);
                AddReligiousHoliday(holidays, new DateOnly(2027, 5, 16), 4);
            }
        }

        // 2429 sayili kanuna gore cuma aksam biten tatilin ertesi cumartesisi de tatildir.
        var fridayHolidays = holidays.Where(entry => entry.Key.DayOfWeek == DayOfWeek.Friday
            && entry.Value == 1m).Select(entry => entry.Key).ToList();
        foreach (DateOnly friday in fridayHolidays)
            AddHoliday(holidays, friday.AddDays(1), 1m);

        return holidays.Where(entry => entry.Key >= firstDay && entry.Key <= lastDay)
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    private static void AddReligiousHoliday(Dictionary<DateOnly, decimal> holidays, DateOnly firstDay, int duration)
    {
        AddHoliday(holidays, firstDay.AddDays(-1), 0.5m);
        for (int day = 0; day < duration; day++)
            AddHoliday(holidays, firstDay.AddDays(day), 1m);
    }

    private static void AddHoliday(Dictionary<DateOnly, decimal> holidays, DateOnly date, decimal duration)
    {
        // Ayni tarihe iki bayram denk gelirse tatil suresi toplanmaz.
        holidays.TryGetValue(date, out decimal existingDuration);
        holidays[date] = Math.Max(existingDuration, duration);
    }
}
