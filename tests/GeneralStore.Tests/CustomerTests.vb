Imports Xunit

Public Class CustomerTests
    Inherits DbTestBase

    <Fact>
    Public Sub CreditSales_PaymentsAndLedger()
        Dim sugar = ProductId("Sugar")
        Dim cust = CustomerRepo.Save(0, "Aslam Bhai", "0300-1", "Street 5")

        ' Bill 480, customer pays 200 now -> 280 udhaar
        Dim first = SalesService.SaveSale({Item(sugar, 160D, 140D, 3D)}, 0D, 200D, "Credit (Udhaar)", "Aslam Bhai", cust)
        Dim sale = Db.Query("SELECT due, change_due FROM sales WHERE id = @id", Db.P("@id", first.Id)).Rows(0)
        Assert.Equal(280D, Ui.ToDec(sale("due")))
        Assert.Equal(0D, Ui.ToDec(sale("change_due")))
        Assert.Equal(280D, CustomerRepo.Balance(cust))

        ' Fully on credit
        SalesService.SaveSale({Item(sugar, 160D, 140D, 3D)}, 0D, 0D, "Credit (Udhaar)", "Aslam Bhai", cust)
        Assert.Equal(760D, CustomerRepo.Balance(cust))

        CustomerRepo.ReceivePayment(cust, 500D, "cash")
        Assert.Equal(260D, CustomerRepo.Balance(cust))
        Assert.Equal(260D, CustomerRepo.TotalOutstanding())

        Dim ledger = CustomerRepo.Ledger(cust)
        Assert.Equal(3, ledger.Rows.Count)
        Assert.Equal(260D, Ui.ToDec(ledger.Rows(2)("balance")))
        Assert.Single(CustomerRepo.List("", onlyWithBalance:=True).Rows.Cast(Of Object)())

        Assert.Equal(760D, ReportsRepo.GetSummary(Date.Today, Date.Today.AddDays(1)).Due) ' 280 + 480 given today

        ' Voiding a credit sale removes its udhaar
        SalesService.VoidSale(first.Id)
        Assert.Equal(-20D, CustomerRepo.Balance(cust))
    End Sub

    <Fact>
    Public Sub CashSale_WithoutCustomerCannotBeShort()
        Dim sugar = ProductId("Sugar")
        Assert.Throws(Of InvalidOperationException)(
            Function() SalesService.SaveSale({Item(sugar, 160D, 140D, 1D)}, 0D, 50D, "Cash", ""))
    End Sub

    <Fact>
    Public Sub CustomerNames_AreUniqueIgnoringCase()
        CustomerRepo.Save(0, "Aslam Bhai", "", "")
        Assert.Throws(Of InvalidOperationException)(Function() CustomerRepo.Save(0, "aslam bhai", "", ""))
    End Sub

    <Fact>
    Public Sub Delete_OnlyWithoutHistory()
        Dim sugar = ProductId("Sugar")
        Dim used = CustomerRepo.Save(0, "Used", "", "")
        SalesService.SaveSale({Item(sugar, 160D, 140D, 1D)}, 0D, 0D, "Credit (Udhaar)", "Used", used)
        Assert.False(CustomerRepo.Delete(used))
        Assert.True(CustomerRepo.Delete(CustomerRepo.Save(0, "Unused", "", "")))
    End Sub

End Class
