Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

''' <summary>Add / edit a product. The barcode can be scanned, typed or generated.</summary>
<DesignerCategory("Code")>
Public Class ProductEditForm
    Inherits Form

    Private _productId As Long
    Private _originalStock As Decimal

    Private ReadOnly txtBarcode As New TextBox()
    Private ReadOnly txtName As New TextBox()
    Private ReadOnly cboCategory As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly cboUnit As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDown}
    Private ReadOnly numCost As NumericUpDown = Ui.MakeNumber(2)
    Private ReadOnly numPrice As NumericUpDown = Ui.MakeNumber(2)
    Private ReadOnly lblMargin As New Label With {.AutoSize = True, .ForeColor = Ui.NeutralColor}
    Private ReadOnly numStock As NumericUpDown = Ui.MakeNumber(3, -1000000D)
    Private ReadOnly numReorder As NumericUpDown = Ui.MakeNumber(3)
    Private ReadOnly picBarcode As New PictureBox With {.Height = 110, .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.White, .BorderStyle = BorderStyle.FixedSingle}

    Public ReadOnly Property SavedProductId As Long
        Get
            Return _productId
        End Get
    End Property

    Public Sub New(Optional productId As Long = 0, Optional presetBarcode As String = "")
        _productId = productId
        Ui.SetupForm(Me, If(productId = 0, "Add Product", "Edit Product"), 540, 620, dialog:=True)
        KeyPreview = True

        cboUnit.Items.AddRange(New Object() {"pcs", "kg", "g", "ltr", "ml", "dozen", "pack", "box", "carton", "meter"})
        numReorder.Value = 5

        Dim btnGenerate = Ui.MakeButton("Generate", Ui.InfoColor, 100, 30)
        Dim btnAddCategory = Ui.MakeButton("+ New", Ui.SuccessColor, 80, 30)

        Dim fields As New FieldTable(130)
        fields.AddRow("Barcode *", FieldTable.WithButton(txtBarcode, btnGenerate))
        fields.AddFull(New Label With {.Text = "Scan the product's barcode with the scanner, or click Generate for loose / unbarcoded items.",
                                       .ForeColor = Ui.NeutralColor, .AutoSize = True, .Font = New Font("Segoe UI", 8.5F)})
        fields.AddRow("Product Name *", txtName)
        fields.AddRow("Category", FieldTable.WithButton(cboCategory, btnAddCategory))
        fields.AddRow("Unit", cboUnit)
        fields.AddRow("Cost Price", numCost)
        fields.AddRow("Sale Price *", numPrice)
        fields.AddRow("", lblMargin)
        fields.AddRow(If(productId = 0, "Opening Stock", "Current Stock"), numStock)
        fields.AddRow("Reorder Level", numReorder)
        fields.AddFull(picBarcode)

        Dim btnSave = Ui.MakeButton("Save (Ctrl+S)", Ui.SuccessColor, 140)
        Dim btnCancel = Ui.MakeButton("Cancel", Ui.NeutralColor, 110)
        btnCancel.DialogResult = DialogResult.Cancel
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 56, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10, 6, 10, 6)}
        buttons.Controls.Add(btnCancel)
        buttons.Controls.Add(btnSave)

        Controls.Add(fields)
        Controls.Add(buttons)
        CancelButton = btnCancel
        ' No AcceptButton on purpose: scanners press Enter after the code, which must not save the form.

        AddHandler btnGenerate.Click, Sub()
                                          txtBarcode.Text = BarcodeHelper.GenerateUniqueBarcode()
                                          txtName.Focus()
                                      End Sub
        AddHandler btnAddCategory.Click, Sub() AddCategory()
        AddHandler btnSave.Click, Sub() SaveProduct()
        AddHandler txtBarcode.TextChanged, Sub() UpdateBarcodePreview()
        AddHandler numCost.ValueChanged, Sub() UpdateMargin()
        AddHandler numPrice.ValueChanged, Sub() UpdateMargin()

        ComboItem.LoadCategories(cboCategory, "(None)")
        cboUnit.Text = "pcs"
        If productId <> 0 Then LoadProduct(productId) Else txtBarcode.Text = presetBarcode
        UpdateMargin()
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        If txtBarcode.Text = "" Then txtBarcode.Focus() Else txtName.Focus()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.KeyCode = Keys.S AndAlso e.Control Then
            e.SuppressKeyPress = True
            SaveProduct()
        ElseIf e.KeyCode = Keys.Enter AndAlso Not TypeOf ScannerInput.FocusedControl(Me) Is Button Then
            ' Enter (typed or sent by the scanner) moves to the next field.
            e.SuppressKeyPress = True
            SelectNextControl(ScannerInput.FocusedControl(Me), True, True, True, True)
        End If
    End Sub

    Private Sub LoadProduct(id As Long)
        Dim r = ProductRepo.GetById(id)
        If r Is Nothing Then Return
        _productId = id
        txtBarcode.Text = Ui.ToStr(r("barcode"))
        txtName.Text = Ui.ToStr(r("name"))
        ComboItem.Select(cboCategory, Ui.ToLng(r("category_id")))
        cboUnit.Text = Ui.ToStr(r("unit"))
        Ui.SetNumber(numCost, Ui.ToDec(r("cost_price")))
        Ui.SetNumber(numPrice, Ui.ToDec(r("sale_price")))
        _originalStock = Ui.ToDec(r("stock"))
        Ui.SetNumber(numStock, _originalStock)
        Ui.SetNumber(numReorder, Ui.ToDec(r("reorder_level")))
        Text = "Edit Product"
    End Sub

    Private Sub UpdateBarcodePreview()
        Dim old = picBarcode.Image
        Dim code = txtBarcode.Text.Trim()
        picBarcode.Image = If(Code128.IsEncodable(code), Code128.Render(code), Nothing)
        old?.Dispose()
    End Sub

    Private Sub UpdateMargin()
        Dim cost = numCost.Value, price = numPrice.Value
        If price <= 0 Then
            lblMargin.Text = ""
        Else
            Dim profit = price - cost
            lblMargin.Text = $"Profit per unit: {AppSettings.Money(profit)}  ({profit / price * 100D:N1}% margin)"
            lblMargin.ForeColor = If(profit < 0, Ui.DangerColor, Ui.SuccessColor)
        End If
    End Sub

    Private Sub AddCategory()
        Dim name = Interaction.InputBox("New category name:", "Add Category").Trim()
        If name = "" Then Return
        Db.Execute("INSERT OR IGNORE INTO categories(name) VALUES(@n)", Db.P("@n", name))
        Dim id = Ui.ToLng(Db.Scalar("SELECT id FROM categories WHERE name = @n", Db.P("@n", name)))
        ComboItem.LoadCategories(cboCategory, "(None)", id)
    End Sub

    Private Sub SaveProduct()
        Dim barcode = txtBarcode.Text.Trim()
        Dim name = txtName.Text.Trim()

        If barcode = "" Then
            Ui.ShowError("Barcode is required. Scan it or click Generate.")
            txtBarcode.Focus()
            Return
        End If
        If Not Code128.IsEncodable(barcode) Then
            Ui.ShowError("Barcode contains invalid characters.")
            txtBarcode.Focus()
            Return
        End If
        If name = "" Then
            Ui.ShowError("Product name is required.")
            txtName.Focus()
            Return
        End If

        Dim existing = Db.Query("SELECT id, name, is_active FROM products WHERE barcode = @b AND id <> @id",
                                Db.P("@b", barcode), Db.P("@id", _productId))
        If existing.Rows.Count > 0 Then
            Dim other = existing.Rows(0)
            If Ui.ToLng(other("is_active")) = 1 Then
                Ui.ShowError($"This barcode already belongs to '{Ui.ToStr(other("name"))}'.")
                Return
            End If
            If _productId = 0 AndAlso Ui.Confirm($"This barcode belongs to deleted product '{Ui.ToStr(other("name"))}'.{vbCrLf}Restore that product instead?") Then
                Dim otherId = Ui.ToLng(other("id"))
                Db.Execute("UPDATE products SET is_active = 1 WHERE id = @id", Db.P("@id", otherId))
                LoadProduct(otherId)
                Ui.ShowInfo("Product restored. Review the details and click Save.")
            End If
            Return
        End If

        If numPrice.Value <= 0 AndAlso Not Ui.Confirm("Sale price is zero. Continue?") Then Return
        If numPrice.Value < numCost.Value AndAlso Not Ui.Confirm("Sale price is less than cost price. Continue?") Then Return

        Dim catId = ComboItem.SelectedId(cboCategory)
        Dim unit = If(cboUnit.Text.Trim() = "", "pcs", cboUnit.Text.Trim())

        Dim savedId = _productId
        Using cn = Db.Open(), tx = cn.BeginTransaction()
            Dim ps = Function() New Microsoft.Data.Sqlite.SqliteParameter() {
                Db.P("@b", barcode), Db.P("@n", name), Db.P("@c", If(catId = 0, Nothing, CObj(catId))), Db.P("@u", unit),
                Db.P("@cp", numCost.Value), Db.P("@sp", numPrice.Value), Db.P("@st", numStock.Value),
                Db.P("@r", numReorder.Value), Db.P("@id", _productId), Db.P("@now", Db.NowText())}

            Dim stockNote As String = Nothing
            Dim stockChange As Decimal
            If savedId = 0 Then
                savedId = Ui.ToLng(Db.ScalarTx(cn, tx,
                    "INSERT INTO products(barcode, name, category_id, unit, cost_price, sale_price, stock, reorder_level, created_at)
                     VALUES(@b, @n, @c, @u, @cp, @sp, @st, @r, @now); SELECT last_insert_rowid();", ps()))
                stockChange = numStock.Value
                stockNote = "Opening stock"
            Else
                ' Stock is adjusted relative to the latest value, so sales made meanwhile are not lost.
                Dim current = Ui.ToDec(Db.ScalarTx(cn, tx, "SELECT stock FROM products WHERE id = @id", Db.P("@id", _productId)))
                stockChange = numStock.Value - _originalStock
                Db.ExecuteTx(cn, tx,
                    "UPDATE products SET barcode = @b, name = @n, category_id = @c, unit = @u, cost_price = @cp,
                                         sale_price = @sp, reorder_level = @r WHERE id = @id", ps())
                If stockChange <> 0 Then
                    Db.ExecuteTx(cn, tx, "UPDATE products SET stock = @s WHERE id = @id",
                                 Db.P("@s", current + stockChange), Db.P("@id", _productId))
                End If
                stockNote = "Manual stock adjustment"
            End If

            If stockChange <> 0 Then
                Db.ExecuteTx(cn, tx,
                    "INSERT INTO stock_in(product_id, qty, cost_price, supplier, note, user_id, created_at)
                     VALUES(@pid, @q, @c, NULL, @note, @user, @now)",
                    Db.P("@pid", savedId), Db.P("@q", stockChange), Db.P("@c", numCost.Value),
                    Db.P("@note", stockNote), Db.P("@user", Session.UserId), Db.P("@now", Db.NowText()))
            End If
            tx.Commit()
        End Using

        _productId = savedId
        DialogResult = DialogResult.OK
    End Sub

End Class
