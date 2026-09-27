Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Media
Imports System.Windows.Forms

''' <summary>Receive stock from suppliers by scanning each product's barcode.</summary>
<DesignerCategory("Code")>
Public Class StockInForm
    Inherits Form

    Private _productId As Long

    Private ReadOnly txtScan As New TextBox With {.Font = New Font("Segoe UI", 14.0F), .Width = 330, .Margin = New Padding(4, 6, 4, 4)}
    Private ReadOnly lblProduct As New Label With {.AutoSize = True, .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold), .ForeColor = Ui.PrimaryColor, .Text = "-"}
    Private ReadOnly lblBarcode As New Label With {.AutoSize = True, .Text = "-"}
    Private ReadOnly lblStock As New Label With {.AutoSize = True, .Text = "-"}
    Private ReadOnly numQty As NumericUpDown = Ui.MakeNumber(3, 0D, 10000000D)
    Private ReadOnly numCost As NumericUpDown = Ui.MakeNumber(2)
    Private ReadOnly numPrice As NumericUpDown = Ui.MakeNumber(2)
    Private ReadOnly txtSupplier As New TextBox()
    Private ReadOnly txtNote As New TextBox()
    Private ReadOnly lblStatus As New Label With {.Dock = DockStyle.Bottom, .Height = 30, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold), .Padding = New Padding(10, 0, 0, 0), .TextAlign = ContentAlignment.MiddleLeft}
    Private ReadOnly grid As New DataGridView()

    Public Sub New()
        Ui.SetupForm(Me, "Stock In / Purchase", 1150, 660)
        KeyPreview = True

        Dim scanBar As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 56, .BackColor = Color.White, .Padding = New Padding(8, 4, 8, 4), .WrapContents = False}
        Dim btnFind = Ui.MakeButton("Find by Name (F3)", Ui.InfoColor, 170, 38)
        Dim btnCamera = Ui.MakeButton("Camera (F8)", Ui.PrimaryColor, 130, 38)
        Dim btnNew = Ui.MakeButton("New Product", Ui.SuccessColor, 130, 38)
        btnNew.Enabled = Session.IsAdmin
        scanBar.Controls.Add(Ui.MakeLabel("Scan Barcode (F2):", True, 11.0F))
        scanBar.Controls.Add(txtScan)
        scanBar.Controls.Add(btnFind)
        scanBar.Controls.Add(btnCamera)
        scanBar.Controls.Add(btnNew)

        Dim fields As New FieldTable(120)
        fields.AddRow("Product", lblProduct)
        fields.AddRow("Barcode", lblBarcode)
        fields.AddRow("Current Stock", lblStock)
        fields.AddRow("Qty Received *", numQty)
        fields.AddRow("Cost Price", numCost)
        fields.AddRow("Sale Price", numPrice)
        fields.AddRow("Supplier", txtSupplier)
        fields.AddRow("Note / Bill #", txtNote)

        Dim btnSave = Ui.MakeButton("Add to Stock (F9)", Ui.SuccessColor, 180, 44)
        Dim btnLabels = Ui.MakeButton("Print Labels", Ui.PrimaryColor, 130, 44)
        Dim btnClear = Ui.MakeButton("Clear", Ui.NeutralColor, 90, 44)
        Dim formButtons As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 60, .Padding = New Padding(10, 4, 4, 4)}
        formButtons.Controls.AddRange(New Control() {btnSave, btnLabels, btnClear})

        Dim left As New Panel With {.Dock = DockStyle.Left, .Width = 440, .BackColor = Color.White, .Padding = New Padding(4)}
        left.Controls.Add(formButtons)
        left.Controls.Add(fields)

        Ui.StyleGrid(grid)
        Ui.AddColumn(grid, "created_at", "Date / Time", fillWeight:=110)
        Ui.AddColumn(grid, "barcode", "Barcode", fillWeight:=100)
        Ui.AddColumn(grid, "name", "Product", fillWeight:=170)
        Ui.AddColumn(grid, "qty", "Qty", "#,##0.###", 60, True)
        Ui.AddColumn(grid, "cost_price", "Cost", "N2", 70, True)
        Ui.AddColumn(grid, "supplier", "Supplier", fillWeight:=100)
        Ui.AddColumn(grid, "note", "Note", fillWeight:=110)
        Ui.AddColumn(grid, "user_name", "By", fillWeight:=70)

        Dim right As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8, 0, 0, 0)}
        right.Controls.Add(Ui.Captioned("Recent stock entries", grid))

        Dim body As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
        body.Controls.Add(right)
        body.Controls.Add(left)

        Controls.Add(body)
        Controls.Add(lblStatus)
        Controls.Add(scanBar)
        Controls.Add(Ui.MakeHeader("Stock In (Purchase)", "Scan product -> enter qty -> F9"))

        AddHandler txtScan.KeyDown, Sub(s, e)
                                        If e.KeyCode = Keys.Enter Then
                                            e.SuppressKeyPress = True
                                            FindByBarcode()
                                        End If
                                    End Sub
        AddHandler btnFind.Click, Sub() FindByName()
        AddHandler btnCamera.Click, Sub() ScanWithCamera()
        AddHandler btnNew.Click, Sub() CreateProduct(txtScan.Text.Trim())
        AddHandler btnSave.Click, Sub() SaveStock()
        AddHandler btnLabels.Click, Sub() PrintLabels()
        AddHandler btnClear.Click, Sub() ClearProduct()

        ScannerInput.RedirectTypingTo(Me, txtScan)
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        LoadHistory()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        txtScan.Focus()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        Select Case e.KeyCode
            Case Keys.F2
                txtScan.Focus()
                txtScan.SelectAll()
            Case Keys.F3 : FindByName()
            Case Keys.F8 : ScanWithCamera()
            Case Keys.F9 : SaveStock()
            Case Keys.Escape : Close()
            Case Keys.Enter
                ' Enter jumps to the next field (the scan box handles its own Enter).
                Dim focused = ScannerInput.FocusedControl(Me)
                If focused Is txtScan OrElse TypeOf focused Is Button Then Return
                SelectNextControl(focused, True, True, True, True)
            Case Else : Return
        End Select
        e.Handled = True
        e.SuppressKeyPress = True
    End Sub

    Private Sub SetStatus(message As String, Optional isError As Boolean = False)
        lblStatus.Text = message
        lblStatus.ForeColor = If(isError, Ui.DangerColor, Ui.SuccessColor)
        If isError Then SystemSounds.Exclamation.Play()
    End Sub

    Private Sub FindByBarcode()
        Dim code = txtScan.Text.Trim()
        If code = "" Then Return
        Dim product = ProductRepo.FindByBarcode(code)
        If product IsNot Nothing Then
            ShowProduct(product)
            Return
        End If
        SetStatus($"No product with barcode {code}", isError:=True)
        If Session.IsAdmin AndAlso Ui.Confirm($"No product has barcode '{code}'.{vbCrLf}Create a new product now?") Then
            CreateProduct(code)
        End If
    End Sub

    Private Sub ScanWithCamera()
        Dim code = CameraScanForm.ScanOnce(Me)
        If code Is Nothing Then Return
        txtScan.Text = code
        FindByBarcode()
    End Sub

    Private Sub FindByName()
        Using dlg As New ProductSearchDialog()
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                Dim product = ProductRepo.GetById(dlg.SelectedProductId)
                If product IsNot Nothing Then ShowProduct(product)
            End If
        End Using
    End Sub

    Private Sub CreateProduct(barcode As String)
        Using f As New ProductEditForm(0, barcode)
            If f.ShowDialog(Me) = DialogResult.OK Then
                Dim product = ProductRepo.GetById(f.SavedProductId)
                If product IsNot Nothing Then ShowProduct(product)
            End If
        End Using
    End Sub

    Private Sub ShowProduct(product As DataRow)
        _productId = Ui.ToLng(product("id"))
        lblProduct.Text = Ui.ToStr(product("name"))
        lblBarcode.Text = Ui.ToStr(product("barcode"))
        lblStock.Text = AppSettings.Qty(Ui.ToDec(product("stock"))) & " " & Ui.ToStr(product("unit"))
        Ui.SetNumber(numCost, Ui.ToDec(product("cost_price")))
        Ui.SetNumber(numPrice, Ui.ToDec(product("sale_price")))
        numQty.Value = 0
        txtScan.Clear()
        SetStatus("Enter the quantity received and press F9.")
        numQty.Focus()
        numQty.Select(0, numQty.Text.Length)
    End Sub

    Private Sub ClearProduct()
        _productId = 0
        lblProduct.Text = "-"
        lblBarcode.Text = "-"
        lblStock.Text = "-"
        numQty.Value = 0
        numCost.Value = 0
        numPrice.Value = 0
        txtNote.Clear()
        txtScan.Clear()
        txtScan.Focus()
    End Sub

    Private Sub SaveStock()
        If _productId = 0 Then
            SetStatus("Scan or select a product first.", isError:=True)
            txtScan.Focus()
            Return
        End If
        Dim qty = numQty.Value
        If qty <= 0 Then
            SetStatus("Enter the quantity received.", isError:=True)
            numQty.Focus()
            Return
        End If

        Try
            SalesService.AddStock(_productId, qty, numCost.Value, numPrice.Value, txtSupplier.Text.Trim(), txtNote.Text.Trim())
        Catch ex As Exception
            Ui.ShowError("Stock could not be saved: " & ex.Message)
            Return
        End Try

        Dim p = ProductRepo.GetById(_productId)
        SetStatus($"Added {AppSettings.Qty(qty)} x {lblProduct.Text}.  New stock: {AppSettings.Qty(Ui.ToDec(p("stock")))}")
        LoadHistory()
        ' Keep the supplier for the next items of the same delivery.
        ClearProduct()
    End Sub

    Private Sub PrintLabels()
        If _productId = 0 Then
            SetStatus("Scan or select a product first.", isError:=True)
            Return
        End If
        Using f As New BarcodeLabelForm(_productId, Math.Max(1, CInt(Math.Ceiling(numQty.Value))))
            f.ShowDialog(Me)
        End Using
    End Sub

    Private Sub LoadHistory()
        grid.DataSource = Db.Query("SELECT si.created_at, p.barcode, p.name, si.qty, si.cost_price, si.supplier, si.note,
                                           COALESCE(u.full_name, u.username, '') AS user_name
                                    FROM stock_in si
                                    JOIN products p ON p.id = si.product_id
                                    LEFT JOIN users u ON u.id = si.user_id
                                    ORDER BY si.id DESC LIMIT 300")
    End Sub

End Class
