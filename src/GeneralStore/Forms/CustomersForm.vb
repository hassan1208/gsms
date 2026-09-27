Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Windows.Forms

''' <summary>Customers and udhaar (credit) accounts: balances, payments received and the khata.</summary>
<DesignerCategory("Code")>
Public Class CustomersForm
    Inherits Form

    Private ReadOnly txtSearch As New TextBox With {.Width = 240, .Margin = New Padding(4, 8, 4, 4)}
    Private ReadOnly chkDueOnly As New CheckBox With {.Text = "Only customers with udhaar", .AutoSize = True, .Margin = New Padding(10, 10, 4, 4)}
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblTotal As New Label With {
        .Dock = DockStyle.Bottom, .Height = 34, .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold), .ForeColor = Ui.DangerColor,
        .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(10, 0, 0, 0)
    }

    Public Sub New()
        Ui.SetupForm(Me, "Customers & Udhaar", 1100, 620)

        Dim filters = Ui.MakeToolbar()
        filters.Controls.AddRange(New Control() {Ui.MakeLabel("Search name / phone:"), txtSearch, chkDueOnly})

        Dim actions = Ui.MakeToolbar(54)
        Dim btnAdd = Ui.MakeButton("Add Customer", Ui.SuccessColor, 140)
        Dim btnEdit = Ui.MakeButton("Edit", Ui.InfoColor, 90)
        Dim btnPay = Ui.MakeButton("Receive Payment", Ui.PrimaryColor, 160)
        Dim btnLedger = Ui.MakeButton("Khata / Ledger", Ui.InfoColor, 150)
        Dim btnDelete = Ui.MakeButton("Delete", Ui.DangerColor, 90)
        Dim btnExport = Ui.MakeButton("Export CSV", Ui.NeutralColor, 120)
        Dim btnClose = Ui.MakeButton("Close", Ui.NeutralColor, 90)
        btnDelete.Enabled = Session.IsAdmin
        actions.Controls.AddRange(New Control() {btnAdd, btnEdit, btnPay, btnLedger, btnDelete, btnExport, btnClose})

        Ui.StyleGrid(grid)
        Ui.AddColumn(grid, "id", "ID", visible:=False)
        Ui.AddColumn(grid, "name", "Customer", fillWeight:=160)
        Ui.AddColumn(grid, "phone", "Phone", fillWeight:=100)
        Ui.AddColumn(grid, "address", "Address", fillWeight:=150)
        Ui.AddColumn(grid, "credit", "Total Udhaar", "N2", 90, True)
        Ui.AddColumn(grid, "received", "Received", "N2", 90, True)
        Ui.AddColumn(grid, "balance", "Balance", "N2", 90, True)
        Ui.AddColumn(grid, "last_activity", "Last Activity", fillWeight:=110)

        Controls.Add(grid)
        Controls.Add(lblTotal)
        Controls.Add(actions)
        Controls.Add(filters)
        Controls.Add(Ui.MakeHeader("Customers & Udhaar (Credit)", "Balance = udhaar minus payments"))

        AddHandler txtSearch.TextChanged, Sub() LoadRows()
        AddHandler chkDueOnly.CheckedChanged, Sub() LoadRows()
        AddHandler btnAdd.Click, Sub() EditCustomer(0)
        AddHandler btnEdit.Click, Sub() EditCustomer(SelectedId())
        AddHandler btnPay.Click, Sub() ReceivePayment()
        AddHandler btnLedger.Click, Sub() ShowLedger()
        AddHandler btnDelete.Click, Sub() DeleteCustomer()
        AddHandler btnExport.Click, Sub() Ui.ExportGridToCsv(grid, $"customers_udhaar_{Date.Today:yyyyMMdd}.csv", Me)
        AddHandler btnClose.Click, Sub() Close()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then ShowLedger()
        AddHandler grid.CellFormatting, AddressOf Grid_CellFormatting
        KeyPreview = True
        AddHandler KeyDown, Sub(s, e)
                                If e.KeyCode = Keys.Escape Then Close()
                            End Sub
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        LoadRows()
        txtSearch.Focus()
    End Sub

    Private Sub LoadRows(Optional selectId As Long = 0)
        If selectId = 0 Then selectId = SelectedId()
        grid.DataSource = CustomerRepo.List(txtSearch.Text, chkDueOnly.Checked)
        For Each r As DataGridViewRow In grid.Rows
            Dim drv = TryCast(r.DataBoundItem, DataRowView)
            If drv IsNot Nothing AndAlso Ui.ToLng(drv("id")) = selectId Then
                grid.CurrentCell = r.Cells("name")
                Exit For
            End If
        Next
        lblTotal.Text = "Total udhaar outstanding (all customers): " & AppSettings.Money(CustomerRepo.TotalOutstanding())
    End Sub

    Private Sub Grid_CellFormatting(sender As Object, e As DataGridViewCellFormattingEventArgs)
        If e.RowIndex < 0 OrElse grid.Columns(e.ColumnIndex).Name <> "balance" Then Return
        Dim row = TryCast(grid.Rows(e.RowIndex).DataBoundItem, DataRowView)
        If row IsNot Nothing AndAlso Ui.ToDec(row("balance")) > 0 Then
            e.CellStyle.ForeColor = Ui.DangerColor
            e.CellStyle.Font = New Font(grid.Font, FontStyle.Bold)
        End If
    End Sub

    Private Function SelectedId() As Long
        Dim row = Ui.SelectedRow(grid)
        Return If(row Is Nothing, 0L, Ui.ToLng(row("id")))
    End Function

    Private Function SelectedName() As String
        Dim row = Ui.SelectedRow(grid)
        Return If(row Is Nothing, "", Ui.ToStr(row("name")))
    End Function

    Private Sub EditCustomer(id As Long)
        Using f As New CustomerEditForm(id)
            If f.ShowDialog(Me) = DialogResult.OK Then LoadRows(f.SavedId)
        End Using
    End Sub

    Private Sub ReceivePayment()
        Dim id = SelectedId()
        If id = 0 Then
            Ui.ShowInfo("Select a customer first.")
            Return
        End If
        Dim balance = CustomerRepo.Balance(id)
        Dim amount = Ui.PromptNumber(Me, "Receive Payment", $"{SelectedName()} - balance {AppSettings.Money(balance)}",
                                     Math.Max(0D, balance), decimals:=2)
        If Not amount.HasValue OrElse amount.Value <= 0 Then Return
        If amount.Value > balance AndAlso
           Not Ui.Confirm($"Amount is more than the balance ({AppSettings.Money(balance)}). The extra will be kept as advance. Continue?") Then Return

        Dim name = SelectedName()
        CustomerRepo.ReceivePayment(id, amount.Value, "")
        Dim newBalance = CustomerRepo.Balance(id)
        LoadRows(id)
        Ui.ShowInfo($"Received {AppSettings.Money(amount.Value)} from {name}.{vbCrLf}Remaining balance: {AppSettings.Money(newBalance)}")
    End Sub

    Private Sub ShowLedger()
        Dim id = SelectedId()
        If id = 0 Then Return
        Using f As New CustomerLedgerForm(id, SelectedName())
            f.ShowDialog(Me)
        End Using
        LoadRows(id)
    End Sub

    Private Sub DeleteCustomer()
        Dim id = SelectedId()
        If id = 0 OrElse Not Ui.Confirm($"Delete customer '{SelectedName()}'?") Then Return
        If Not CustomerRepo.Delete(id) Then
            Ui.ShowError("This customer has sales or payments and cannot be deleted.")
            Return
        End If
        LoadRows()
    End Sub

End Class

<DesignerCategory("Code")>
Public Class CustomerEditForm
    Inherits Form

    Private _id As Long
    Private ReadOnly txtName As New TextBox()
    Private ReadOnly txtPhone As New TextBox()
    Private ReadOnly txtAddress As New TextBox()

    Public ReadOnly Property SavedId As Long
        Get
            Return _id
        End Get
    End Property

    Public Sub New(id As Long)
        _id = id
        Ui.SetupForm(Me, If(id = 0, "Add Customer", "Edit Customer"), 440, 210, dialog:=True)

        Dim fields As New FieldTable(100)
        fields.AddRow("Name *", txtName)
        fields.AddRow("Phone", txtPhone)
        fields.AddRow("Address", txtAddress)

        Dim btnSave = Ui.MakeButton("Save", Ui.SuccessColor, 110)
        Dim btnCancel = Ui.MakeButton("Cancel", Ui.NeutralColor, 110)
        btnCancel.DialogResult = DialogResult.Cancel
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 56, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10, 6, 10, 6)}
        buttons.Controls.AddRange(New Control() {btnCancel, btnSave})

        Controls.Add(fields)
        Controls.Add(buttons)
        AcceptButton = btnSave
        CancelButton = btnCancel
        AddHandler btnSave.Click, Sub() SaveCustomer()

        If id <> 0 Then
            Dim dt = Db.Query("SELECT name, phone, address FROM customers WHERE id = @id", Db.P("@id", id))
            If dt.Rows.Count > 0 Then
                txtName.Text = Ui.ToStr(dt.Rows(0)("name"))
                txtPhone.Text = Ui.ToStr(dt.Rows(0)("phone"))
                txtAddress.Text = Ui.ToStr(dt.Rows(0)("address"))
            End If
        End If
        Ui.FinishLayout(Me)
    End Sub

    Private Sub SaveCustomer()
        Try
            _id = CustomerRepo.Save(_id, txtName.Text, txtPhone.Text, txtAddress.Text)
        Catch ex As Exception When TypeOf ex Is ArgumentException OrElse TypeOf ex Is InvalidOperationException
            Ui.ShowError(ex.Message)
            txtName.Focus()
            Return
        End Try
        DialogResult = DialogResult.OK
    End Sub

End Class

''' <summary>Customer khata with running balance.</summary>
<DesignerCategory("Code")>
Public Class CustomerLedgerForm
    Inherits Form

    Private ReadOnly _customerId As Long
    Private ReadOnly _name As String
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly lblBalance As New Label With {
        .Dock = DockStyle.Bottom, .Height = 36, .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold),
        .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(10, 0, 0, 0)
    }

    Public Sub New(customerId As Long, name As String)
        _customerId = customerId
        _name = name
        Ui.SetupForm(Me, "Khata - " & name, 860, 520, dialog:=True)
        FormBorderStyle = FormBorderStyle.Sizable

        Ui.StyleGrid(grid)
        Ui.AddColumn(grid, "entry_date", "Date / Time", fillWeight:=110)
        Ui.AddColumn(grid, "details", "Details", fillWeight:=260)
        Ui.AddColumn(grid, "debit", "Udhaar (+)", "N2", 80, True)
        Ui.AddColumn(grid, "credit", "Paid (-)", "N2", 80, True)
        Ui.AddColumn(grid, "balance", "Balance", "N2", 90, True)
        For Each c As DataGridViewColumn In grid.Columns
            c.SortMode = DataGridViewColumnSortMode.NotSortable
        Next

        Dim bar = Ui.MakeToolbar(54)
        Dim btnPay = Ui.MakeButton("Receive Payment", Ui.PrimaryColor, 160)
        Dim btnExport = Ui.MakeButton("Export CSV", Ui.NeutralColor, 120)
        Dim btnClose = Ui.MakeButton("Close", Ui.NeutralColor, 90)
        btnClose.DialogResult = DialogResult.Cancel
        bar.Controls.AddRange(New Control() {btnPay, btnExport, btnClose})

        Controls.Add(grid)
        Controls.Add(lblBalance)
        Controls.Add(bar)
        CancelButton = btnClose

        AddHandler btnPay.Click, Sub() ReceivePayment()
        AddHandler btnExport.Click, Sub() Ui.ExportGridToCsv(grid, $"khata_{_name}.csv", Me)
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        LoadRows()
    End Sub

    Private Sub LoadRows()
        grid.DataSource = CustomerRepo.Ledger(_customerId)
        If grid.Rows.Count > 0 Then grid.FirstDisplayedScrollingRowIndex = grid.Rows.Count - 1
        Dim balance = CustomerRepo.Balance(_customerId)
        lblBalance.Text = If(balance >= 0, "Balance due: ", "Advance: ") & AppSettings.Money(Math.Abs(balance))
        lblBalance.ForeColor = If(balance > 0, Ui.DangerColor, Ui.SuccessColor)
    End Sub

    Private Sub ReceivePayment()
        Dim balance = CustomerRepo.Balance(_customerId)
        Dim amount = Ui.PromptNumber(Me, "Receive Payment", $"{_name} - balance {AppSettings.Money(balance)}", Math.Max(0D, balance), decimals:=2)
        If Not amount.HasValue OrElse amount.Value <= 0 Then Return
        CustomerRepo.ReceivePayment(_customerId, amount.Value, "")
        LoadRows()
    End Sub

End Class
