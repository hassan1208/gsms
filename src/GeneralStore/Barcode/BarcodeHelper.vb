Imports System.Globalization
Imports System.Security.Cryptography
Imports System.Windows.Forms

Public Module BarcodeHelper

    ''' <summary>EAN-13 check digit for the first 12 digits.</summary>
    Public Function Ean13CheckDigit(first12 As String) As Integer
        If first12 Is Nothing OrElse first12.Length <> 12 OrElse Not first12.All(AddressOf Char.IsDigit) Then
            Throw New ArgumentException("12 digits required.", NameOf(first12))
        End If
        Dim sum = 0
        For i = 0 To 11
            Dim d = AscW(first12(i)) - AscW("0"c)
            sum += If(i Mod 2 = 0, d, d * 3)
        Next
        Return (10 - (sum Mod 10)) Mod 10
    End Function

    ''' <summary>
    ''' New in-store barcode: 13 digits starting with "200" (the GS1 range reserved for
    ''' internal store use) with a valid EAN-13 check digit. Guaranteed unique in the database.
    ''' </summary>
    Public Function GenerateUniqueBarcode() As String
        Do
            Dim body = "200" & RandomNumberGenerator.GetInt32(0, 1000000000).ToString("D9", CultureInfo.InvariantCulture)
            Dim code = body & Ean13CheckDigit(body).ToString(CultureInfo.InvariantCulture)
            If Not BarcodeExists(code) Then Return code
        Loop
    End Function

    Public Function BarcodeExists(code As String) As Boolean
        Return Ui.ToLng(Db.Scalar("SELECT COUNT(*) FROM products WHERE barcode = @b", Db.P("@b", code))) > 0
    End Function

End Module

''' <summary>
''' USB / Bluetooth barcode scanners work as a keyboard ("keyboard wedge"): they type the
''' code very fast and press Enter. This helper makes sure a scan always lands in the scan
''' box even if the cashier clicked somewhere else (grid, button, ...).
''' </summary>
Public NotInheritable Class ScannerInput

    Private Sub New()
    End Sub

    Public Shared Sub RedirectTypingTo(form As Form, scanBox As TextBox)
        form.KeyPreview = True
        AddHandler form.KeyPress,
            Sub(sender, e)
                If Char.IsControl(e.KeyChar) Then Return
                Dim focused = FocusedControl(form)
                If focused Is scanBox Then Return
                If IsTextEntry(focused) Then Return
                scanBox.Focus()
                scanBox.AppendText(e.KeyChar.ToString())
                e.Handled = True
            End Sub
    End Sub

    Public Shared Function IsTextEntry(c As Control) As Boolean
        If c Is Nothing Then Return False
        If TypeOf c Is TextBoxBase OrElse TypeOf c Is ComboBox OrElse TypeOf c Is NumericUpDown Then Return True
        If c.Parent IsNot Nothing AndAlso TypeOf c.Parent Is NumericUpDown Then Return True
        Dim grid = TryCast(c, DataGridView)
        Return grid IsNot Nothing AndAlso grid.IsCurrentCellInEditMode
    End Function

    Public Shared Function FocusedControl(container As ContainerControl) As Control
        Dim c As Control = container.ActiveControl
        While c IsNot Nothing
            Dim inner = TryCast(c, ContainerControl)
            If inner Is Nothing OrElse inner.ActiveControl Is Nothing Then Exit While
            c = inner.ActiveControl
        End While
        Return c
    End Function

End Class
