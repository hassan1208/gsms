Imports System.Security.Cryptography
Imports System.Text

''' <summary>PBKDF2 password hashing.</summary>
Public Module PasswordHasher

    Private Const Iterations As Integer = 100000

    Public Function NewSalt() As String
        Return Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))
    End Function

    Public Function Hash(password As String, salt As String) As String
        Dim bytes = Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), Convert.FromBase64String(salt),
                                              Iterations, HashAlgorithmName.SHA256, 32)
        Return Convert.ToBase64String(bytes)
    End Function

    Public Function Verify(password As String, salt As String, expectedHash As String) As Boolean
        Try
            Return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(Hash(password, salt)),
                                                           Convert.FromBase64String(expectedHash))
        Catch ex As FormatException
            Return False
        End Try
    End Function

End Module

''' <summary>The logged-in user.</summary>
Public Module Session

    Public Property UserId As Long
    Public Property Username As String = ""
    Public Property FullName As String = ""
    Public Property Role As String = ""

    Public ReadOnly Property IsAdmin As Boolean
        Get
            Return String.Equals(Role, "Admin", StringComparison.OrdinalIgnoreCase)
        End Get
    End Property

    Public Function Login(user As String, password As String) As Boolean
        Dim dt = Db.Query("SELECT id, username, full_name, role, password_hash, salt FROM users
                           WHERE username = @u AND is_active = 1", Db.P("@u", user))
        If dt.Rows.Count = 0 Then Return False
        Dim r = dt.Rows(0)
        If Not PasswordHasher.Verify(password, Ui.ToStr(r("salt")), Ui.ToStr(r("password_hash"))) Then Return False

        UserId = Ui.ToLng(r("id"))
        Username = Ui.ToStr(r("username"))
        FullName = Ui.ToStr(r("full_name"))
        If FullName = "" Then FullName = Username
        Role = Ui.ToStr(r("role"))
        Return True
    End Function

    Public Sub Logout()
        UserId = 0
        Username = ""
        FullName = ""
        Role = ""
    End Sub

    Public Sub SetPassword(id As Long, newPassword As String)
        Dim salt = PasswordHasher.NewSalt()
        Db.Execute("UPDATE users SET password_hash = @h, salt = @s WHERE id = @id",
                   Db.P("@h", PasswordHasher.Hash(newPassword, salt)), Db.P("@s", salt), Db.P("@id", id))
    End Sub

End Module

''' <summary>Store settings kept in the "settings" table.</summary>
Public Module AppSettings

    Private ReadOnly Cache As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

    Public Function GetValue(key As String, Optional defaultValue As String = "") As String
        Dim v As String = Nothing
        If Cache.TryGetValue(key, v) Then Return v
        Dim o = Db.Scalar("SELECT value FROM settings WHERE key = @k", Db.P("@k", key))
        v = If(o Is Nothing OrElse o Is DBNull.Value, defaultValue, Convert.ToString(o))
        Cache(key) = v
        Return v
    End Function

    Public Sub SetValue(key As String, value As String)
        Db.Execute("INSERT INTO settings(key, value) VALUES(@k, @v)
                    ON CONFLICT(key) DO UPDATE SET value = excluded.value", Db.P("@k", key), Db.P("@v", value))
        Cache(key) = value
    End Sub

    Public Sub ClearCache()
        Cache.Clear()
    End Sub

    Public ReadOnly Property StoreName As String
        Get
            Dim v = GetValue("store_name")
            Return If(v = "", "My General Store", v)
        End Get
    End Property

    Public ReadOnly Property CurrencySymbol As String
        Get
            Return GetValue("currency", "Rs")
        End Get
    End Property

    Public ReadOnly Property AllowNegativeStock As Boolean
        Get
            Return GetValue("allow_negative_stock", "0") = "1"
        End Get
    End Property

    Public ReadOnly Property AutoPrintReceipt As Boolean
        Get
            Return GetValue("auto_print_receipt", "1") = "1"
        End Get
    End Property

    Public Function Money(amount As Decimal) As String
        Return (CurrencySymbol & " " & amount.ToString("N2")).Trim()
    End Function

    Public Function Qty(value As Decimal) As String
        Return value.ToString("#,##0.###")
    End Function

End Module
