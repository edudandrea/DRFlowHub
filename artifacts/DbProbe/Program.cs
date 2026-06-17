// See https://aka.ms/new-console-template for more information
using Microsoft.Data.Sqlite;

var dbPath = Path.GetFullPath(Path.Combine("..", "..", "back", "DRFlowHub.Api", "drflowhub.db"));
if (args.Length > 0)
{
    dbPath = Path.GetFullPath(args[0]);
}

using var connection = new SqliteConnection($"Data Source={dbPath}");
connection.Open();

using var command = connection.CreateCommand();
command.CommandText = """
SELECT Id, Email, Role, Ativo
FROM "User"
ORDER BY Id;
""";

using var reader = command.ExecuteReader();
while (reader.Read())
{
    Console.WriteLine($"{reader.GetInt32(0)} | {reader.GetString(1)} | {reader.GetString(2)} | {reader.GetInt32(3)}");
}
