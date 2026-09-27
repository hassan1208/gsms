Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Drawing.Printing
Imports System.Diagnostics
Imports System.Windows.Forms

''' <summary>User accounts (Admin / Cashier).</summary>
<DesignerCategory("Code")>
Public Class UsersForm
    Inherits Form

    Private ReadOnly grid As New DataGridView()

    Public Sub New()
        Ui.SetupForm(Me, "Users", 780, 480, dialog:=True)
        FormBorderStyle = FormBorderStyle.Sizable

        Ui.StyleGrid(grid)
        Ui.AddColumn(grid, "id", "ID", visible:=False)
        Ui.AddColumn(grid, "username", "Username", fillWeight:=100)
        Ui.AddColumn(grid, "full_name", "Full Name", fillWeight:=150)
        Ui.AddColumn(grid, "role", "Role", fillWeight:=70)
        Ui.AddColumn(grid, "status", "Status", fillWeight:=60)
        Ui.AddColumn(grid, "created_at", "Created", fillWeight:=110)

        Dim bar = Ui.MakeToolbar(54)
        Dim btnAdd = Ui.MakeButton("Add User", Ui.SuccessColor, 110)
        Dim btnEdit = Ui.MakeButton("Edit / Reset Password", Ui.InfoColor, 200)
        Dim btnClose = Ui.MakeButton("Close", Ui.NeutralColor, 90)
        btnClose.DialogResult = DialogResult.Cancel
        bar.Controls.AddRange(New Control() {btnAdd, btnEdit, btnClose})

        Controls.Add(grid)
        Controls.Add(bar)
        CancelButton = btnClose

        AddHandler btnAdd.Click, Sub() EditUser(0)
        AddHandler btnEdit.Click, Sub() EditSelected()
        AddHandler grid.CellDoubleClick, Sub(s, e) If e.RowIndex >= 0 Then EditSelected()
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        LoadRows()
    End Sub

    Private Sub LoadRows()
        grid.DataSource = Db.Query("SELECT id, username, full_name, role, CASE is_active WHEN 1 THEN 'Active' ELSE 'Disabled' END AS status, created_at
                                    FROM users ORDER BY username")
    End Sub

    Private Sub EditSelected()
        Dim row = Ui.SelectedRow(grid)
        If row IsNot Nothing Then EditUser(Ui.ToLng(row("id")))
    End Sub

    Private Sub EditUser(id As Long)
        Using f As New UserEditForm(id)
            If f.ShowDialog(Me) = DialogResult.OK Then LoadRows()
        End Using
    End Sub

End Class

<DesignerCategory("Code")>
Public Class UserEditForm
    Inherits Form

    Private ReadOnly _userId As Long
    Private ReadOnly txtUsername As New TextBox()
    Private ReadOnly txtFullName As New TextBox()
    Private ReadOnly cboRole As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly txtPassword As New TextBox With {.UseSystemPasswordChar = True}
    Private ReadOnly txtConfirm As New TextBox With {.UseSystemPasswordChar = True}
    Private ReadOnly chkActive As New CheckBox With {.Text = "Active (can log in)", .Checked = True, .AutoSize = True}

    Public Sub New(userId As Long)
        _userId = userId
        Ui.SetupForm(Me, If(userId = 0, "Add User", "Edit User"), 440, 360, dialog:=True)
        cboRole.Items.AddRange(New Object() {"Cashier", "Admin"})
        cboRole.SelectedIndex = 0

        Dim fields As New FieldTable(130)
        fields.AddRow("Username *", txtUsername)
        fields.AddRow("Full Name", txtFullName)
        fields.AddRow("Role", cboRole)
        fields.AddRow(If(userId = 0, "Password *", "New Password"), txtPassword)
        fields.AddRow("Confirm", txtConfirm)
        fields.AddRow("", chkActive)
        If userId <> 0 Then
            fields.AddFull(New Label With {.Text = "Leave password empty to keep the current one.", .ForeColor = Ui.NeutralColor, .AutoSize = True})
        End If

        Dim btnSave = Ui.MakeButton("Save", Ui.SuccessColor, 110)
        Dim btnCancel = Ui.MakeButton("Cancel", Ui.NeutralColor, 110)
        btnCancel.DialogResult = DialogResult.Cancel
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 56, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10, 6, 10, 6)}
        buttons.Controls.AddRange(New Control() {btnCancel, btnSave})

        Controls.Add(fields)
        Controls.Add(buttons)
        AcceptButton = btnSave
        CancelButton = btnCancel
        AddHandler btnSave.Click, Sub() SaveUser()

        If userId <> 0 Then
            Dim dt = Db.Query("SELECT username, full_name, role, is_active FROM users WHERE id = @id", Db.P("@id", userId))
            If dt.Rows.Count > 0 Then
                Dim r = dt.Rows(0)
                txtUsername.Text = Ui.ToStr(r("username"))
                txtUsername.ReadOnly = True
                txtFullName.Text = Ui.ToStr(r("full_name"))
                cboRole.SelectedItem = If(Ui.ToStr(r("role")) = "Admin", "Admin", "Cashier")
                chkActive.Checked = Ui.ToLng(r("is_active")) = 1
            End If
        End If
        Ui.FinishLayout(Me)
    End Sub

    Private Sub SaveUser()
        Dim username = txtUsername.Text.Trim()
        Dim role = cboRole.Text
        If username = "" Then
            Ui.ShowError("Username is required.")
            Return
        End If
        If _userId = 0 AndAlso txtPassword.Text = "" Then
            Ui.ShowError("Password is required.")
            Return
        End If
        If txtPassword.Text <> "" AndAlso txtPassword.Text.Length < 4 Then
            Ui.ShowError("Password must be at least 4 characters.")
            Return
        End If
        If txtPassword.Text <> txtConfirm.Text Then
            Ui.ShowError("Passwords do not match.")
            Return
        End If

        If _userId <> 0 Then
            If _userId = Session.UserId AndAlso (role <> "Admin" OrElse Not chkActive.Checked) Then
                Ui.ShowError("You cannot remove your own admin rights or disable your own account.")
                Return
            End If
            Dim otherAdmins = Ui.ToLng(Db.Scalar("SELECT COUNT(*) FROM users WHERE role = 'Admin' AND is_active = 1 AND id <> @id", Db.P("@id", _userId)))
            If otherAdmins = 0 AndAlso (role <> "Admin" OrElse Not chkActive.Checked) Then
                Ui.ShowError("At least one active Admin account is required.")
                Return
            End If
        End If

        Try
            If _userId = 0 Then
                Dim salt = PasswordHasher.NewSalt()
                Db.Execute("INSERT INTO users(username, full_name, password_hash, salt, role, is_active, created_at)
                            VALUES(@u, @f, @h, @s, @r, @a, @now)",
                           Db.P("@u", username), Db.P("@f", txtFullName.Text.Trim()), Db.P("@h", PasswordHasher.Hash(txtPassword.Text, salt)),
                           Db.P("@s", salt), Db.P("@r", role), Db.P("@a", If(chkActive.Checked, 1, 0)), Db.P("@now", Db.NowText()))
            Else
                Db.Execute("UPDATE users SET full_name = @f, role = @r, is_active = @a WHERE id = @id",
                           Db.P("@f", txtFullName.Text.Trim()), Db.P("@r", role), Db.P("@a", If(chkActive.Checked, 1, 0)), Db.P("@id", _userId))
                If txtPassword.Text <> "" Then Session.SetPassword(_userId, txtPassword.Text)
            End If
        Catch ex As Microsoft.Data.Sqlite.SqliteException
            Ui.ShowError("This username already exists.")
            Return
        End Try
        DialogResult = DialogResult.OK
    End Sub

End Class

<DesignerCategory("Code")>
Public Class ChangePasswordForm
    Inherits Form

    Private ReadOnly txtCurrent As New TextBox With {.UseSystemPasswordChar = True}
    Private ReadOnly txtNew As New TextBox With {.UseSystemPasswordChar = True}
    Private ReadOnly txtConfirm As New TextBox With {.UseSystemPasswordChar = True}

    Public Sub New()
        Ui.SetupForm(Me, "Change Password - " & Session.Username, 420, 230, dialog:=True)
        Dim fields As New FieldTable(150)
        fields.AddRow("Current Password", txtCurrent)
        fields.AddRow("New Password", txtNew)
        fields.AddRow("Confirm Password", txtConfirm)

        Dim btnSave = Ui.MakeButton("Change", Ui.SuccessColor, 110)
        Dim btnCancel = Ui.MakeButton("Cancel", Ui.NeutralColor, 110)
        btnCancel.DialogResult = DialogResult.Cancel
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 56, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10, 6, 10, 6)}
        buttons.Controls.AddRange(New Control() {btnCancel, btnSave})

        Controls.Add(fields)
        Controls.Add(buttons)
        AcceptButton = btnSave
        CancelButton = btnCancel
        AddHandler btnSave.Click, Sub() ChangePassword()
        Ui.FinishLayout(Me)
    End Sub

    Private Sub ChangePassword()
        Dim dt = Db.Query("SELECT password_hash, salt FROM users WHERE id = @id", Db.P("@id", Session.UserId))
        If dt.Rows.Count = 0 OrElse Not PasswordHasher.Verify(txtCurrent.Text, Ui.ToStr(dt.Rows(0)("salt")), Ui.ToStr(dt.Rows(0)("password_hash"))) Then
            Ui.ShowError("Current password is wrong.")
            Return
        End If
        If txtNew.Text.Length < 4 Then
            Ui.ShowError("New password must be at least 4 characters.")
            Return
        End If
        If txtNew.Text <> txtConfirm.Text Then
            Ui.ShowError("Passwords do not match.")
            Return
        End If
        Session.SetPassword(Session.UserId, txtNew.Text)
        Ui.ShowInfo("Password changed.")
        DialogResult = DialogResult.OK
    End Sub

End Class

''' <summary>Store details, printers, stock rules and backup / restore.</summary>
<DesignerCategory("Code")>
Public Class SettingsForm
    Inherits Form

    Private Const DefaultPrinterText As String = "(Windows default printer)"

    Private ReadOnly txtStoreName As New TextBox()
    Private ReadOnly txtAddress As New TextBox()
    Private ReadOnly txtPhone As New TextBox()
    Private ReadOnly txtCurrency As New TextBox()
    Private ReadOnly txtFooter As New TextBox With {.Multiline = True, .Height = 60, .ScrollBars = ScrollBars.Vertical}
    Private ReadOnly cboReceiptPrinter As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly cboLabelPrinter As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly chkAutoPrint As New CheckBox With {.Text = "Print receipt automatically after each sale", .AutoSize = True}
    Private ReadOnly chkNegative As New CheckBox With {.Text = "Allow selling when stock is zero (negative stock)", .AutoSize = True}

    Public Sub New()
        Ui.SetupForm(Me, "Settings", 560, 560, dialog:=True)

        Dim fields As New FieldTable(150)
        fields.AddRow("Store Name", txtStoreName)
        fields.AddRow("Address", txtAddress)
        fields.AddRow("Phone", txtPhone)
        fields.AddRow("Currency Symbol", txtCurrency)
        fields.AddRow("Receipt Footer", txtFooter)
        fields.AddRow("Receipt Printer", cboReceiptPrinter)
        fields.AddRow("Label Printer", cboLabelPrinter)
        fields.AddRow("", chkAutoPrint)
        fields.AddRow("", chkNegative)
        fields.AddFull(New Label With {.Text = "Database: " & Db.DatabaseFile, .ForeColor = Ui.NeutralColor, .AutoSize = True, .MaximumSize = New Size(520, 0)})

        Dim backupBar As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 54, .Padding = New Padding(10, 4, 10, 4)}
        Dim btnBackup = Ui.MakeButton("Backup Database", Ui.InfoColor, 160)
        Dim btnRestore = Ui.MakeButton("Restore Backup", Ui.WarningColor, 150)
        Dim btnFolder = Ui.MakeButton("Open Data Folder", Ui.NeutralColor, 160)
        backupBar.Controls.AddRange(New Control() {btnBackup, btnRestore, btnFolder})

        Dim btnSave = Ui.MakeButton("Save", Ui.SuccessColor, 110)
        Dim btnCancel = Ui.MakeButton("Cancel", Ui.NeutralColor, 110)
        btnCancel.DialogResult = DialogResult.Cancel
        Dim buttons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 56, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(10, 6, 10, 6)}
        buttons.Controls.AddRange(New Control() {btnCancel, btnSave})

        Controls.Add(backupBar)
        Controls.Add(fields)
        Controls.Add(buttons)
        CancelButton = btnCancel

        AddHandler btnSave.Click, Sub() SaveSettings()
        AddHandler btnBackup.Click, Sub() Backup()
        AddHandler btnRestore.Click, Sub() Restore()
        AddHandler btnFolder.Click, Sub() Process.Start(New ProcessStartInfo(Db.DataFolder) With {.UseShellExecute = True})

        LoadValues()
        Ui.FinishLayout(Me)
    End Sub

    Private Sub LoadValues()
        txtStoreName.Text = AppSettings.GetValue("store_name")
        txtAddress.Text = AppSettings.GetValue("store_address")
        txtPhone.Text = AppSettings.GetValue("store_phone")
        txtCurrency.Text = AppSettings.GetValue("currency")
        txtFooter.Text = AppSettings.GetValue("receipt_footer")
        chkAutoPrint.Checked = AppSettings.AutoPrintReceipt
        chkNegative.Checked = AppSettings.AllowNegativeStock
        FillPrinters(cboReceiptPrinter, AppSettings.GetValue("receipt_printer"))
        FillPrinters(cboLabelPrinter, AppSettings.GetValue("label_printer"))
    End Sub

    Private Shared Sub FillPrinters(cbo As ComboBox, selected As String)
        cbo.Items.Clear()
        cbo.Items.Add(DefaultPrinterText)
        Try
            For Each name As String In PrinterSettings.InstalledPrinters
                cbo.Items.Add(name)
            Next
        Catch
            ' No print spooler available: only the default entry is shown.
        End Try
        If selected <> "" AndAlso Not cbo.Items.Contains(selected) Then cbo.Items.Add(selected)
        cbo.SelectedItem = If(selected = "", DefaultPrinterText, selected)
    End Sub

    Private Shared Function PrinterValue(cbo As ComboBox) As String
        Dim v = Convert.ToString(cbo.SelectedItem)
        Return If(v = DefaultPrinterText, "", v)
    End Function

    Private Sub SaveSettings()
        If txtStoreName.Text.Trim() = "" Then
            Ui.ShowError("Store name is required.")
            Return
        End If
        AppSettings.SetValue("store_name", txtStoreName.Text.Trim())
        AppSettings.SetValue("store_address", txtAddress.Text.Trim())
        AppSettings.SetValue("store_phone", txtPhone.Text.Trim())
        AppSettings.SetValue("currency", txtCurrency.Text.Trim())
        AppSettings.SetValue("receipt_footer", txtFooter.Text.Trim())
        AppSettings.SetValue("receipt_printer", PrinterValue(cboReceiptPrinter))
        AppSettings.SetValue("label_printer", PrinterValue(cboLabelPrinter))
        AppSettings.SetValue("auto_print_receipt", If(chkAutoPrint.Checked, "1", "0"))
        AppSettings.SetValue("allow_negative_stock", If(chkNegative.Checked, "1", "0"))
        Ui.ShowInfo("Settings saved. Some changes (store name in the title) show after the next login.")
        DialogResult = DialogResult.OK
    End Sub

    Private Sub Backup()
        Using dlg As New SaveFileDialog With {
            .Filter = "Database backup (*.db)|*.db",
            .FileName = $"store_backup_{Date.Now:yyyyMMdd_HHmm}.db"
        }
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Db.BackupTo(dlg.FileName)
                Ui.ShowInfo("Backup saved: " & dlg.FileName)
            Catch ex As Exception
                Ui.ShowError("Backup failed: " & ex.Message)
            End Try
        End Using
    End Sub

    Private Sub Restore()
        Using dlg As New OpenFileDialog With {.Filter = "Database backup (*.db)|*.db|All files (*.*)|*.*"}
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            If Not Ui.Confirm("Restoring will REPLACE all current data (products, sales, users) with the backup." & vbCrLf & "Continue?") Then Return
            Try
                Db.RestoreFrom(dlg.FileName)
                LoadValues()
                Ui.ShowInfo("Backup restored. Please logout and login again.")
            Catch ex As Exception
                Ui.ShowError("Restore failed: " & ex.Message)
            End Try
        End Using
    End Sub

End Class
