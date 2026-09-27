Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms
Imports Microsoft.Data.Sqlite

''' <summary>Product list: search / scan, add, edit, delete and print barcode labels.</summary>
<DesignerCategory("Code")>
Public Class ProductsForm
    Inherits Form

    Private ReadOnly txtSearch As New TextBox With {.Width = 260}
    Private ReadOnly cboCategory As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 180}
    Private ReadOnly chkLowOnly As New CheckBox With {.Text = "Low stock only", .AutoSize = True, .Margin = New Padding(10, 10, 4, 4)}
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblCount As New Label With {.Dock = DockStyle.Bottom, .Height = 28, .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(10, 0, 0, 0)}
    Private ReadOnly searchTimer As New System.Windows.Forms.Timer With {.Interval = 300}

    Public Sub New()
        Ui.SetupForm(Me, "Products", 1150, 660)

        Dim filters = Ui.MakeToolbar()
        filters.Controls.Add(Ui.MakeLabel("Search / Scan barcode:"))
        filters.Controls.Add(txtSearch)
        filters.Controls.Add(Ui.MakeLabel("Category:"))
        filters.Controls.Add(cboCategory)
        filters.Controls.Add(chkLowOnly)
        txtSearch.Margin = New Padding(4, 8, 4, 4)
        cboCategory.Margin = New Padding(4, 8, 4, 4)

        Dim actions = Ui.MakeToolbar(54)
        Dim btnAdd = Ui.MakeButton("Add New (Ins)", Ui.SuccessColor, 140)
        Dim btnEdit = Ui.MakeButton("Edit (Enter)", Ui.InfoColor, 120)
        Dim btnDelete = Ui.MakeButton("Delete", Ui.DangerColor, 100)
        Dim btnLabels = Ui.MakeButton("Print Barcode Labels", Ui.PrimaryColor, 190)
        Dim btnExport = Ui.MakeButton("Export CSV", Ui.NeutralColor, 120)
        Dim btnClose = Ui.MakeButton("Close", Ui.NeutralColor, 100)
        actions.Controls.AddRange(New Control() {btnAdd, btnEdit, btnDelete, btnLabels, btnExport, btnClose})

        Dim canEdit = Session.IsAdmin
        btnAdd.Enabled = canEdit
        btnEdit.Enabled = canEdit
        btnDelete.Enabled = canEdit

        Ui.StyleGrid(grid)
        Ui.AddColumn(grid, "id", "ID", visible:=False)
        Ui.AddColumn(grid, "barcode", "Barcode", fillWeight:=110)
        Ui.AddColumn(grid, "name", "Product Name", fillWeight:=220)
        Ui.AddColumn(grid, "category", "Category", fillWeight:=110)
        Ui.AddColumn(grid, "unit", "Unit", fillWeight:=50)
        Ui.AddColumn(grid, "cost_price", "Cost Price", "N2", 80, True, visible:=Session.IsAdmin)
        Ui.AddColumn(grid, "sale_price", "Sale Price", "N2", 80, True)
        Ui.AddColumn(grid, "stock", "Stock", "#,##0.###", 70, True)
        Ui.AddColumn(grid, "reorder_level", "Reorder At", "#,##0.###", 70, True)

        Controls.Add(grid)
        Controls.Add(lblCount)
        Controls.Add(actions)
        Controls.Add(filters)
        Controls.Add(Ui.MakeHeader("Products & Barcodes", "Red rows = low stock"))

        AddHandler btnAdd.Click, Sub() AddProduct("")
        AddHandler btnEdit.Click, Sub() EditSelected()
        AddHandler btnDelete.Click, Sub() DeleteSelected()
        AddHandler btnLabels.Click, Sub() PrintLabels()
        AddHandler btnExport.Click, Sub() Ui.ExportGridToCsv(grid, "products.csv", Me)
        AddHandler btnClose.Click, Sub() Close()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then EditSelected()
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        AddHandler grid.KeyDown, AddressOf Grid_KeyDown
        AddHandler txtSearch.TextChanged, Sub()
                                              searchTimer.Stop()
                                              searchTimer.Start()
                                          End Sub
        AddHandler txtSearch.KeyDown, AddressOf Search_KeyDown
        AddHandler searchTimer.Tick, Sub()
                                         searchTimer.Stop()
                                         LoadProducts()
                                     End Sub
        AddHandler cboCategory.SelectedIndexChanged, Sub() LoadProducts()
        AddHandler chkLowOnly.CheckedChanged, Sub() LoadProducts()

        KeyPreview = True
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        ComboItem.LoadCategories(cboCategory, "(All categories)")
        LoadProducts()
        txtSearch.Focus()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        If e.KeyCode = Keys.Insert AndAlso Session.IsAdmin Then
            AddProduct("")
            e.Handled = True
        ElseIf e.KeyCode = Keys.Escape Then
            Close()
        End If
    End Sub

    Private Sub LoadProducts()
        Dim sql = "SELECT p.id, p.barcode, p.name, COALESCE(c.name, '') AS category, p.unit, p.cost_price, p.sale_price,
                          p.stock, p.reorder_level
                   FROM products p LEFT JOIN categories c ON c.id = p.category_id
                   WHERE p.is_active = 1"
        Dim ps As New List(Of SqliteParameter)
        Dim q = txtSearch.Text.Trim()
        If q <> "" Then
            sql &= " AND (p.name LIKE @q OR p.barcode LIKE @q)"
            ps.Add(Db.P("@q", "%" & q & "%"))
        End If
        Dim catId = ComboItem.SelectedId(cboCategory)
        If catId > 0 Then
            sql &= " AND p.category_id = @c"
            ps.Add(Db.P("@c", catId))
        End If
        If chkLowOnly.Checked Then sql &= " AND p.stock <= p.reorder_level"
        sql &= " ORDER BY p.name"

        Dim dt = Db.Query(sql, ps.ToArray())
        grid.DataSource = dt
        lblCount.Text = $"{dt.Rows.Count} product(s)"
    End Sub

    Private Sub Search_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Down Then
            grid.Focus()
            e.Handled = True
            Return
        End If
        If e.KeyCode <> Keys.Enter Then Return
        e.SuppressKeyPress = True
        searchTimer.Stop()

        ' A scanned barcode ends with Enter: jump straight to that product.
        Dim code = txtSearch.Text.Trim()
        If code = "" Then Return
        Dim product = ProductRepo.FindByBarcode(code)
        If product IsNot Nothing Then
            txtSearch.Clear()
            LoadProducts()
            SelectProduct(Ui.ToLng(product("id")))
            If Session.IsAdmin Then EditSelected()
        Else
            LoadProducts()
            If grid.Rows.Count = 0 AndAlso Session.IsAdmin AndAlso Code128.IsEncodable(code) AndAlso Not code.Contains(" "c) AndAlso
               Ui.Confirm($"No product has barcode '{code}'.{vbCrLf}Add a new product with this barcode?") Then
                txtSearch.Clear()
                AddProduct(code)
            End If
        End If
    End Sub

    Private Sub Grid_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            If Session.IsAdmin Then EditSelected()
        ElseIf e.KeyCode = Keys.Delete AndAlso Session.IsAdmin Then
            DeleteSelected()
        End If
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 Then Return
        Dim row = TryCast(grid.Rows(e.RowIndex).DataBoundItem, DataRowView)
        If row Is Nothing Then Return
        If Ui.ToDec(row("stock")) <= Ui.ToDec(row("reorder_level")) Then
            e.CellStyle.BackColor = Ui.LowStockColor
            e.CellStyle.ForeColor = Ui.DangerColor
        End If
    End Sub

    Private Function SelectedId() As Long
        Dim row = Ui.SelectedRow(grid)
        Return If(row Is Nothing, 0L, Ui.ToLng(row("id")))
    End Function

    Private Sub SelectProduct(id As Long)
        For Each r As DataGridViewRow In grid.Rows
            Dim drv = TryCast(r.DataBoundItem, DataRowView)
            If drv IsNot Nothing AndAlso Ui.ToLng(drv("id")) = id Then
                grid.CurrentCell = r.Cells("barcode")
                Return
            End If
        Next
    End Sub

    Private Sub AddProduct(barcode As String)
        Using f As New ProductEditForm(0, barcode)
            If f.ShowDialog(Me) = DialogResult.OK Then
                LoadProducts()
                SelectProduct(f.SavedProductId)
            End If
        End Using
    End Sub

    Private Sub EditSelected()
        Dim id = SelectedId()
        If id = 0 OrElse Not Session.IsAdmin Then Return
        Using f As New ProductEditForm(id)
            If f.ShowDialog(Me) = DialogResult.OK Then
                LoadProducts()
                SelectProduct(f.SavedProductId)
            End If
        End Using
    End Sub

    Private Sub DeleteSelected()
        Dim row = Ui.SelectedRow(grid)
        If row Is Nothing Then Return
        Dim id = Ui.ToLng(row("id"))
        If Not Ui.Confirm($"Delete product '{Ui.ToStr(row("name"))}'?") Then Return

        Dim used = Ui.ToLng(Db.Scalar("SELECT (SELECT COUNT(*) FROM sale_items WHERE product_id = @id) +
                                              (SELECT COUNT(*) FROM stock_in WHERE product_id = @id)", Db.P("@id", id)))
        If used > 0 Then
            ' Keep history intact: hide the product instead of removing it.
            Db.Execute("UPDATE products SET is_active = 0 WHERE id = @id", Db.P("@id", id))
        Else
            Db.Execute("DELETE FROM products WHERE id = @id", Db.P("@id", id))
        End If
        LoadProducts()
    End Sub

    Private Sub PrintLabels()
        Dim id = SelectedId()
        If id = 0 Then
            Ui.ShowInfo("Select a product first.")
            Return
        End If
        Using f As New BarcodeLabelForm(id)
            f.ShowDialog(Me)
        End Using
    End Sub

End Class
