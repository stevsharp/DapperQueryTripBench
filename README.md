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
