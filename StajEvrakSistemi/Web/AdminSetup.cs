using System.Text;
using StajEvrakSistemi.Models;
using StajEvrakSistemi.Services;

namespace StajEvrakSistemi.Web;

internal static class AdminSetup
{
    internal static int Run(AuthService auth)
    {
        Console.Write("Yönetici adı ve soyadı: ");
        string name = Console.ReadLine() ?? "";
        Console.Write("Yönetici T.C. kimlik numarası: ");
        string identity = Console.ReadLine() ?? "";
        Console.Write("Yönetici e-postası: ");
        string email = Console.ReadLine() ?? "";
        Console.Write("Yönetici şifresi (en az 8 karakter): ");
        string password = ReadPassword();
        Console.WriteLine();

        try
        {
            auth.Register(new Admin(identity, name, email), password);
            Console.WriteLine("İlk yönetici hesabı oluşturuldu.");
            return 0;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException or ForbiddenAccessException)
        {
            Console.Error.WriteLine(error.Message);
            return 1;
        }
    }

    private static string ReadPassword()
    {
        if (Console.IsInputRedirected)
            return Console.ReadLine() ?? "";

        var password = new StringBuilder();
        ConsoleKeyInfo key;
        while ((key = Console.ReadKey(intercept: true)).Key != ConsoleKey.Enter)
        {
            if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                password.Length--;
            else if (!char.IsControl(key.KeyChar))
                password.Append(key.KeyChar);
        }
        return password.ToString();
    }
}
