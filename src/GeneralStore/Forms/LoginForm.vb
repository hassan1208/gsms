Imports System.ComponentModel
Imports System.Drawing
Imports System.Windows.Forms

<DesignerCategory("Code")>
Public Class LoginForm
    Inherits Form

    Private ReadOnly txtUser As New TextBox()
    Private ReadOnly txtPass As New TextBox With {.UseSystemPasswordChar = True}
    Private ReadOnly lblStatus As New Label With {
        .Dock = DockStyle.Fill, .ForeColor = Ui.DangerColor, .TextAlign = ContentAlignment.MiddleCenter
    }

    Public Sub New()
        Ui.SetupForm(Me, "Login - " & AppSettings.StoreName, 420, 330, dialog:=True)
        StartPosition = FormStartPosition.CenterScreen
        ShowInTaskbar = True

        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 86, .BackColor = Ui.PrimaryColor}
        header.Controls.Add(New Label With {
            .Text = "General Store Management System", .Dock = DockStyle.Fill, .ForeColor = Color.Gainsboro,
            .TextAlign = ContentAlignment.TopCenter, .Font = New Font("Segoe UI", 9.5F)
        })
        header.Controls.Add(New Label With {
            .Text = AppSettings.StoreName, .Dock = DockStyle.Top, .Height = 50, .ForeColor = Color.White,
            .TextAlign = ContentAlignment.BottomCenter, .Font = New Font("Segoe UI", 16.0F, FontStyle.Bold)
        })

        Dim fields As New FieldTable(100)
        fields.Padding = New Padding(24, 16, 24, 0)
        fields.AddRow("Username", txtUser)
        fields.AddRow("Password", txtPass)

        Dim btnLogin = Ui.MakeButton("Login", Ui.SuccessColor, 120)
        Dim btnExit = Ui.MakeButton("Exit", Ui.NeutralColor, 120)
        btnExit.DialogResult = DialogResult.Cancel
        AddHandler btnLogin.Click, Sub() DoLogin()

        Dim buttons As New FlowLayoutPanel With {
            .Dock = DockStyle.Bottom, .Height = 58, .FlowDirection = FlowDirection.RightToLeft, .Padding = New Padding(24, 6, 20, 6)
        }
        buttons.Controls.Add(btnExit)
        buttons.Controls.Add(btnLogin)

        Controls.Add(lblStatus)
        Controls.Add(buttons)
        Controls.Add(fields)
        Controls.Add(header)

        AcceptButton = btnLogin
        CancelButton = btnExit
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        txtUser.Focus()
    End Sub

    Private Sub DoLogin()
        Dim user = txtUser.Text.Trim()
        If user = "" OrElse txtPass.Text = "" Then
            lblStatus.Text = "Enter username and password."
            Return
        End If

        Cursor = Cursors.WaitCursor
        Dim ok = Session.Login(user, txtPass.Text)
        Cursor = Cursors.Default

        If ok Then
            DialogResult = DialogResult.OK
        Else
            lblStatus.Text = "Invalid username or password."
            txtPass.SelectAll()
            txtPass.Focus()
        End If
    End Sub

End Class
