using System;
using Npgsql;

class Program
{
    static void Main()
    {
        string connStr = "Host=103.75.182.211;Port=5432;Database=M.E.P Database; Username=postgres;Password=Vuatrom154321@";
        using var conn = new NpgsqlConnection(connStr);
        conn.Open();

        using var cmd = new NpgsqlCommand("SELECT user_id, full_name, username FROM users WHERE user_id = 'e4185ad3-af26-4f06-ab75-2758cedcca6e'", conn);
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            Console.WriteLine($"{reader.GetGuid(0)} | {reader.GetString(1)} | {reader.GetString(2)}");
        }
    }
}
