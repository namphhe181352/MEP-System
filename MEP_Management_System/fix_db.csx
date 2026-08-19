using System;
using System.IO;
using Npgsql;
using Microsoft.Extensions.Configuration;

var builder = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json");
var config = builder.Build();
var connString = config.GetConnectionString("DefaultConnection");

using var conn = new NpgsqlConnection(connString);
conn.Open();
using var cmd = new NpgsqlCommand("ALTER TABLE expense_vouchers ALTER COLUMN category_id DROP NOT NULL;", conn);
cmd.ExecuteNonQuery();
Console.WriteLine("DB Schema fixed!");
