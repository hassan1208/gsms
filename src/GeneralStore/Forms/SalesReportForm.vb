Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Sales invoices, product-wise sales and stock reports.</summary>
<DesignerCategory("Code")>
Public Class SalesReportForm
    Inherits Form

    Private ReadOnly dtpFrom As New DateTimePicker With {.Format = DateTimePickerFormat.Custom, .CustomFormat = "dd-MMM-yyyy", .Width = 130, .Margin = New Padding(4, 8, 4, 4)}
    Private ReadOnly dtpTo As New DateTimePicker With {.Format = DateTimePickerFormat.Custom, .CustomFormat = "dd-MMM-yyyy", .Width = 130, .Margin = New Padding(4, 8, 4, 4)}
    Private ReadOnly txtInvoice As New TextBox With {.Width = 170, .Margin = New Padding(4, 8, 4, 4)}
    Private ReadOnly tabs As New TabControl With {.Dock = DockStyle.Fill}
    Private ReadOnly gridInvoices As New DataGridView()
    Private ReadOnly gridItems As New DataGridView()
    Private ReadOnly gridProducts As New DataGridView()
    Private ReadOnly gridStock As New DataGridView()
    Private ReadOnly chkLowOnly As New CheckBox With {.Text = "Low stock only", .AutoSize = True, .Margin = New Padding(10, 12, 4, 4)}
    Private ReadOnly lblSummary As New Label With {.Dock = DockStyle.Bottom, .Height = 34, .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold), .ForeColor = Ui.PrimaryColor, .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(10, 0, 0, 0)}
    Private ReadOnly lblStockSummary As New Label With {.AutoSize = True, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold), .Margin = New Padding(16, 12, 4, 4)}

    Public Sub New()
        Ui.SetupForm(Me, "Sales Reports", 1200, 700)
        WindowState = FormWindowState.Maximized

        ' ---- Filter bar ----
        Dim filters = Ui.MakeToolbar(54)
        Dim btnShow = Ui.MakeButton("Show", Ui.SuccessColor, 80)
        Dim btnToday = Ui.MakeButton("Today", Ui.InfoColor, 80)
        Dim btnYesterday = Ui.MakeButton("Yesterday", Ui.InfoColor, 100)
        Dim btnMonth = Ui.MakeButton("This Month", Ui.InfoColor, 110)
        filters.Controls.AddRange(New Control() {
            Ui.MakeLabel("From:"), dtpFrom, Ui.MakeLabel("To:"), dtpTo, btnShow, btnToday, btnYesterday, btnMonth,
            Ui.MakeLabel("   Scan / type invoice #:"), txtInvoice})

        ' ---- Tab 1: invoices ----
        Ui.StyleGrid(gridInvoices)
        Ui.AddColumn(gridInvoices, "id", "ID", visible:=False)
        Ui.AddColumn(gridInvoices, "invoice_no", "Invoice #", fillWeight:=110)
        Ui.AddColumn(gridInvoices, "sale_date", "Date / Time", fillWeight:=120)
        Ui.AddColumn(gridInvoices, "cashier", "Cashier", fillWeight:=90)
        Ui.AddColumn(gridInvoices, "customer_name", "Customer", fillWeight:=100)
        Ui.AddColumn(gridInvoices, "item_count", "Items", "N0", 45, True)
        Ui.AddColumn(gridInvoices, "sub_total", "Sub Total", "N2", 75, True)
        Ui.AddColumn(gridInvoices, "discount", "Discount", "N2", 70, True)
        Ui.AddColumn(gridInvoices, "total", "Total", "N2", 80, True)
        Ui.AddColumn(gridInvoices, "due", "Udhaar", "N2", 70, True)
        Ui.AddColumn(gridInvoices, "payment_method", "Payment", fillWeight:=80)

        Ui.StyleGrid(gridItems)
        Ui.AddColumn(gridItems, "barcode", "Barcode", fillWeight:=100)
        Ui.AddColumn(gridItems, "product_name", "Item", fillWeight:=220)
        Ui.AddColumn(gridItems, "qty", "Qty", "#,##0.###", 60, True)
        Ui.AddColumn(gridItems, "unit_price", "Price", "N2", 70, True)
        Ui.AddColumn(gridItems, "line_total", "Total", "N2", 80, True)
        Ui.AddColumn(gridItems, "profit", "Profit", "N2", 70, True, visible:=Session.IsAdmin)

        Dim split As New SplitContainer With {.Dock = DockStyle.Fill, .Orientation = Orientation.Horizontal}
        split.Panel1.Controls.Add(gridInvoices)
        split.Panel2.Controls.Add(Ui.Captioned("Items in selected invoice", gridItems))

        Dim invoiceButtons = Ui.MakeToolbar(52)
        Dim btnPreview = Ui.MakeButton("View Receipt", Ui.InfoColor, 130)
        Dim btnReprint = Ui.MakeButton("Reprint", Ui.PrimaryColor, 100)
        Dim btnVoid = Ui.MakeButton("Void / Return Sale", Ui.DangerColor, 170)
        Dim btnExportInv = Ui.MakeButton("Export CSV", Ui.NeutralColor, 120)
        btnVoid.Visible = Session.IsAdmin
        invoiceButtons.Controls.AddRange(New Control() {btnPreview, btnReprint, btnVoid, btnExportInv})

        Dim tabInvoices As New TabPage("Invoices")
        tabInvoices.Controls.Add(split)
        tabInvoices.Controls.Add(invoiceButtons)

        ' ---- Tab 2: product-wise ----
        Ui.StyleGrid(gridProducts)
        Ui.AddColumn(gridProducts, "barcode", "Barcode", fillWeight:=100)
        Ui.AddColumn(gridProducts, "product_name", "Product", fillWeight:=220)
        Ui.AddColumn(gridProducts, "qty", "Qty Sold", "#,##0.###", 70, True)
        Ui.AddColumn(gridProducts, "revenue", "Sales Amount", "N2", 90, True)
        Ui.AddColumn(gridProducts, "cost", "Cost", "N2", 80, True, visible:=Session.IsAdmin)
        Ui.AddColumn(gridProducts, "profit", "Profit", "N2", 80, True, visible:=Session.IsAdmin)
        Dim productButtons = Ui.MakeToolbar(52)
        Dim btnExportProd = Ui.MakeButton("Export CSV", Ui.NeutralColor, 120)
        productButtons.Controls.Add(btnExportProd)
        Dim tabProducts As New TabPage("Product-wise Sales")
        tabProducts.Controls.Add(gridProducts)
        tabProducts.Controls.Add(productButtons)

        ' ---- Tab 3: stock ----
        Ui.StyleGrid(gridStock)
        Ui.AddColumn(gridStock, "barcode", "Barcode", fillWeight:=100)
        Ui.AddColumn(gridStock, "name", "Product", fillWeight:=200)
        Ui.AddColumn(gridStock, "category", "Category", fillWeight:=100)
        Ui.AddColumn(gridStock, "stock", "Stock", "#,##0.###", 60, True)
        Ui.AddColumn(gridStock, "reorder_level", "Reorder At", "#,##0.###", 60, True)
        Ui.AddColumn(gridStock, "cost_value", "Stock Value (Cost)", "N2", 90, True, visible:=Session.IsAdmin)
        Ui.AddColumn(gridStock, "retail_value", "Stock Value (Sale)", "N2", 90, True)
        Dim stockButtons = Ui.MakeToolbar(52)
        Dim btnExportStock = Ui.MakeButton("Export CSV", Ui.NeutralColor, 120)
        stockButtons.Controls.AddRange(New Control() {btnExportStock, chkLowOnly, lblStockSummary})
        Dim tabStock As New TabPage("Stock Report")
        tabStock.Controls.Add(gridStock)
        tabStock.Controls.Add(stockButtons)

        tabs.TabPages.AddRange({tabInvoices, tabProducts, tabStock})

        Controls.Add(tabs)
        Controls.Add(lblSummary)
        Controls.Add(filters)
        Controls.Add(Ui.MakeHeader("Reports"))

        dtpFrom.Value = Date.Today
        dtpTo.Value = Date.Today

        AddHandler btnShow.Click, Sub() LoadAll()
        AddHandler btnToday.Click, Sub() SetRange(Date.Today, Date.Today)
        AddHandler btnYesterday.Click, Sub() SetRange(Date.Today.AddDays(-1), Date.Today.AddDays(-1))
        AddHandler btnMonth.Click, Sub() SetRange(New Date(Date.Today.Year, Date.Today.Month, 1), Date.Today)
        AddHandler txtInvoice.KeyDown, Sub(s, e)
                                           If e.KeyCode = Keys.Enter Then
                                               e.SuppressKeyPress = True
                                               FindInvoice()
                                           End If
                                       End Sub
        AddHandler gridInvoices.SelectionChanged, Sub() LoadItems()
        AddHandler gridInvoices.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then PrintSelected(True)
        AddHandler btnPreview.Click, Sub() PrintSelected(True)
        AddHandler btnReprint.Click, Sub() PrintSelected(False)
        AddHandler btnVoid.Click, Sub() VoidSelected()
        AddHandler btnExportInv.Click, Sub() Ui.ExportGridToCsv(gridInvoices, $"invoices_{dtpFrom.Value:yyyyMMdd}_{dtpTo.Value:yyyyMMdd}.csv", Me)
        AddHandler btnExportProd.Click, Sub() Ui.ExportGridToCsv(gridProducts, $"product_sales_{dtpFrom.Value:yyyyMMdd}_{dtpTo.Value:yyyyMMdd}.csv", Me)
        AddHandler btnExportStock.Click, Sub() Ui.ExportGridToCsv(gridStock, $"stock_{Date.Today:yyyyMMdd}.csv", Me)
        AddHandler chkLowOnly.CheckedChanged, Sub() LoadStock()
        AddHandler gridStock.CellFormatting, AddressOf Stock_CellFormatting

        KeyPreview = True
        AddHandler KeyDown, Sub(s, e)
                                If e.KeyCode = Keys.Escape Then Close()
                            End Sub
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        LoadAll()
    End Sub

    Private ReadOnly Property RangeStart As Date
        Get
            Return dtpFrom.Value.Date
        End Get
    End Property

    ''' <summary>Exclusive end (the day after "To").</summary>
    Private ReadOnly Property RangeEnd As Date
        Get
            Return dtpTo.Value.Date.AddDays(1)
        End Get
    End Property

    Private Sub SetRange(dFrom As Date, dTo As Date)
        dtpFrom.Value = dFrom
        dtpTo.Value = dTo
        LoadAll()
    End Sub

    Private Sub LoadAll()
        If dtpFrom.Value.Date > dtpTo.Value.Date Then
            Ui.ShowError("'From' date must be before 'To' date.")
            Return
        End If
        LoadInvoices()
        LoadProducts()
        LoadStock()
    End Sub

    Private Const InvoiceSql As String =
        "SELECT s.id, s.invoice_no, s.sale_date, COALESCE(u.full_name, u.username, '') AS cashier, s.customer_name,
                (SELECT COUNT(*) FROM sale_items si WHERE si.sale_id = s.id) AS item_count,
                s.sub_total, s.discount, s.total, s.due, s.payment_method
         FROM sales s LEFT JOIN users u ON u.id = s.user_id "

    Private Sub LoadInvoices()
        gridInvoices.DataSource = Db.Query(InvoiceSql & "WHERE s.sale_date >= @f AND s.sale_date < @t ORDER BY s.id DESC",
                                           Db.P("@f", Db.DateKey(RangeStart)), Db.P("@t", Db.DateKey(RangeEnd)))
        LoadItems()
        UpdateSummary()
    End Sub

    Private Sub UpdateSummary()
        Dim s = ReportsRepo.GetSummary(RangeStart, RangeEnd)
        Dim methods = Db.Query("SELECT payment_method, SUM(total) AS amount FROM sales
                                WHERE sale_date >= @f AND sale_date < @t GROUP BY payment_method ORDER BY amount DESC",
                               Db.P("@f", Db.DateKey(RangeStart)), Db.P("@t", Db.DateKey(RangeEnd)))
        Dim parts As New List(Of String) From {
            $"Bills: {s.Invoices:N0}",
            $"Gross: {AppSettings.Money(s.Gross)}",
            $"Discount: {AppSettings.Money(s.Discount)}",
            $"Net Sales: {AppSettings.Money(s.Net)}"
        }
        If Session.IsAdmin Then parts.Add($"Profit: {AppSettings.Money(s.Profit)}")
        If s.Due > 0 Then parts.Add($"Udhaar given: {AppSettings.Money(s.Due)}")
        For Each r As DataRow In methods.Rows
            parts.Add($"{Ui.ToStr(r("payment_method"))}: {AppSettings.Money(Ui.ToDec(r("amount")))}")
        Next
        lblSummary.Text = String.Join("     |     ", parts)
    End Sub

    Private Sub LoadItems()
        Dim row = Ui.SelectedRow(gridInvoices)
        Dim saleId = If(row Is Nothing, 0L, Ui.ToLng(row("id")))
        gridItems.DataSource = Db.Query("SELECT barcode, product_name, qty, unit_price, line_total,
                                                line_total - cost_price * qty AS profit
                                         FROM sale_items WHERE sale_id = @id ORDER BY id", Db.P("@id", saleId))
    End Sub

    Private Sub LoadProducts()
        gridProducts.DataSource = Db.Query(
            "SELECT si.barcode, si.product_name, SUM(si.qty) AS qty, SUM(si.line_total) AS revenue,
                    SUM(si.cost_price * si.qty) AS cost, SUM(si.line_total - si.cost_price * si.qty) AS profit
             FROM sale_items si JOIN sales s ON s.id = si.sale_id
             WHERE s.sale_date >= @f AND s.sale_date < @t
             GROUP BY si.product_id, si.barcode, si.product_name
             ORDER BY revenue DESC",
            Db.P("@f", Db.DateKey(RangeStart)), Db.P("@t", Db.DateKey(RangeEnd)))
    End Sub

    Private Sub LoadStock()
        Dim sql = "SELECT p.barcode, p.name, COALESCE(c.name, '') AS category, p.stock, p.reorder_level,
                          p.stock * p.cost_price AS cost_value, p.stock * p.sale_price AS retail_value
                   FROM products p LEFT JOIN categories c ON c.id = p.category_id
                   WHERE p.is_active = 1"
        If chkLowOnly.Checked Then sql &= " AND p.stock <= p.reorder_level"
        Dim dt = Db.Query(sql & " ORDER BY p.name")
        gridStock.DataSource = dt

        Dim costValue = 0D, retailValue = 0D
        For Each r As DataRow In dt.Rows
            costValue += Ui.ToDec(r("cost_value"))
            retailValue += Ui.ToDec(r("retail_value"))
        Next
        lblStockSummary.Text = $"{dt.Rows.Count} products     Stock value (sale): {AppSettings.Money(retailValue)}" &
                               If(Session.IsAdmin, $"     (cost): {AppSettings.Money(costValue)}", "")
    End Sub

    Private Sub Stock_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 Then Return
        Dim row = TryCast(gridStock.Rows(e.RowIndex).DataBoundItem, DataRowView)
        If row IsNot Nothing AndAlso Ui.ToDec(row("stock")) <= Ui.ToDec(row("reorder_level")) Then
            e.CellStyle.BackColor = Ui.LowStockColor
            e.CellStyle.ForeColor = Ui.DangerColor
        End If
    End Sub

    Private Sub FindInvoice()
        Dim inv = txtInvoice.Text.Trim()
        If inv = "" Then Return
        Dim dt = Db.Query(InvoiceSql & "WHERE s.invoice_no = @inv", Db.P("@inv", inv))
        If dt.Rows.Count = 0 Then
            Ui.ShowError($"Invoice '{inv}' not found.")
            txtInvoice.SelectAll()
            Return
        End If
        tabs.SelectedIndex = 0
        gridInvoices.DataSource = dt
        LoadItems()
        lblSummary.Text = $"Showing invoice {inv}. Click Show to return to the date range."
        txtInvoice.Clear()
    End Sub

    Private Function SelectedSaleId() As Long
        Dim row = Ui.SelectedRow(gridInvoices)
        If row Is Nothing Then
            Ui.ShowInfo("Select an invoice first.")
            Return 0
        End If
        Return Ui.ToLng(row("id"))
    End Function

    Private Sub PrintSelected(preview As Boolean)
        Dim id = SelectedSaleId()
        If id <> 0 Then ReceiptPrinter.PrintSale(id, preview, Me)
    End Sub

    Private Sub VoidSelected()
        If Not Session.IsAdmin Then Return
        Dim row = Ui.SelectedRow(gridInvoices)
        If row Is Nothing Then Return
        If Not Ui.Confirm($"Void invoice {Ui.ToStr(row("invoice_no"))} of {AppSettings.Money(Ui.ToDec(row("total")))}?{vbCrLf}" &
                          "All its items will be returned to stock. This cannot be undone.") Then Return
        Try
            SalesService.VoidSale(Ui.ToLng(row("id")))
        Catch ex As Exception
            Ui.ShowError("Could not void the sale: " & ex.Message)
        End Try
        LoadAll()
    End Sub

End Class
