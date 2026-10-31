using Microsoft.Data.Sqlite;
using StajEvrakSistemi.Models;

namespace StajEvrakSistemi.Services;

/// <summary>
/// Servislerin ortak kontrolü: bellekte oluşturulmuş bir kullanıcı nesnesiyle yetki atlatılamasın diye
/// kullanıcının kaydı ve rolü veritabanından da doğrulanır.
/// </summary>
internal static class UserGuard
{
    public static void EnsureUserInDatabase(SqliteConnection connection, SqliteTransaction? transaction, User user)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT Role FROM Users WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", user.Id);

        if (command.ExecuteScalar() as string != user.Role.ToString())
            throw new ForbiddenAccessException("403 Yetkisiz Erişim: Kullanıcı kaydı doğrulanamadı.");
    }
}
