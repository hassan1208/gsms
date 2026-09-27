Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Drawing.Printing
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

''' <summary>Find a product by name (for items without a barcode sticker).</summary>
<DesignerCategory("Code")>
Public Class ProductSearchDialog
    Inherits Form

    Private ReadOnly txtFilter As New TextBox With {.Dock = DockStyle.Fill, .Font = New Font("Segoe UI", 13.0F)}
    Private ReadOnly grid As New DataGridView()

    Public Property SelectedProductId As Long

    Public Sub New(Optional initialText As String = "")
        Ui.SetupForm(Me, "Search Product", 760, 480, dialog:=True)
        FormBorderStyle = FormBorderStyle.Sizable

        Dim top As New Panel With {.Dock = DockStyle.Top, .Height = 48, .Padding = New Padding(10, 8, 10, 4)}
        top.Controls.Add(txtFilter)

        Ui.StyleGrid(grid)
        Ui.AddColumn(grid, "id", "ID", visible:=False)
        Ui.AddColumn(grid, "barcode", "Barcode", fillWeight:=100)
        Ui.AddColumn(grid, "name", "Product", fillWeight:=220)
        Ui.AddColumn(grid, "sale_price", "Price", "N2", 70, True)
        Ui.AddColumn(grid, "stock", "Stock", "#,##0.###", 60, True)

        Dim hint As New Label With {.Dock = DockStyle.Bottom, .Height = 28, .ForeColor = Ui.NeutralColor, .Padding = New Padding(10, 0, 0, 0),
                                    .Text = "Type name or barcode.  Down arrow = go to list,  Enter = select,  Esc = cancel", .TextAlign = ContentAlignment.MiddleLeft}
        Controls.Add(grid)
        Controls.Add(hint)
        Controls.Add(top)

        txtFilter.Text = initialText
        AddHandler txtFilter.TextChanged, Sub() LoadRows()
        AddHandler txtFilter.KeyDown, Sub(s, e)
                                          If e.KeyCode = Keys.Down Then
                                              grid.Focus()
                                              e.Handled = True
                                          ElseIf e.KeyCode = Keys.Enter Then
                                              e.SuppressKeyPress = True
                                              Choose()
                                          End If
                                      End Sub
        AddHandler grid.KeyDown, Sub(s, e)
                                     If e.KeyCode = Keys.Enter Then
                                         e.SuppressKeyPress = True
                                         Choose()
                                     End If
                                 End Sub
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then Choose()
        KeyPreview = True
        AddHandler KeyDown, Sub(s, e)
                                If e.KeyCode = Keys.Escape Then DialogResult = DialogResult.Cancel
                            End Sub
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        LoadRows()
        txtFilter.Focus()
        txtFilter.SelectionStart = txtFilter.TextLength
    End Sub

    Private Sub LoadRows()
        grid.DataSource = Db.Query("SELECT id, barcode, name, sale_price, stock FROM products
                                    WHERE is_active = 1 AND (name LIKE @q OR barcode LIKE @q)
                                    ORDER BY name LIMIT 300", Db.P("@q", "%" & txtFilter.Text.Trim() & "%"))
    End Sub

    Private Sub Choose()
        Dim row = Ui.SelectedRow(grid)
        If row Is Nothing Then Return
        SelectedProductId = Ui.ToLng(row("id"))
        DialogResult = DialogResult.OK
    End Sub

End Class

''' <summary>Print barcode stickers for a product.</summary>
<DesignerCategory("Code")>
Public Class BarcodeLabelForm
    Inherits Form

    Private ReadOnly job As New LabelJob()
    Private ReadOnly numCopies As NumericUpDown = Ui.MakeNumber(0, 1D, 1000D)
    Private ReadOnly cboLayout As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly chkPrice As New CheckBox With {.Text = "Show price", .Checked = True, .AutoSize = True}
    Private ReadOnly chkStore As New CheckBox With {.Text = "Show store name", .Checked = True, .AutoSize = True}
    Private ReadOnly picPreview As New PictureBox With {.Height = 210, .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.White}

    Public Sub New(productId As Long, Optional copies As Integer = 1)
        Ui.SetupForm(Me, "Print Barcode Labels", 480, 560, dialog:=True)

        Dim p = ProductRepo.GetById(productId)
        job.Barcode = Ui.ToStr(p("barcode"))
        job.ProductName = Ui.ToStr(p("name"))
        job.Price = Ui.ToDec(p("sale_price"))

        cboLayout.Items.AddRange(New Object() {"A4 sticker sheet (3 x 8 = 24 per page)", "Label printer roll (50 x 25 mm)"})
        cboLayout.SelectedIndex = 0
        Ui.SetNumber(numCopies, copies)

        Dim fields As New FieldTable(110)
        fields.AddRow("Product", New Label With {.Text = job.ProductName, .Font = New Font("Segoe UI", 10.0F, FontStyle.Bold), .AutoSize = True})
        fields.AddRow("Barcode", New Label With {.Text = job.Barcode, .AutoSize = True})
        fields.AddRow("Copies", numCopies)
        fields.AddRow("Layout", cboLayout)
        fields.AddRow("", chkPrice)
        fields.AddRow("", chkStore)
        fields.AddFull(New Label With {.Text = "Preview:", .AutoSize = True, .ForeColor = Ui.NeutralColor})
        fields.AddFull(picPreview)

        Dim btnPrint = Ui.MakeButton("Print", Ui.SuccessColor, 110)
        Dim btnPreview = Ui.MakeButton("Print Preview", Ui.InfoColor, 130)
        Dim btnClose = Ui.MakeButton("Close", Ui.NeutralColor, 100)
        btnClose.DialogResult = DialogResult.Cancel
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 56, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10, 6, 10, 6)}
        buttons.Controls.AddRange(New Control() {btnClose, btnPrint, btnPreview})

        Controls.Add(fields)
        Controls.Add(buttons)
        CancelButton = btnClose

        AddHandler chkPrice.CheckedChanged, Sub() UpdatePreview()
        AddHandler chkStore.CheckedChanged, Sub() UpdatePreview()
        AddHandler btnPrint.Click, Sub() DoPrint(False)
        AddHandler btnPreview.Click, Sub() DoPrint(True)
        UpdatePreview()
        Ui.FinishLayout(Me)
    End Sub

    Private Sub UpdateJob()
        job.Copies = CInt(numCopies.Value)
        job.ShowPrice = chkPrice.Checked
        job.ShowStoreName = chkStore.Checked
        job.Layout = If(cboLayout.SelectedIndex = 1, LabelLayout.LabelRoll50x25, LabelLayout.A4Sheet3x8)
    End Sub

    Private Sub UpdatePreview()
        UpdateJob()
        Dim old = picPreview.Image
        picPreview.Image = LabelPrinter.RenderPreview(job)
        old?.Dispose()
    End Sub

    Private Sub DoPrint(preview As Boolean)
        UpdateJob()
        Try
            Using doc = LabelPrinter.CreateDocument(job)
                If preview Then
                    Using dlg As New PrintPreviewDialog With {.Document = doc, .Width = 900, .Height = 700, .UseAntiAlias = True}
                        dlg.ShowDialog(Me)
                    End Using
                Else
                    Using pd As New PrintDialog With {.Document = doc, .UseEXDialog = True}
                        If pd.ShowDialog(Me) = DialogResult.OK Then doc.Print()
                    End Using
                End If
            End Using
        Catch ex As Exception
            Ui.ShowError("Printing failed: " & ex.Message)
        End Try
    End Sub

End Class

''' <summary>Manage product categories.</summary>
<DesignerCategory("Code")>
Public Class CategoriesForm
    Inherits Form

    Private ReadOnly grid As New DataGridView()

    Public Sub New()
        Ui.SetupForm(Me, "Categories", 520, 480, dialog:=True)
        FormBorderStyle = FormBorderStyle.Sizable

        Ui.StyleGrid(grid)
        Ui.AddColumn(grid, "id", "ID", visible:=False)
        Ui.AddColumn(grid, "name", "Category", fillWeight:=200)
        Ui.AddColumn(grid, "products", "Products", "N0", 70, True)

        Dim bar = Ui.MakeToolbar(54)
        Dim btnAdd = Ui.MakeButton("Add", Ui.SuccessColor, 90)
        Dim btnRename = Ui.MakeButton("Rename", Ui.InfoColor, 100)
        Dim btnDelete = Ui.MakeButton("Delete", Ui.DangerColor, 90)
        Dim btnClose = Ui.MakeButton("Close", Ui.NeutralColor, 90)
        btnClose.DialogResult = DialogResult.Cancel
        bar.Controls.AddRange(New Control() {btnAdd, btnRename, btnDelete, btnClose})

        Controls.Add(grid)
        Controls.Add(bar)
        CancelButton = btnClose

        AddHandler btnAdd.Click, Sub() AddCategory()
        AddHandler btnRename.Click, Sub() RenameCategory()
        AddHandler btnDelete.Click, Sub() DeleteCategory()
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        LoadRows()
    End Sub

    Private Sub LoadRows()
        grid.DataSource = Db.Query("SELECT c.id, c.name, (SELECT COUNT(*) FROM products p WHERE p.category_id = c.id AND p.is_active = 1) AS products
                                    FROM categories c ORDER BY c.name")
    End Sub

    Private Sub AddCategory()
        Dim name = Interaction.InputBox("Category name:", "Add Category").Trim()
        If name = "" Then Return
        Try
            Db.Execute("INSERT INTO categories(name) VALUES(@n)", Db.P("@n", name))
        Catch ex As Microsoft.Data.Sqlite.SqliteException
            Ui.ShowError("This category already exists.")
        End Try
        LoadRows()
    End Sub

    Private Sub RenameCategory()
        Dim row = Ui.SelectedRow(grid)
        If row Is Nothing Then Return
        Dim name = Interaction.InputBox("New name:", "Rename Category", Ui.ToStr(row("name"))).Trim()
        If name = "" Then Return
        Try
            Db.Execute("UPDATE categories SET name = @n WHERE id = @id", Db.P("@n", name), Db.P("@id", row("id")))
        Catch ex As Microsoft.Data.Sqlite.SqliteException
            Ui.ShowError("A category with this name already exists.")
        End Try
        LoadRows()
    End Sub

    Private Sub DeleteCategory()
        Dim row = Ui.SelectedRow(grid)
        If row Is Nothing Then Return
        If Not Ui.Confirm($"Delete category '{Ui.ToStr(row("name"))}'? Its products will become uncategorised.") Then Return
        Db.Execute("DELETE FROM categories WHERE id = @id", Db.P("@id", row("id")))
        LoadRows()
    End Sub

End Class
