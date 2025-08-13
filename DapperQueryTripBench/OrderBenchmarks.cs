namespace DapperQueryTripBench;


using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

using Dapper;

using Microsoft.Data.SqlClient;

#region Benchmarks

[MemoryDiagnoser]
// Uncomment the next line on Windows to capture threadpool / CLR counters
// [EtwProfiler]
public class OrderBenchmarks
{
    private readonly string _connStr = "Server=SPYROS;Database=DBHistory;Trusted_Connection=True;TrustServerCertificate=True;";
    private readonly int _orderId = 1;


    [Benchmark(Description = "Multiple Async Calls Sequential (4 trips)")]
    public async Task<object> MultipleAsyncCalls_Sequential()
    {
        await using var conn = new SqlConnection(_connStr);

        var order = await conn.QuerySingleAsync<OrderDto>(
            "SELECT OrderId, OrderDate, CustomerName FROM Orders WHERE OrderId = @id",
            new { id = _orderId });

        var items = (await conn.QueryAsync<OrderItemDto>(
            "SELECT ProductName, Quantity, UnitPrice FROM OrderItems WHERE OrderId = @id",
            new { id = _orderId })).ToList();

        var shipping = (await conn.QueryAsync<ShippingDto>(
            "SELECT Address, City, PostalCode, Country FROM ShippingDetails WHERE OrderId = @id",
            new { id = _orderId })).SingleOrDefault();

        var payments = (await conn.QueryAsync<PaymentDto>(
            "SELECT PaymentDate, Amount, PaymentMethod FROM Payments WHERE OrderId = @id",
            new { id = _orderId })).ToList();

        order.Items = items;
        order.Shipping = shipping;
        order.Payments = payments;

        return order;
    }

    [Benchmark(Description = "Multiple Async Calls Parallel (4 trips, Task.WhenAll)")]
    public async Task<object> MultipleAsyncCalls_Parallel()
    {
        // Separate connections per task (no MARS; avoids single-connection contention)
        var orderTask = Task.Run(async () =>
        {
            await using var c = new SqlConnection(_connStr);
            return await c.QuerySingleAsync<OrderDto>(
                "SELECT OrderId, OrderDate, CustomerName FROM Orders WHERE OrderId = @id",
                new { id = _orderId });
        });

        var itemsTask = Task.Run(async () =>
        {
            await using var c = new SqlConnection(_connStr);
            var res = await c.QueryAsync<OrderItemDto>(
                "SELECT ProductName, Quantity, UnitPrice FROM OrderItems WHERE OrderId = @id",
                new { id = _orderId });
            return res.ToList();
        });

        var shippingTask = Task.Run(async () =>
        {
            await using var c = new SqlConnection(_connStr);
            var res = await c.QueryAsync<ShippingDto>(
                "SELECT Address, City, PostalCode, Country FROM ShippingDetails WHERE OrderId = @id",
                new { id = _orderId });
            return res.SingleOrDefault();
        });

        var paymentsTask = Task.Run(async () =>
        {
            await using var c = new SqlConnection(_connStr);
            var res = await c.QueryAsync<PaymentDto>(
                "SELECT PaymentDate, Amount, PaymentMethod FROM Payments WHERE OrderId = @id",
                new { id = _orderId });
            return res.ToList();
        });

        await Task.WhenAll(orderTask, itemsTask, shippingTask, paymentsTask);

        var order = orderTask.Result;
        order.Items = itemsTask.Result;
        order.Shipping = shippingTask.Result;
        order.Payments = paymentsTask.Result;
        return order;
    }

    [Benchmark(Description = "QueryMultiple (1 trip)")]
    public async Task<object> QueryMultiple_OneTrip()
    {
        const string sql = @"
        SELECT OrderId, OrderDate, CustomerName FROM Orders WHERE OrderId = @id;
        SELECT ProductName, Quantity, UnitPrice FROM OrderItems WHERE OrderId = @id;
        SELECT Address, City, PostalCode, Country FROM ShippingDetails WHERE OrderId = @id;
        SELECT PaymentDate, Amount, PaymentMethod FROM Payments WHERE OrderId = @id;";

        await using var conn = new SqlConnection(_connStr);
        using var grid = await conn.QueryMultipleAsync(sql, new { id = _orderId });

        var order = await grid.ReadSingleAsync<OrderDto>();
        var items = (await grid.ReadAsync<OrderItemDto>()).ToList();
        var shipping = (await grid.ReadAsync<ShippingDto>()).SingleOrDefault();
        var payments = (await grid.ReadAsync<PaymentDto>()).ToList();

        order.Items = items;
        order.Shipping = shipping;
        order.Payments = payments;
        return order;
    }

    [Benchmark(Description = "JOIN (1 trip, server-shaped)")]
    public async Task<object> Join_OneTrip()
    {
        const string sql = @"
SELECT 
    o.OrderId, o.OrderDate, o.CustomerName,
    i.ProductName, i.Quantity, i.UnitPrice,
    s.Address, s.City, s.PostalCode, s.Country,
    p.PaymentDate, p.Amount, p.PaymentMethod
FROM Orders o
LEFT JOIN OrderItems i     ON o.OrderId = i.OrderId
LEFT JOIN ShippingDetails s ON o.OrderId = s.OrderId
LEFT JOIN Payments p       ON o.OrderId = p.OrderId
WHERE o.OrderId = @id;";

        await using var conn = new SqlConnection(_connStr);
        // For the JOIN case we return the raw rows; consumer can reshape as needed
        var rows = await conn.QueryAsync(sql, new { id = _orderId });
        return rows.ToList();
    }
}

#endregion

#region DTOs

public sealed class OrderDto
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public string CustomerName { get; set; } = "";
    public List<OrderItemDto> Items { get; set; } = new();
    public ShippingDto? Shipping { get; set; }
    public List<PaymentDto> Payments { get; set; } = new();
}

public sealed class OrderItemDto
{
    public string ProductName { get; set; } = "";
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public sealed class ShippingDto
{
    public string Address { get; set; } = "";
    public string City { get; set; } = "";
    public string PostalCode { get; set; } = "";
    public string Country { get; set; } = "";
}

public sealed class PaymentDto
{
    public DateTime PaymentDate { get; set; }
    public decimal Amount { get; set; }
    public string PaymentMethod { get; set; } = "";
}

#endregion
