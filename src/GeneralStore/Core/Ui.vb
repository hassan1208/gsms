Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Windows.Forms

''' <summary>Shared look &amp; feel and small helpers used by all screens.</summary>
Public Module Ui

    Public ReadOnly PrimaryColor As Color = Color.FromArgb(31, 78, 121)
    Public ReadOnly SuccessColor As Color = Color.FromArgb(34, 139, 84)
    Public ReadOnly DangerColor As Color = Color.FromArgb(185, 50, 40)
    Public ReadOnly WarningColor As Color = Color.FromArgb(205, 115, 20)
    Public ReadOnly InfoColor As Color = Color.FromArgb(41, 128, 185)
    Public ReadOnly NeutralColor As Color = Color.FromArgb(95, 105, 120)
    Public ReadOnly BackgroundColor As Color = Color.FromArgb(243, 245, 248)
    Public ReadOnly LowStockColor As Color = Color.FromArgb(255, 226, 222)

    ' ---------- Forms & controls ----------

    ''' <summary>Common form setup. Call <see cref="FinishLayout"/> at the end of the constructor.</summary>
    Public Sub SetupForm(f As Form, title As String, width As Integer, height As Integer, Optional dialog As Boolean = False)
        f.SuspendLayout()
        f.AutoScaleDimensions = New SizeF(96.0F, 96.0F)
        f.AutoScaleMode = AutoScaleMode.Dpi
        f.Text = title
        f.Font = New Font("Segoe UI", 10.0F)
        f.BackColor = BackgroundColor
        f.ClientSize = New Size(width, height)
        If dialog Then
            f.FormBorderStyle = FormBorderStyle.FixedDialog
            f.MaximizeBox = False
            f.MinimizeBox = False
            f.ShowInTaskbar = False
            f.StartPosition = FormStartPosition.CenterParent
        Else
            f.StartPosition = FormStartPosition.CenterScreen
            f.MinimumSize = New Size(Math.Min(width, 820), Math.Min(height, 540))
        End If
    End Sub

    Public Sub FinishLayout(f As Form)
        f.ResumeLayout(False)
        f.PerformLayout()
    End Sub

    Public Function MakeButton(text As String, back As Color, Optional width As Integer = 130, Optional height As Integer = 38) As Button
        Dim b As New Button With {
            .Text = text,
            .BackColor = back,
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Size = New Size(width, height),
            .Cursor = Cursors.Hand,
            .Margin = New Padding(4),
            .Font = New Font("Segoe UI", 9.75F, FontStyle.Bold),
            .UseVisualStyleBackColor = False
        }
        b.FlatAppearance.BorderSize = 0
        Return b
    End Function

    Public Function MakeLabel(text As String, Optional bold As Boolean = False, Optional size As Single = 10.0F) As Label
        Return New Label With {
            .Text = text,
            .AutoSize = True,
            .Font = New Font("Segoe UI", size, If(bold, FontStyle.Bold, FontStyle.Regular)),
            .Margin = New Padding(6, 10, 4, 4)
        }
    End Function

    Public Function MakeNumber(Optional decimals As Integer = 2, Optional minimum As Decimal = 0D, Optional maximum As Decimal = 100000000D) As NumericUpDown
        Return New NumericUpDown With {
            .DecimalPlaces = decimals,
            .Minimum = minimum,
            .Maximum = maximum,
            .ThousandsSeparator = True,
            .TextAlign = HorizontalAlignment.Right
        }
    End Function

    Public Sub SetNumber(n As NumericUpDown, value As Decimal)
        n.Value = Math.Min(n.Maximum, Math.Max(n.Minimum, value))
    End Sub

    ''' <summary>Value as currently typed (NumericUpDown.Value only updates after the user leaves the box).</summary>
    Public Function TypedValue(n As NumericUpDown) As Decimal
        Dim v As Decimal
        If Decimal.TryParse(n.Text, NumberStyles.Number, CultureInfo.CurrentCulture, v) Then
            Return Math.Min(n.Maximum, Math.Max(n.Minimum, v))
        End If
        Return n.Value
    End Function

    Public Function MakeHeader(title As String, Optional subtitle As String = "") As Panel
        Dim header As New Panel With {.Dock = DockStyle.Top, .Height = 54, .BackColor = PrimaryColor}
        Dim lblTitle As New Label With {
            .Text = title, .Dock = DockStyle.Fill, .ForeColor = Color.White,
            .Font = New Font("Segoe UI", 15.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(14, 0, 0, 0)
        }
        header.Controls.Add(lblTitle)
        If subtitle <> "" Then
            header.Controls.Add(New Label With {
                .Text = subtitle, .Dock = DockStyle.Right, .Width = 420, .ForeColor = Color.White,
                .TextAlign = ContentAlignment.MiddleRight, .Padding = New Padding(0, 0, 14, 0)
            })
        End If
        Return header
    End Function

    Public Function MakeToolbar(Optional height As Integer = 52) As FlowLayoutPanel
        Return New FlowLayoutPanel With {
            .Dock = DockStyle.Top, .Height = height, .Padding = New Padding(8, 6, 8, 4),
            .WrapContents = False, .AutoScroll = True
        }
    End Function

    ' ---------- Grids ----------

    Public Sub StyleGrid(g As DataGridView)
        g.AutoGenerateColumns = False
        g.AllowUserToAddRows = False
        g.AllowUserToDeleteRows = False
        g.AllowUserToResizeRows = False
        g.ReadOnly = True
        g.SelectionMode = DataGridViewSelectionMode.FullRowSelect
        g.MultiSelect = False
        g.RowHeadersVisible = False
        g.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        g.BackgroundColor = Color.White
        g.BorderStyle = BorderStyle.None
        g.Dock = DockStyle.Fill
        g.Font = New Font("Segoe UI", 10.0F)
        g.EnableHeadersVisualStyles = False
        g.ColumnHeadersDefaultCellStyle.BackColor = PrimaryColor
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.White
        g.ColumnHeadersDefaultCellStyle.SelectionBackColor = PrimaryColor
        g.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 10.0F, FontStyle.Bold)
        g.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing
        g.ColumnHeadersHeight = 34
        g.RowTemplate.Height = 30
        g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(247, 249, 252)
        g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(52, 152, 219)
        g.DefaultCellStyle.SelectionForeColor = Color.White
    End Sub

    Public Function AddColumn(g As DataGridView, dataProperty As String, header As String,
                              Optional format As String = Nothing, Optional fillWeight As Single = 100,
                              Optional alignRight As Boolean = False, Optional visible As Boolean = True) As DataGridViewTextBoxColumn
        Dim c As New DataGridViewTextBoxColumn With {
            .DataPropertyName = dataProperty,
            .Name = If(dataProperty = "", "col" & g.Columns.Count.ToString(CultureInfo.InvariantCulture), dataProperty),
            .HeaderText = header,
            .FillWeight = fillWeight,
            .Visible = visible
        }
        If format IsNot Nothing Then c.DefaultCellStyle.Format = format
        If alignRight Then c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight
        g.Columns.Add(c)
        Return c
    End Function

    Public Function SelectedRow(g As DataGridView) As DataRowView
        If g.CurrentRow Is Nothing Then Return Nothing
        Return TryCast(g.CurrentRow.DataBoundItem, DataRowView)
    End Function

    ''' <summary>Wraps a grid with a caption bar.</summary>
    Public Function Captioned(caption As String, content As Control) As Panel
        Dim p As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(4)}
        content.Dock = DockStyle.Fill
        p.Controls.Add(content)
        p.Controls.Add(New Label With {
            .Text = caption, .Dock = DockStyle.Top, .Height = 30, .ForeColor = PrimaryColor,
            .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleLeft
        })
        Return p
    End Function

    Public Sub ExportGridToCsv(g As DataGridView, suggestedName As String, owner As IWin32Window)
        If g.Rows.Count = 0 Then
            ShowInfo("Nothing to export.")
            Return
        End If
        Using dlg As New SaveFileDialog With {.Filter = "CSV file (*.csv)|*.csv", .FileName = suggestedName}
            If dlg.ShowDialog(owner) <> DialogResult.OK Then Return
            Dim cols = g.Columns.Cast(Of DataGridViewColumn)().Where(Function(c) c.Visible).OrderBy(Function(c) c.DisplayIndex).ToList()
            Dim sb As New StringBuilder()
            sb.AppendLine(String.Join(",", cols.Select(Function(c) Csv(c.HeaderText))))
            For Each row As DataGridViewRow In g.Rows
                sb.AppendLine(String.Join(",", cols.Select(Function(c) Csv(Convert.ToString(row.Cells(c.Index).FormattedValue, CultureInfo.CurrentCulture)))))
            Next
            File.WriteAllText(dlg.FileName, sb.ToString(), New UTF8Encoding(True))
            ShowInfo("Exported: " & dlg.FileName)
        End Using
    End Sub

    Private Function Csv(value As String) As String
        If value Is Nothing Then Return ""
        If value.IndexOfAny({","c, """"c, ControlChars.Cr, ControlChars.Lf}) >= 0 Then
            Return """" & value.Replace("""", """""") & """"
        End If
        Return value
    End Function

    ' ---------- Messages & prompts ----------

    Public Sub ShowError(message As String)
        MessageBox.Show(message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error)
    End Sub

    Public Sub ShowInfo(message As String)
        MessageBox.Show(message, AppSettings.StoreName, MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Public Function Confirm(message As String) As Boolean
        Return MessageBox.Show(message, "Please confirm", MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes
    End Function

    ''' <summary>Asks for a number; returns Nothing on cancel.</summary>
    Public Function PromptNumber(owner As IWin32Window, title As String, prompt As String, initial As Decimal,
                                 Optional decimals As Integer = 3, Optional minimum As Decimal = 0D) As Decimal?
        Using f As New Form()
            SetupForm(f, title, 320, 150, dialog:=True)
            Dim lbl As New Label With {.Text = prompt, .Location = New Point(14, 12), .AutoSize = True}
            Dim num = MakeNumber(decimals, minimum)
            num.Font = New Font("Segoe UI", 13.0F)
            num.Location = New Point(14, 40)
            num.Width = 292
            SetNumber(num, initial)
            Dim ok = MakeButton("OK", SuccessColor, 100, 36)
            ok.Location = New Point(98, 100)
            ok.DialogResult = DialogResult.OK
            Dim cancel = MakeButton("Cancel", NeutralColor, 100, 36)
            cancel.Location = New Point(206, 100)
            cancel.DialogResult = DialogResult.Cancel
            f.Controls.AddRange(New Control() {lbl, num, ok, cancel})
            f.AcceptButton = ok
            f.CancelButton = cancel
            FinishLayout(f)
            AddHandler f.Shown, Sub()
                                    num.Focus()
                                    num.Select(0, num.Text.Length)
                                End Sub
            If f.ShowDialog(owner) = DialogResult.OK Then Return num.Value
            Return Nothing
        End Using
    End Function

    ' ---------- Conversions ----------

    Public Function ToDec(v As Object) As Decimal
        If v Is Nothing OrElse v Is DBNull.Value Then Return 0D
        Return Convert.ToDecimal(v, CultureInfo.InvariantCulture)
    End Function

    Public Function ToLng(v As Object) As Long
        If v Is Nothing OrElse v Is DBNull.Value Then Return 0L
        Return Convert.ToInt64(v, CultureInfo.InvariantCulture)
    End Function

    Public Function ToStr(v As Object) As String
        If v Is Nothing OrElse v Is DBNull.Value Then Return ""
        Return Convert.ToString(v, CultureInfo.InvariantCulture)
    End Function

End Module

''' <summary>Id/text pair for combo boxes.</summary>
Public Class ComboItem
    Public ReadOnly Property Id As Long
    Public ReadOnly Property Text As String

    Public Sub New(id As Long, text As String)
        Me.Id = id
        Me.Text = text
    End Sub

    Public Overrides Function ToString() As String
        Return Text
    End Function

    Public Shared Sub LoadCategories(cbo As ComboBox, firstText As String, Optional selectId As Long = 0)
        cbo.Items.Clear()
        cbo.Items.Add(New ComboItem(0, firstText))
        For Each r As DataRow In Db.Query("SELECT id, name FROM categories ORDER BY name").Rows
            cbo.Items.Add(New ComboItem(Ui.ToLng(r("id")), Ui.ToStr(r("name"))))
        Next
        cbo.SelectedIndex = 0
        [Select](cbo, selectId)
    End Sub

    Public Shared Sub [Select](cbo As ComboBox, id As Long)
        For i = 0 To cbo.Items.Count - 1
            Dim item = TryCast(cbo.Items(i), ComboItem)
            If item IsNot Nothing AndAlso item.Id = id Then
                cbo.SelectedIndex = i
                Return
            End If
        Next
    End Sub

    Public Shared Function SelectedId(cbo As ComboBox) As Long
        Dim item = TryCast(cbo.SelectedItem, ComboItem)
        Return If(item Is Nothing, 0L, item.Id)
    End Function
End Class

''' <summary>Two-column "Label | Input" layout used on entry forms.</summary>
<DesignerCategory("Code")>
Public Class FieldTable
    Inherits TableLayoutPanel

    Public Sub New(Optional labelWidth As Integer = 140)
        ColumnCount = 2
        RowCount = 0
        ColumnStyles.Add(New ColumnStyle(SizeType.Absolute, labelWidth))
        ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        AutoSize = True
        AutoSizeMode = AutoSizeMode.GrowAndShrink
        Dock = DockStyle.Top
        Padding = New Padding(10, 8, 10, 8)
    End Sub

    Public Function AddRow(caption As String, ctl As Control) As Control
        RowCount += 1
        RowStyles.Add(New RowStyle(SizeType.AutoSize))
        Dim lbl As New Label With {.Text = caption, .AutoSize = False, .Dock = DockStyle.Fill, .TextAlign = ContentAlignment.MiddleLeft}
        ctl.Dock = DockStyle.Fill
        ctl.Margin = New Padding(3, 5, 3, 5)
        Controls.Add(lbl, 0, RowCount - 1)
        Controls.Add(ctl, 1, RowCount - 1)
        Return ctl
    End Function

    Public Function AddFull(ctl As Control) As Control
        RowCount += 1
        RowStyles.Add(New RowStyle(SizeType.AutoSize))
        ctl.Dock = DockStyle.Fill
        Controls.Add(ctl, 0, RowCount - 1)
        SetColumnSpan(ctl, 2)
        Return ctl
    End Function

    ''' <summary>Puts a control and a button side by side in one cell.</summary>
    Public Shared Function WithButton(ctl As Control, btn As Button) As Control
        Dim t As New TableLayoutPanel With {.ColumnCount = 2, .RowCount = 1, .AutoSize = True, .AutoSizeMode = AutoSizeMode.GrowAndShrink, .Margin = New Padding(0)}
        t.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        t.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        ctl.Dock = DockStyle.Fill
        ctl.Margin = New Padding(0, 2, 4, 0)
        btn.Margin = New Padding(0)
        t.Controls.Add(ctl, 0, 0)
        t.Controls.Add(btn, 1, 0)
        Return t
    End Function
End Class
