using StajEvrakSistemi.Data;

var database = new DatabaseManager();
database.InitializeDatabase();

Console.WriteLine($"Veritabanı hazır: {DatabaseManager.DefaultDatabaseFile}");
