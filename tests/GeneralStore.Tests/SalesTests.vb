Imports System.Data
Imports Xunit

Public Class SalesTests
    Inherits DbTestBase

    <Fact>
    Public Sub Login_RejectsWrongPassword()
        Assert.False(Session.Login("admin", "wrong"))
        Assert.True(Session.Login("ADMIN", "admin123"))
        Assert.True(Session.IsAdmin)
    End Sub

    <Fact>
    Public Sub SampleProducts_HaveValidUniqueEan13Barcodes()
        Dim rows = Db.Query("SELECT barcode FROM products").Rows.Cast(Of DataRow)().Select(Function(r) Ui.ToStr(r("barcode"))).ToList()
        Assert.NotEmpty(rows)
        Assert.Equal(rows.Count, rows.Distinct().Count())
        For Each b In rows
            Assert.Equal(13, b.Length)
            Assert.StartsWith("200", b)
            Assert.Equal(b.Substring(12), BarcodeHelper.Ean13CheckDigit(b.Substring(0, 12)).ToString())
        Next
    End Sub

    <Fact>
    Public Sub SaveSale_StoresTotalsAndDeductsStock()
        Dim sugar = ProductId("Sugar"), milk = ProductId("Fresh Milk")
        Dim s0 = Stock(sugar), m0 = Stock(milk)

        Dim r = SalesService.SaveSale({Item(sugar, 160D, 140D, 2D), Item(milk, 220D, 200D, 1.5D)}, 10D, 1000D, "Cash", "Ali")

        Assert.EndsWith("-0001", r.InvoiceNo)
        Assert.Equal(s0 - 2D, Stock(sugar))
        Assert.Equal(m0 - 1.5D, Stock(milk))
        Dim sale = Db.Query("SELECT * FROM sales WHERE id = @id", Db.P("@id", r.Id)).Rows(0)
        Assert.Equal(650D, Ui.ToDec(sale("sub_total")))
        Assert.Equal(640D, Ui.ToDec(sale("total")))
        Assert.Equal(360D, Ui.ToDec(sale("change_due")))
        Assert.Equal(0D, Ui.ToDec(sale("due")))
    End Sub

    <Fact>
    Public Sub InvoiceNumbers_AreSequentialAndNotReusedAfterVoid()
        Dim sugar = ProductId("Sugar")
        Dim a = SalesService.SaveSale({Item(sugar, 160D, 140D, 1D)}, 0D, 160D, "Cash", "")
        Dim b = SalesService.SaveSale({Item(sugar, 160D, 140D, 1D)}, 0D, 160D, "Cash", "")
        SalesService.VoidSale(a.Id)
        Dim c = SalesService.SaveSale({Item(sugar, 160D, 140D, 1D)}, 0D, 160D, "Cash", "")
        Assert.EndsWith("-0002", b.InvoiceNo)
        Assert.EndsWith("-0003", c.InvoiceNo)
    End Sub

    <Fact>
    Public Sub SaveSale_BlocksInsufficientStockAndUnderpayment()
        Dim milk = ProductId("Fresh Milk")
        Dim ex = Assert.Throws(Of InvalidOperationException)(
            Function() SalesService.SaveSale({Item(milk, 220D, 200D, 1000D)}, 0D, 999999D, "Cash", ""))
        Assert.Contains("Not enough stock", ex.Message)
        Assert.Throws(Of InvalidOperationException)(
            Function() SalesService.SaveSale({Item(milk, 220D, 200D, 1D)}, 0D, 100D, "Cash", ""))
    End Sub

    <Fact>
    Public Sub SaveSale_AllowsNegativeStockWhenEnabled()
        AppSettings.SetValue("allow_negative_stock", "1")
        Dim milk = ProductId("Fresh Milk")
        Dim before = Stock(milk)
        SalesService.SaveSale({Item(milk, 220D, 200D, before + 2D)}, 0D, 99999D, "Cash", "")
        Assert.Equal(-2D, Stock(milk))
    End Sub

    <Fact>
    Public Sub VoidSale_RestoresStock()
        Dim sugar = ProductId("Sugar")
        Dim s0 = Stock(sugar)
        Dim r = SalesService.SaveSale({Item(sugar, 160D, 140D, 3D)}, 0D, 480D, "Cash", "")
        SalesService.VoidSale(r.Id)
        Assert.Equal(s0, Stock(sugar))
        Assert.Equal(0L, Ui.ToLng(Db.Scalar("SELECT COUNT(*) FROM sale_items WHERE sale_id = @id", Db.P("@id", r.Id))))
    End Sub

    <Fact>
    Public Sub Summary_ComputesProfitAfterDiscount()
        Dim sugar = ProductId("Sugar"), milk = ProductId("Fresh Milk")
        SalesService.SaveSale({Item(sugar, 160D, 140D, 2D), Item(milk, 220D, 200D, 1.5D)}, 10D, 1000D, "Cash", "")
        SalesService.SaveSale({Item(sugar, 160D, 140D, 2D)}, 0D, 320D, "Card", "")
        Dim s = ReportsRepo.GetSummary(Date.Today, Date.Today.AddDays(1))
        Assert.Equal(2L, s.Invoices)
        Assert.Equal(960D, s.Net)
        Assert.Equal(10D, s.Discount)
        Assert.Equal(100D, s.Profit) ' (40 + 30 - 10) + 40
    End Sub

    <Fact>
    Public Sub AddStock_IncreasesStockAndUpdatesPrices()
        Dim milk = ProductId("Fresh Milk")
        Dim m0 = Stock(milk)
        SalesService.AddStock(milk, 12D, 205D, 230D, "Supplier", "bill 1")
        Assert.Equal(m0 + 12D, Stock(milk))
        Dim p = ProductRepo.GetById(milk)
        Assert.Equal(205D, Ui.ToDec(p("cost_price")))
        Assert.Equal(230D, Ui.ToDec(p("sale_price")))
    End Sub

    <Fact>
    Public Sub Settings_RoundTrip()
        AppSettings.SetValue("currency", "PKR")
        AppSettings.ClearCache()
        Assert.Equal("PKR", AppSettings.CurrencySymbol)
        Assert.StartsWith("PKR ", AppSettings.Money(1234.5D))
    End Sub

End Class
