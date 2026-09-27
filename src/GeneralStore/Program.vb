Imports System.Windows.Forms

Public Module Program

    <STAThread>
    Public Sub Main()
        Application.SetHighDpiMode(HighDpiMode.SystemAware)
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException)
        AddHandler Application.ThreadException,
            Sub(sender, e) Ui.ShowError("Unexpected error: " & e.Exception.Message)

        Try
            Db.Initialize()
        Catch ex As Exception
            MessageBox.Show("Database could not be opened:" & Environment.NewLine & ex.Message,
                            "General Store", MessageBoxButtons.OK, MessageBoxIcon.Error)
            Return
        End Try

        ' Login -> main window. "Logout" brings the login screen back.
        Do
            Using login As New LoginForm()
                If login.ShowDialog() <> DialogResult.OK Then Exit Do
            End Using

            Dim logout As Boolean
            Using main As New MainForm()
                Application.Run(main)
                logout = main.LogoutRequested
            End Using
            Session.Logout()
            If Not logout Then Exit Do
        Loop
    End Sub

End Module
