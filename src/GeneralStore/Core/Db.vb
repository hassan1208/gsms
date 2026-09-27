Imports System.Data
Imports System.Globalization
Imports System.IO
Imports Microsoft.Data.Sqlite

''' <summary>
''' Small data-access layer over a local SQLite database file
''' (%LOCALAPPDATA%\GeneralStore\store.db).
''' </summary>
Public Module Db

    Private _connectionString As String
    Private _databaseFile As String

    Public ReadOnly Property DatabaseFile As String
        Get
            Return _databaseFile
        End Get
    End Property

    Public ReadOnly Property DataFolder As String
        Get
            Return Path.GetDirectoryName(_databaseFile)
        End Get
    End Property

    Public Sub Initialize()
        Dim folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GeneralStore")
        Directory.CreateDirectory(folder)
        _databaseFile = Path.Combine(folder, "store.db")
        _connectionString = New SqliteConnectionStringBuilder With {
            .DataSource = _databaseFile,
            .Mode = SqliteOpenMode.ReadWriteCreate,
            .ForeignKeys = True
        }.ToString()

        CreateSchema()
        SeedData()
    End Sub

    Public Function Open() As SqliteConnection
        Dim cn As New SqliteConnection(_connectionString)
        cn.Open()
        Return cn
    End Function

    ''' <summary>Creates a named parameter. Decimals are stored as REAL.</summary>
    Public Function P(name As String, value As Object) As SqliteParameter
        If value Is Nothing Then value = DBNull.Value
        If TypeOf value Is Decimal Then value = CDbl(DirectCast(value, Decimal))
        Return New SqliteParameter(name, value)
    End Function

    ''' <summary>Current local time in the format stored in the database.</summary>
    Public Function NowText() As String
        Return Date.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
    End Function

    Public Function DateKey(d As Date) As String
        Return d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    End Function

    Private Function MakeCommand(cn As SqliteConnection, tx As SqliteTransaction, sql As String, params As SqliteParameter()) As SqliteCommand
        Dim cmd = cn.CreateCommand()
        cmd.CommandText = sql
        cmd.Transaction = tx
        If params IsNot Nothing Then
            For Each prm In params
                cmd.Parameters.Add(prm)
            Next
        End If
        Return cmd
    End Function

    Public Function Execute(sql As String, ParamArray params As SqliteParameter()) As Integer
        Using cn = Open(), cmd = MakeCommand(cn, Nothing, sql, params)
            Return cmd.ExecuteNonQuery()
        End Using
    End Function

    Public Function Scalar(sql As String, ParamArray params As SqliteParameter()) As Object
        Using cn = Open(), cmd = MakeCommand(cn, Nothing, sql, params)
            Return cmd.ExecuteScalar()
        End Using
    End Function

    Public Function Query(sql As String, ParamArray params As SqliteParameter()) As DataTable
        Using cn = Open(), cmd = MakeCommand(cn, Nothing, sql, params), rd = cmd.ExecuteReader()
            Return ReadTable(rd)
        End Using
    End Function

    ' ---- Transaction-aware variants ----

    Public Function ExecuteTx(cn As SqliteConnection, tx As SqliteTransaction, sql As String, ParamArray params As SqliteParameter()) As Integer
        Using cmd = MakeCommand(cn, tx, sql, params)
            Return cmd.ExecuteNonQuery()
        End Using
    End Function

    Public Function ScalarTx(cn As SqliteConnection, tx As SqliteTransaction, sql As String, ParamArray params As SqliteParameter()) As Object
        Using cmd = MakeCommand(cn, tx, sql, params)
            Return cmd.ExecuteScalar()
        End Using
    End Function

    Public Function QueryTx(cn As SqliteConnection, tx As SqliteTransaction, sql As String, ParamArray params As SqliteParameter()) As DataTable
        Using cmd = MakeCommand(cn, tx, sql, params), rd = cmd.ExecuteReader()
            Return ReadTable(rd)
        End Using
    End Function

    ''' <summary>
    ''' Loads a reader into a DataTable. Column types are picked from the actual values
    ''' (SQLite is dynamically typed), so grids can sort numbers correctly.
    ''' </summary>
    Private Function ReadTable(rd As SqliteDataReader) As DataTable
        Dim fieldCount = rd.FieldCount
        Dim declared(fieldCount - 1) As Type
        For i = 0 To fieldCount - 1
            Try
                declared(i) = rd.GetFieldType(i)
            Catch
                declared(i) = GetType(String)
            End Try
        Next

        Dim rows As New List(Of Object())
        While rd.Read()
            Dim values(fieldCount - 1) As Object
            rd.GetValues(values)
            rows.Add(values)
        End While

        Dim dt As New DataTable()
        For i = 0 To fieldCount - 1
            Dim hasLong = False, hasDouble = False, hasOther = False
            For Each r In rows
                Dim v = r(i)
                If v Is Nothing OrElse v Is DBNull.Value Then Continue For
                If TypeOf v Is Long Then
                    hasLong = True
                ElseIf TypeOf v Is Double Then
                    hasDouble = True
                Else
                    hasOther = True
                End If
            Next

            Dim colType As Type
            If hasOther Then
                colType = GetType(String)
            ElseIf hasDouble Then
                colType = GetType(Double)
            ElseIf hasLong Then
                colType = GetType(Long)
            ElseIf declared(i) Is GetType(Long) OrElse declared(i) Is GetType(Double) Then
                colType = declared(i)
            Else
                colType = GetType(String)
            End If

            Dim name = rd.GetName(i)
            Dim unique = name, n = 1
            While dt.Columns.Contains(unique)
                n += 1
                unique = name & n.ToString(CultureInfo.InvariantCulture)
            End While
            dt.Columns.Add(unique, colType)
        Next

        For Each r In rows
            Dim converted(fieldCount - 1) As Object
            For i = 0 To fieldCount - 1
                Dim v = r(i)
                If v Is Nothing OrElse v Is DBNull.Value Then
                    converted(i) = DBNull.Value
                Else
                    converted(i) = Convert.ChangeType(v, dt.Columns(i).DataType, CultureInfo.InvariantCulture)
                End If
            Next
            dt.Rows.Add(converted)
        Next
        Return dt
    End Function

    Private Sub CreateSchema()
        Execute("
CREATE TABLE IF NOT EXISTS settings (
    key   TEXT PRIMARY KEY,
    value TEXT
);

CREATE TABLE IF NOT EXISTS users (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    username      TEXT NOT NULL UNIQUE COLLATE NOCASE,
    full_name     TEXT,
    password_hash TEXT NOT NULL,
    salt          TEXT NOT NULL,
    role          TEXT NOT NULL DEFAULT 'Cashier',
    is_active     INTEGER NOT NULL DEFAULT 1,
    created_at    TEXT
);

CREATE TABLE IF NOT EXISTS categories (
    id   INTEGER PRIMARY KEY AUTOINCREMENT,
    name TEXT NOT NULL UNIQUE COLLATE NOCASE
);

CREATE TABLE IF NOT EXISTS products (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    barcode       TEXT NOT NULL UNIQUE,
    name          TEXT NOT NULL,
    category_id   INTEGER REFERENCES categories(id) ON DELETE SET NULL,
    unit          TEXT NOT NULL DEFAULT 'pcs',
    cost_price    REAL NOT NULL DEFAULT 0,
    sale_price    REAL NOT NULL DEFAULT 0,
    stock         REAL NOT NULL DEFAULT 0,
    reorder_level REAL NOT NULL DEFAULT 5,
    is_active     INTEGER NOT NULL DEFAULT 1,
    created_at    TEXT
);
CREATE INDEX IF NOT EXISTS ix_products_name ON products(name);

CREATE TABLE IF NOT EXISTS sales (
    id             INTEGER PRIMARY KEY AUTOINCREMENT,
    invoice_no     TEXT NOT NULL UNIQUE,
    sale_date      TEXT NOT NULL,
    user_id        INTEGER REFERENCES users(id),
    customer_name  TEXT,
    sub_total      REAL NOT NULL,
    discount       REAL NOT NULL DEFAULT 0,
    total          REAL NOT NULL,
    paid           REAL NOT NULL,
    change_due     REAL NOT NULL DEFAULT 0,
    payment_method TEXT NOT NULL DEFAULT 'Cash'
);
CREATE INDEX IF NOT EXISTS ix_sales_date ON sales(sale_date);

CREATE TABLE IF NOT EXISTS sale_items (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    sale_id      INTEGER NOT NULL REFERENCES sales(id) ON DELETE CASCADE,
    product_id   INTEGER NOT NULL REFERENCES products(id),
    barcode      TEXT,
    product_name TEXT NOT NULL,
    qty          REAL NOT NULL,
    unit_price   REAL NOT NULL,
    cost_price   REAL NOT NULL,
    line_total   REAL NOT NULL
);
CREATE INDEX IF NOT EXISTS ix_sale_items_sale ON sale_items(sale_id);

CREATE TABLE IF NOT EXISTS stock_in (
    id         INTEGER PRIMARY KEY AUTOINCREMENT,
    product_id INTEGER NOT NULL REFERENCES products(id),
    qty        REAL NOT NULL,
    cost_price REAL NOT NULL DEFAULT 0,
    supplier   TEXT,
    note       TEXT,
    user_id    INTEGER REFERENCES users(id),
    created_at TEXT NOT NULL
);
")
    End Sub

    Private Sub SeedData()
        Dim defaults As New Dictionary(Of String, String) From {
            {"store_name", "My General Store"},
            {"store_address", "Main Bazar, City"},
            {"store_phone", "0300-0000000"},
            {"currency", "Rs"},
            {"receipt_footer", "Thank you for shopping with us!" & vbCrLf & "No return without receipt."},
            {"allow_negative_stock", "0"},
            {"auto_print_receipt", "1"},
            {"receipt_printer", ""},
            {"label_printer", ""}
        }
        For Each kv In defaults
            Execute("INSERT OR IGNORE INTO settings(key, value) VALUES(@k, @v)", P("@k", kv.Key), P("@v", kv.Value))
        Next

        If Ui.ToLng(Scalar("SELECT COUNT(*) FROM users")) > 0 Then Return

        ' ---- First run: default admin + some sample data ----
        Dim salt = PasswordHasher.NewSalt()
        Execute("INSERT INTO users(username, full_name, password_hash, salt, role, is_active, created_at)
                 VALUES('admin', 'Administrator', @h, @s, 'Admin', 1, @now)",
                P("@h", PasswordHasher.Hash("admin123", salt)), P("@s", salt), P("@now", NowText()))

        For Each c In {"Grocery", "Beverages", "Dairy", "Snacks", "Personal Care", "Household"}
            Execute("INSERT OR IGNORE INTO categories(name) VALUES(@n)", P("@n", c))
        Next

        Dim samples = New Object()() {
            New Object() {"Sugar 1kg", "Grocery", "pcs", 140D, 160D, 50D},
            New Object() {"Cooking Oil 1L", "Grocery", "pcs", 480D, 530D, 30D},
            New Object() {"Tea 190g", "Beverages", "pcs", 330D, 370D, 25D},
            New Object() {"Mineral Water 1.5L", "Beverages", "pcs", 70D, 90D, 60D},
            New Object() {"Fresh Milk 1L", "Dairy", "pcs", 200D, 220D, 3D},
            New Object() {"Biscuits Family Pack", "Snacks", "pcs", 95D, 120D, 40D},
            New Object() {"Bath Soap", "Personal Care", "pcs", 110D, 135D, 35D},
            New Object() {"Dishwash Bar", "Household", "pcs", 55D, 70D, 4D},
            New Object() {"Rice (Loose)", "Grocery", "kg", 280D, 320D, 100D}
        }
        For Each s In samples
            Dim catId = Scalar("SELECT id FROM categories WHERE name = @n", P("@n", s(1)))
            Execute("INSERT INTO products(barcode, name, category_id, unit, cost_price, sale_price, stock, reorder_level, created_at)
                     VALUES(@b, @n, @c, @u, @cp, @sp, @st, 5, @now)",
                    P("@b", BarcodeHelper.GenerateUniqueBarcode()), P("@n", s(0)), P("@c", catId), P("@u", s(2)),
                    P("@cp", s(3)), P("@sp", s(4)), P("@st", s(5)), P("@now", NowText()))
        Next
    End Sub

    ''' <summary>Copies the live database to <paramref name="targetFile"/>.</summary>
    Public Sub BackupTo(targetFile As String)
        If File.Exists(targetFile) Then File.Delete(targetFile)
        Dim cs = New SqliteConnectionStringBuilder With {.DataSource = targetFile, .Pooling = False}.ToString()
        Using src = Open(), dst As New SqliteConnection(cs)
            dst.Open()
            src.BackupDatabase(dst)
        End Using
    End Sub

    ''' <summary>Replaces the live database contents with a backup file.</summary>
    Public Sub RestoreFrom(backupFile As String)
        Dim cs = New SqliteConnectionStringBuilder With {.DataSource = backupFile, .Mode = SqliteOpenMode.ReadOnly, .Pooling = False}.ToString()
        Using src As New SqliteConnection(cs)
            src.Open()
            Using cmd = src.CreateCommand()
                cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('products','sales','users')"
                If Ui.ToLng(cmd.ExecuteScalar()) < 3 Then
                    Throw New InvalidOperationException("Selected file is not a valid General Store backup.")
                End If
            End Using
            Using dst = Open()
                src.BackupDatabase(dst)
            End Using
        End Using
        SqliteConnection.ClearAllPools()
        CreateSchema()
        AppSettings.ClearCache()
    End Sub

End Module
