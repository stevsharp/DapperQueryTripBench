# DbRoundtripBenchmark

A **BenchmarkDotNet** project comparing different ways of fetching related data from SQL Server using **Dapper**:

- Multiple sequential queries (4 trips)
- Multiple parallel queries with `Task.WhenAll` (4 trips)
- Single `QueryMultiple` batch (1 trip)
- Single JOIN query (1 trip, server-shaped)

The goal: **prove with data** that reducing roundtrips is usually better for performance and database health.

---

## Why This Exists

Many developers assume that running several queries in parallel from C# to the *same database* is the fastest way to get related data.  
In reality, this can overload the connection pool, increase locking contention, and reduce SQL Server's own parallelism efficiency.

This benchmark measures the difference between those approaches in a controlled environment.

---

## Tech Stack

- [.NET 8+](https://dotnet.microsoft.com/)
- [BenchmarkDotNet](https://benchmarkdotnet.org/)
- [Dapper](https://dapper-tutorial.net/)
- [Microsoft.Data.SqlClient](https://learn.microsoft.com/en-us/sql/connect/ado-net/sqlclient-support)

---

## Database Setup

### 1. Create the tables

```sql
DROP TABLE IF EXISTS Payments;
DROP TABLE IF EXISTS ShippingDetails;
DROP TABLE IF EXISTS OrderItems;
DROP TABLE IF EXISTS Orders;
GO

CREATE TABLE Orders (
    OrderId INT IDENTITY PRIMARY KEY,
    OrderDate DATETIME2 NOT NULL,
    CustomerName NVARCHAR(100) NOT NULL
);

CREATE TABLE OrderItems (
    OrderItemId INT IDENTITY PRIMARY KEY,
    OrderId INT NOT NULL,
    ProductName NVARCHAR(100) NOT NULL,
    Quantity INT NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL,
    FOREIGN KEY (OrderId) REFERENCES Orders(OrderId)
);

CREATE TABLE ShippingDetails (
    ShippingId INT IDENTITY PRIMARY KEY,
    OrderId INT NOT NULL,
    Address NVARCHAR(200) NOT NULL,
    City NVARCHAR(50) NOT NULL,
    PostalCode NVARCHAR(20) NOT NULL,
    Country NVARCHAR(50) NOT NULL,
    FOREIGN KEY (OrderId) REFERENCES Orders(OrderId)
);

CREATE TABLE Payments (
    PaymentId INT IDENTITY PRIMARY KEY,
    OrderId INT NOT NULL,
    PaymentDate DATETIME2 NOT NULL,
    Amount DECIMAL(10,2) NOT NULL,
    PaymentMethod NVARCHAR(50) NOT NULL,
    FOREIGN KEY (OrderId) REFERENCES Orders(OrderId)
);
```
2. Seed with fake data

```sql
SET NOCOUNT ON;
DECLARE @i INT = 1;
WHILE @i <= 10000
BEGIN
    INSERT INTO Orders (OrderDate, CustomerName)
    VALUES (DATEADD(DAY, -@i % 365, GETDATE()), CONCAT('Customer ', @i));

    DECLARE @orderId INT = SCOPE_IDENTITY();

    DECLARE @j INT = 1;
    WHILE @j <= 3 + (@i % 3)
    BEGIN
        INSERT INTO OrderItems (OrderId, ProductName, Quantity, UnitPrice)
        VALUES (@orderId, CONCAT('Product ', @j), ABS(CHECKSUM(NEWID()) % 5) + 1, (ABS(CHECKSUM(NEWID()) % 100) + 1));
        SET @j += 1;
    END

    INSERT INTO ShippingDetails (OrderId, Address, City, PostalCode, Country)
    VALUES (@orderId, CONCAT('Address ', @i), 'CityX', '12345', 'CountryY');

    DECLARE @k INT = 1;
    WHILE @k <= 1 + (@i % 2)
    BEGIN
        INSERT INTO Payments (OrderId, PaymentDate, Amount, PaymentMethod)
        VALUES (@orderId, DATEADD(DAY, -@k, GETDATE()), (ABS(CHECKSUM(NEWID()) % 200) + 20), 'Credit Card');
        SET @k += 1;
    END

    SET @i += 1;
END
```
Running the Benchmark
1. Clone this repo

bash
```
git clone https://github.com/yourusername/DbRoundtripBenchmark.git
cd DbRoundtripBenchmark
```
2. Install dependencies

bash
```
dotnet add package BenchmarkDotNet
dotnet add package Dapper
dotnet add package Microsoft.Data.SqlClient
```
3. Configure your connection string
Set it in an environment variable:

powershell
```
$env:DB_CONN="Server=.;Database=YourDbName;Trusted_Connection=True;TrustServerCertificate=True;"
Or hardcode it in BenchConfig.cs / Program.cs.
```
4. Build and run in Release mode

bash
```
dotnet run -c Release
Sample Output

| Method                                                   | Mean     | Allocated |
|----------------------------------------------------------|----------|-----------|
| Multiple Async Calls Sequential (4 trips)                | 3.021 ms | 24.77 KB  |
| Multiple Async Calls Parallel (4 trips, Task.WhenAll)    | 1.568 ms | 28.08 KB  |
| QueryMultiple (1 trip)                                   | 2.466 ms | 12.59 KB  |
| JOIN (1 trip, server-shaped)                             | 2.468 ms | 14.02 KB  |
```
Interpretation

Sequential (4 trips): slowest, latency stacks per roundtrip

Parallel (4 trips): faster locally, but increases pool pressure and memory usage

QueryMultiple (1 trip): great balance — avoids pool pressure, keeps latency low

JOIN (1 trip): can be fastest for small child sets, but risks fan-out duplicates

