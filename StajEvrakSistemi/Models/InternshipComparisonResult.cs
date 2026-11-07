namespace StajEvrakSistemi.Models;

public sealed record InternshipComparisonResult(
    IReadOnlyList<Ek16Discrepancy> Discrepancies,
    decimal? CalculatedWorkDays,
    decimal? DocumentCalculatedWorkDays)
{
    public bool IsMatched => Discrepancies.Count == 0;
}
