Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Main window: navigation menu + today's dashboard.</summary>
<DesignerCategory("Code")>
Public Class MainForm
    Inherits Form

    Public Property LogoutRequested As Boolean

    Private ReadOnly lblSalesToday As Label
    Private ReadOnly lblInvoicesToday As Label
    Private ReadOnly lblProfitToday As Label
    Private ReadOnly lblProducts As Label
    Private ReadOnly lblLowStock As Label
    Private ReadOnly lblClock As New Label With {
        .Dock = DockStyle.Right, .Width = 460, .ForeColor = Color.White,
        .TextAlign = ContentAlignment.MiddleRight, .Padding = New Padding(0, 0, 16, 0)
    }
    Private ReadOnly gridLow As New DataGridView()
    Private ReadOnly gridRecent As New DataGridView()
    Private ReadOnly clockTimer As New System.Windows.Forms.Timer With {.Interval = 1000}

    Public Sub New()
        Ui.SetupForm(Me, AppSettings.StoreName & " - General Store Management System", 1200, 720)
        WindowState = FormWindowState.Maximized
        KeyPreview = True

        ' ---- Header ----
        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 60, .BackColor = Ui.PrimaryColor}
        header.Controls.Add(New Label With {
            .Text = AppSettings.StoreName, .Dock = DockStyle.Fill, .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 18.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(16, 0, 0, 0)
        })
        header.Controls.Add(lblClock)

        ' ---- Left menu ----
        Dim menu As New FlowLayoutPanel With {
            .Dock = DockStyle.Left, .Width = 236, .FlowDirection = FlowDirection.TopDown, .WrapContents = False,
            .BackColor = Color.FromArgb(34, 45, 60), .Padding = New Padding(12, 16, 12, 12), .AutoScroll = True
        }
        AddMenuButton(menu, "New Sale / POS      F1", AddressOf OpenPos, Ui.SuccessColor)
        AddMenuButton(menu, "Products               F2", AddressOf OpenProducts)
        AddMenuButton(menu, "Stock In (Purchase)  F3", AddressOf OpenStockIn)
        AddMenuButton(menu, "Sales Reports        F4", AddressOf OpenReports)
        If Session.IsAdmin Then
            AddMenuButton(menu, "Categories", AddressOf OpenCategories)
            AddMenuButton(menu, "Users", AddressOf OpenUsers)
            AddMenuButton(menu, "Settings / Backup", AddressOf OpenSettings)
        End If
        AddMenuButton(menu, "Change Password", AddressOf OpenChangePassword)
        AddMenuButton(menu, "Logout", AddressOf DoLogout, Ui.WarningColor)
        AddMenuButton(menu, "Exit", AddressOf Close, Ui.DangerColor)

        ' ---- Dashboard ----
        Dim content As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(16)}
        Dim tiles As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 124, .WrapContents = False, .AutoScroll = True}
        lblSalesToday = AddTile(tiles, "Today's Sales", Ui.SuccessColor)
        lblInvoicesToday = AddTile(tiles, "Today's Bills", Ui.InfoColor)
        lblProfitToday = AddTile(tiles, "Today's Profit", Color.FromArgb(142, 68, 173))
        lblProducts = AddTile(tiles, "Total Products", Ui.PrimaryColor)
        lblLowStock = AddTile(tiles, "Low Stock Items", Ui.DangerColor)
        lblProfitToday.Parent.Visible = Session.IsAdmin

        Ui.StyleGrid(gridLow)
        Ui.AddColumn(gridLow, "barcode", "Barcode", fillWeight:=90)
        Ui.AddColumn(gridLow, "name", "Product", fillWeight:=160)
        Ui.AddColumn(gridLow, "stock", "Stock", "#,##0.###", 60, True)
        Ui.AddColumn(gridLow, "reorder_level", "Reorder At", "#,##0.###", 60, True)

        Ui.StyleGrid(gridRecent)
        Ui.AddColumn(gridRecent, "invoice_no", "Invoice", fillWeight:=100)
        Ui.AddColumn(gridRecent, "sale_date", "Date / Time", fillWeight:=110)
        Ui.AddColumn(gridRecent, "cashier", "Cashier", fillWeight:=80)
        Ui.AddColumn(gridRecent, "total", "Total", "N2", 70, True)

        Dim lists As New TableLayoutPanel With {.Dock = DockStyle.Fill, .ColumnCount = 2, .RowCount = 1, .Padding = New Padding(0, 12, 0, 0)}
        lists.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        lists.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 50.0F))
        lists.RowStyles.Add(New RowStyle(SizeType.Percent, 100.0F))
        lists.Controls.Add(Ui.Captioned("Low Stock Alert (reorder these)", gridLow), 0, 0)
        lists.Controls.Add(Ui.Captioned("Recent Sales", gridRecent), 1, 0)

        Dim footer As New Label With {
            .Dock = DockStyle.Bottom, .Height = 26, .ForeColor = Ui.NeutralColor, .TextAlign = ContentAlignment.MiddleLeft,
            .Text = "Tip: connect any USB barcode scanner and open New Sale (F1) - just scan products to bill them."
        }

        content.Controls.Add(lists)
        content.Controls.Add(footer)
        content.Controls.Add(tiles)

        Controls.Add(content)
        Controls.Add(menu)
        Controls.Add(header)

        AddHandler clockTimer.Tick, Sub() UpdateClock()
        AddHandler gridRecent.CellDoubleClick, Sub() OpenReports()
        Ui.FinishLayout(Me)
    End Sub

    Private Function AddTile(parent As Control, title As String, tileColor As Color) As Label
        Dim tile As New Panel With {.Size = New Size(230, 108), .BackColor = tileColor, .Margin = New Padding(0, 0, 14, 0)}
        Dim value As New Label With {
            .Dock = DockStyle.Fill, .ForeColor = Color.White, .Text = "0",
            .Font = New Font("Segoe UI", 19.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(12, 0, 0, 6)
        }
        Dim caption As New Label With {
            .Dock = DockStyle.Top, .Height = 36, .ForeColor = Color.White, .Text = title,
            .Font = New Font("Segoe UI", 10.5F), .Padding = New Padding(12, 12, 0, 0)
        }
        tile.Controls.Add(value)
        tile.Controls.Add(caption)
        parent.Controls.Add(tile)
        Return value
    End Function

    Private Sub AddMenuButton(menu As Control, text As String, action As Action, Optional back As Color = Nothing)
        Dim b = Ui.MakeButton(text, If(back = Nothing, Color.FromArgb(52, 73, 94), back), 208, 46)
        b.TextAlign = ContentAlignment.MiddleLeft
        b.Padding = New Padding(10, 0, 0, 0)
        b.Margin = New Padding(0, 0, 0, 8)
        AddHandler b.Click, Sub() action()
        menu.Controls.Add(b)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        UpdateClock()
        clockTimer.Start()
        RefreshDashboard()
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        Select Case e.KeyCode
            Case Keys.F1 : OpenPos()
            Case Keys.F2 : OpenProducts()
            Case Keys.F3 : OpenStockIn()
            Case Keys.F4 : OpenReports()
            Case Keys.F5 : RefreshDashboard()
            Case Else : Return
        End Select
        e.Handled = True
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If Not LogoutRequested AndAlso e.CloseReason = CloseReason.UserClosing AndAlso Not Ui.Confirm("Exit the application?") Then
            e.Cancel = True
            Return
        End If
        clockTimer.Stop()
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub UpdateClock()
        lblClock.Text = $"{Session.FullName} ({Session.Role})    |    {Date.Now:dd-MMM-yyyy  hh:mm:ss tt}"
    End Sub

    Public Sub RefreshDashboard()
        Try
            Dim s = ReportsRepo.GetSummary(Date.Today, Date.Today.AddDays(1))
            lblSalesToday.Text = AppSettings.Money(s.Net)
            lblInvoicesToday.Text = s.Invoices.ToString("N0")
            lblProfitToday.Text = AppSettings.Money(s.Profit)
            lblProducts.Text = Ui.ToLng(Db.Scalar("SELECT COUNT(*) FROM products WHERE is_active = 1")).ToString("N0")

            Dim low = Db.Query("SELECT barcode, name, stock, reorder_level FROM products
                                WHERE is_active = 1 AND stock <= reorder_level ORDER BY stock, name LIMIT 200")
            lblLowStock.Text = low.Rows.Count.ToString("N0")
            gridLow.DataSource = low

            gridRecent.DataSource = Db.Query("SELECT s.invoice_no, s.sale_date, COALESCE(u.full_name, u.username, '') AS cashier, s.total
                                              FROM sales s LEFT JOIN users u ON u.id = s.user_id
                                              ORDER BY s.id DESC LIMIT 25")
        Catch ex As Exception
            Ui.ShowError("Dashboard could not be loaded: " & ex.Message)
        End Try
    End Sub

    Private Sub ShowChild(f As Form)
        Using f
            f.ShowDialog(Me)
        End Using
        RefreshDashboard()
    End Sub

    Private Sub OpenPos()
        ShowChild(New PosForm())
    End Sub

    Private Sub OpenProducts()
        ShowChild(New ProductsForm())
    End Sub

    Private Sub OpenStockIn()
        ShowChild(New StockInForm())
    End Sub

    Private Sub OpenReports()
        ShowChild(New SalesReportForm())
    End Sub

    Private Sub OpenCategories()
        ShowChild(New CategoriesForm())
    End Sub

    Private Sub OpenUsers()
        ShowChild(New UsersForm())
    End Sub

    Private Sub OpenSettings()
        ShowChild(New SettingsForm())
    End Sub

    Private Sub OpenChangePassword()
        ShowChild(New ChangePasswordForm())
    End Sub

    Private Sub DoLogout()
        If Not Ui.Confirm("Logout " & Session.FullName & "?") Then Return
        LogoutRequested = True
        Close()
    End Sub

End Class
