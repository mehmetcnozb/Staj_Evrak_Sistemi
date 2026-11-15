using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using StajEvrakSistemi.Models;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using DotnetPoi.HWPF.UserModel;

namespace StajEvrakSistemi.Services;

public sealed class Ek16DocumentReader(ILogger<Ek16DocumentReader> logger)
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly IReadOnlyDictionary<string, string> Labels = CreateLabels();

    public (ScannedDocumentFields Fields, string? Error) Read(Ek16Document document)
    {
        var empty = new ScannedDocumentFields(null, null, null, null, null);
        if (document.FileFormat is "jpg" or "jpeg" or "png")
            return (empty, "Görüntüdeki alanları okumak için OCR gerekir. Metin içeren bir PDF veya Word belgesi yükleyin.");

        string contents;
        try
        {
            contents = document.FileFormat switch
            {
                "pdf" => ReadPdf(document.FilePath),
                "docx" => ReadDocx(document.FilePath),
                "doc" => ReadDoc(document.FilePath),
                _ => string.Empty
            };
        }
        catch (Exception error)
        {
            logger.LogWarning("Ek-16 {DocumentId} okunamadı ({ErrorType}).", document.Id, error.GetType().Name);
            return (empty, "Ek-16 dosyası okunamadı. Dosyayı açıp metin içeren bir PDF veya Word belgesi olarak kaydedin.");
        }

        if (string.IsNullOrWhiteSpace(contents))
            return (empty, "Dosyada seçilebilir metin yok. Taranmış PDF ve el yazısı için OCR gerekir.");

        var fields = Parse(contents);
        return fields == empty ? (empty, "Dosyada tanınan bir Ek-16 alanı bulunamadı.") : (fields, null);
    }

    private static string ReadPdf(string path)
    {
        using var pdf = PdfDocument.Open(path);
        return string.Join('\n', pdf.GetPages().Select(page => ContentOrderTextExtractor.GetText(page)));
    }

    private static string ReadDocx(string path)
    {
        using var archive = ZipFile.OpenRead(path);
        using var document = archive.GetEntry("word/document.xml")?.Open()
            ?? throw new InvalidDataException("Word document body is missing.");
        var body = XDocument.Load(document).Root!;
        XNamespace w = body.Name.Namespace;
        var lines = new StringBuilder();
        foreach (var element in body.Descendants())
        {
            if (element.Name == w + "t" && !element.Ancestors(w + "del").Any())
                lines.Append(element.Value);
            else if (element.Name == w + "tab" || element.Name == w + "br")
                lines.Append('\t');
            else if (element.Name == w + "p" || element.Name == w + "tc")
                lines.Append('\n');
        }
        return lines.ToString();
    }

    private static string ReadDoc(string path)
    {
        using var file = File.OpenRead(path);
        using var document = new HWPFDocument(file);
        return document.getRange().text().Replace('\a', '\n');
    }

    private static Dictionary<string, string> CreateLabels()
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        void Add(string field, params string[] aliases)
        {
            foreach (var alias in aliases)
                labels.Add(Key(alias), field);
        }

        Add("student", "Öğrenci Numarası", "Öğrenci No", "Öğrenci Nu.", "Okul No", "Numarası", "Student Number");
        Add("id", "T.C. Kimlik Numarası", "TC Kimlik No", "Kimlik No", "Kimlik Numarası", "Yabancı Kimlik No",
            "T.C. Kimlik No / Yabancı Kimlik No", "T.C. Kimlik No / Yabancı Kimlik Numarası", "Identity Number");
        Add("name", "Adı Soyadı", "Ad Soyad", "Öğrencinin Adı Soyadı", "Öğrenci Adı Soyadı", "Full Name");
        Add("first", "Adı", "Öğrencinin Adı");
        Add("last", "Soyadı", "Öğrencinin Soyadı");
        Add("department", "Bölümü", "Bölüm", "Öğrencinin Bölümü", "Department");
        Add("year", "Öğretim Yılı", "Akademik Yıl", "Öğretim Yılı ve Yarıyıl", "Öğretim Yılı (Dönem)", "Academic Year");
        Add("term", "Yarıyıl", "Dönem", "Semester", "Staj Yarıyılı");
        Add("start", "Staj Başlangıç Tarihi", "Staj Başlama Tarihi", "Başlangıç Tarihi", "Başlama Tarihi", "Start Date");
        Add("end", "Staj Bitiş Tarihi", "Bitiş Tarihi", "End Date");
        Add("dates", "Stajın Başlangıç ve Bitiş Tarihleri", "Staj Tarihleri");
        Add("duration", "Staj Günü Sayısı", "Staj Süresi", "Staj Süresi (İş Günü)", "İş Günü Sayısı", "Toplam İş Günü", "Declared Work Days");
        Add("work", "Haftalık Çalışma Günleri", "Çalışma Günleri", "Staj Günleri", "Work Days");
        Add("address", "İkametgâh Adresi", "Öğrencinin Adresi", "İkamet Adresi", "Öğrenci Adresi", "Address");
        Add("phone", "Telefon", "Telefon No", "Telefon Numarası", "Cep Telefonu", "Öğrenci Telefonu", "Phone");
        Add("email", "E-posta", "E Posta Adresi", "Öğrenci E-posta", "Email");
        Add("fax", "Faks", "Fax", "Faks No");
        Add("company", "İşyeri Adı", "İşyerinin Adı", "Kurum Adı", "Firma Adı", "İşyeri Unvanı", "Company Name");
        Add("engineers", "Mühendis Sayısı", "İşyerindeki Mühendis Sayısı", "Engineer Count");

        Add("birth", "Doğum Tarihi", "Birth Date");
        Add("previousDays", "Önceki Staj Gün Sayısı", "Daha Önce Yapılan Staj Gün Sayısı", "Previous Internship Days");
        Add("healthAid", "Sağlık Yardımı Statüsü", "Sağlık Yardımı Durumu", "Health Aid Status");
        return labels;
    }

    private static ScannedDocumentFields Parse(string text)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        string? pending = null;
        foreach (var part in text.Split(['\r', '\n', '\t', '\a'], StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            int separator = part.IndexOf(':');
            string candidate = (separator < 0 ? part : part[..separator]).Trim();
            if (Labels.TryGetValue(Key(candidate), out string? label))
            {
                pending = label;
                if (separator < 0)
                    continue;
                string remainder = part[(separator + 1)..].Trim();
                if (remainder.Length == 0)
                    continue;
                Store(label, remainder);
            }
            else if (pending is not null)
            {
                Store(pending, part);
            }

            pending = null;
        }

        void Store(string field, string value)
        {
            if (value.Length > 500 || !value.Any(char.IsLetterOrDigit))
                return;
            if (!values.TryGetValue(field, out var items))
                values[field] = items = [];
            items.Add(value);
        }

        string? Value(string field)
        {
            if (!values.TryGetValue(field, out var items))
                return null;
            var distinct = items.Distinct(StringComparer.Create(Turkish, true)).Take(2).ToArray();
            return distinct.Length == 1 ? distinct[0] : null;
        }

        string? yearAndTerm = Value("year");
        var year = Regex.Match(yearAndTerm ?? "", @"(?<![0-9])[0-9]{4}\s*[-–/]\s*[0-9]{4}(?![0-9])");
        string? term = Value("term");
        if (term is null && year.Success)
            term = yearAndTerm![(year.Index + year.Length)..].Trim();
        string? name = Value("name");
        if (name is null && Value("first") is { } first && Value("last") is { } last)
            name = $"{first} {last}";

        var combinedDates = Regex.Matches(Value("dates") ?? "", @"[0-9]{4}-[0-9]{2}-[0-9]{2}|[0-9]{1,2}[./][0-9]{1,2}[./][0-9]{4}");
        var start = ParseDate(Value("start"));
        var end = ParseDate(Value("end"));
        if (combinedDates.Count == 2)
        {
            start ??= ParseDate(combinedDates[0].Value);
            end ??= ParseDate(combinedDates[1].Value);
        }

        var amount = Regex.Match(Value("duration") ?? "", @"^([0-9]+(?:[,.][0-9]+)?)(?:\s+(?:iş\s+)?gün(?:ü)?)?$",
            RegexOptions.IgnoreCase).Groups[1].Value;
        return new ScannedDocumentFields(Value("student"), Value("id"), name, Value("department"),
            year.Success ? string.Join('-', Regex.Matches(year.Value, @"[0-9]{4}").Select(match => match.Value)) : null,
            start, end,
            decimal.TryParse(amount.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var days) ? days : null,
            ReadDays(Value("work")), Value("address"), Value("phone"), Value("email"), Value("fax"),
            string.IsNullOrWhiteSpace(term) ? null : term, Value("company"),
            ReadInteger(Value("engineers")), ParseDate(Value("birth")),
            ReadInteger(Value("previousDays")), Value("healthAid"));
    }

    private static int? ReadInteger(string? text) =>
        int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;

    private static DateTime? ParseDate(string? value) =>
        DateTime.TryParseExact(value, ["d.M.yyyy", "d/M/yyyy", "yyyy-MM-dd"], CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var date) ? date : null;

    private static IReadOnlyList<DayOfWeek>? ReadDays(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var days = new List<DayOfWeek>();
        foreach (var item in text.Split([',', ';', '/', ' '], StringSplitOptions.RemoveEmptyEntries))
        {
            var day = Enum.GetValues<DayOfWeek>().Where(value =>
                Key(Turkish.DateTimeFormat.GetDayName(value)) == Key(item) || Key(value.ToString()) == Key(item)).ToArray();
            if (day.Length != 1)
                return null;
            days.Add(day[0]);
        }
        return days.Distinct().ToArray();
    }

    private static string Key(string label) => new(label.Normalize(NormalizationForm.FormD).Where(char.IsLetterOrDigit)
        .Select(letter => letter == 'ı' ? 'i' : char.ToLowerInvariant(letter)).ToArray());
}
