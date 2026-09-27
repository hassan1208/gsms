Imports System.IO
Imports Xunit

' The app keeps its database connection string in a shared module, so tests run one at a time.
<Assembly: CollectionBehavior(DisableTestParallelization:=True)>

''' <summary>Gives every test class a fresh database with the default admin logged in.</summary>
Public MustInherit Class DbTestBase
    Implements IDisposable

    Protected ReadOnly DbFile As String

    Protected Sub New()
        DbFile = Path.Combine(Path.GetTempPath(), "gs_test_" & Guid.NewGuid().ToString("N") & ".db")
        Db.Initialize(DbFile)
        Assert.True(Session.Login("admin", "admin123"))
    End Sub

    Protected Function Stock(productId As Long) As Decimal
        Return Ui.ToDec(Db.Scalar("SELECT stock FROM products WHERE id = @id", Db.P("@id", productId)))
    End Function

    Protected Function ProductId(namePrefix As String) As Long
        Return Ui.ToLng(Db.Scalar("SELECT id FROM products WHERE name LIKE @n", Db.P("@n", namePrefix & "%")))
    End Function

    Protected Function Item(productId As Long, price As Decimal, cost As Decimal, qty As Decimal) As CartItem
        Return New CartItem With {.ProductId = productId, .Barcode = "", .Name = "Item " & productId.ToString(),
                                  .Price = price, .Cost = cost, .Qty = qty}
    End Function

    Public Sub Dispose() Implements IDisposable.Dispose
        Session.Logout()
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools()
        Try
            File.Delete(DbFile)
        Catch ex As IOException
        End Try
    End Sub

End Class
