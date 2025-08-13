


using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

using Dapper;

using DapperQueryTripBench;

using Microsoft.Data.SqlClient;

const string connectionString = "Server=SPYROS;Database=DBHistory;Trusted_Connection=True;TrustServerCertificate=True;";
try
{
    await using var warmConn = new SqlConnection(connectionString);
    await warmConn.OpenAsync();
    await warmConn.ExecuteAsync("SELECT 1");
    Console.WriteLine("[Warmup] DB connection OK.");

    BenchmarkRunner.Run(typeof(OrderBenchmarks));
}
catch (Exception ex)
{
    Console.Error.WriteLine("[Warmup] Failed to connect to DB:");
    Console.Error.WriteLine(ex);
    return;
}

Console.ReadLine();