Imports System.Data
Imports System.IO
Imports Microsoft.Data.Sqlite
Imports Xunit

Public Class DatabaseTests
    Inherits DbTestBase

    <Fact>
    Public Sub BackupAndRestore_RoundTrip()
        Dim sugar = ProductId("Sugar")
        SalesService.SaveSale({Item(sugar, 160D, 140D, 1D)}, 0D, 160D, "Cash", "")
        Dim backup = Path.Combine(Path.GetTempPath(), "gs_backup_" & Guid.NewGuid().ToString("N") & ".db")
        Try
            Db.BackupTo(backup)
            Db.Execute("DELETE FROM sale_items")
            Db.Execute("DELETE FROM sales")
            Db.RestoreFrom(backup)
            Assert.Equal(1L, Ui.ToLng(Db.Scalar("SELECT COUNT(*) FROM sales")))
        Finally
            SqliteConnection.ClearAllPools()
            File.Delete(backup)
        End Try
    End Sub

    <Fact>
    Public Sub Restore_RejectsNonBackupFile()
        Dim bogus = Path.Combine(Path.GetTempPath(), "gs_bogus_" & Guid.NewGuid().ToString("N") & ".db")
        Try
            Using cn As New SqliteConnection("Data Source=" & bogus & ";Pooling=False")
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = "CREATE TABLE something(x)"
                    cmd.ExecuteNonQuery()
                End Using
            End Using
            Assert.Throws(Of InvalidOperationException)(Sub() Db.RestoreFrom(bogus))
        Finally
            File.Delete(bogus)
        End Try
    End Sub

    <Fact>
    Public Sub Version1Database_IsMigrated()
        ' A database created by the first release has no customers table and no sales.due column.
        Dim oldDb = Path.Combine(Path.GetTempPath(), "gs_v1_" & Guid.NewGuid().ToString("N") & ".db")
        Try
            Using cn As New SqliteConnection("Data Source=" & oldDb & ";Pooling=False")
                cn.Open()
                Using cmd = cn.CreateCommand()
                    cmd.CommandText = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Data", "v1_schema.sql")) &
                        "INSERT INTO sales(invoice_no, sale_date, sub_total, total, paid) VALUES('OLD-1', '2025-01-01 10:00:00', 10, 10, 10);"
                    cmd.ExecuteNonQuery()
                End Using
            End Using

            Db.RestoreFrom(oldDb)

            Dim cols = Db.Query("PRAGMA table_info(sales)").Rows.Cast(Of DataRow)().Select(Function(r) Ui.ToStr(r("name"))).ToList()
            Assert.Contains("due", cols)
            Assert.Contains("customer_id", cols)
            Assert.Equal(0D, Ui.ToDec(Db.Scalar("SELECT due FROM sales WHERE invoice_no = 'OLD-1'")))
            Assert.Equal(0L, Ui.ToLng(Db.Scalar("SELECT COUNT(*) FROM customers")))
        Finally
            SqliteConnection.ClearAllPools()
            File.Delete(oldDb)
        End Try
    End Sub

    <Fact>
    Public Sub Query_TypesColumnsFromValues()
        Dim dt = Db.Query("SELECT id, stock, name, COALESCE(SUM(stock), 0) AS total FROM products GROUP BY id")
        Assert.Equal(GetType(Long), dt.Columns("id").DataType)
        Assert.Equal(GetType(Double), dt.Columns("stock").DataType)
        Assert.Equal(GetType(String), dt.Columns("name").DataType)
    End Sub

End Class
