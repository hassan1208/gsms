Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Globalization

''' <summary>
''' Self-contained Code 128 barcode generator (no external library needed).
''' All-numeric values use Code Set C (compact), everything else Code Set B.
''' Any standard USB / Bluetooth barcode scanner can read the output.
''' </summary>
Public NotInheritable Class Code128

    Public Const QuietZone As Integer = 10

    Private Const StartB As Integer = 104
    Private Const StartC As Integer = 105
    Private Const SwitchToB As Integer = 100
    Private Const StopCode As Integer = 106

    ' Bar/space widths for symbol values 0..106 (bar first).
    Private Shared ReadOnly Patterns As String() = {
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
    }

    Private Sub New()
    End Sub

    Public Shared Function IsEncodable(text As String) As Boolean
        If String.IsNullOrEmpty(text) OrElse text.Length > 80 Then Return False
        For Each ch In text
            If AscW(ch) < 32 OrElse AscW(ch) > 126 Then Return False
        Next
        Return True
    End Function

    ''' <summary>Symbol values including start, checksum and stop.</summary>
    Public Shared Function GetSymbolValues(text As String) As List(Of Integer)
        If Not IsEncodable(text) Then
            Throw New ArgumentException("Barcode may only contain letters, digits and standard symbols.", NameOf(text))
        End If

        Dim values As New List(Of Integer)
        Dim allDigits = text.All(Function(c) c >= "0"c AndAlso c <= "9"c)
        If allDigits AndAlso text.Length >= 4 Then
            values.Add(StartC)
            Dim pairEnd = text.Length - (text.Length Mod 2)
            For i = 0 To pairEnd - 1 Step 2
                values.Add(Integer.Parse(text.Substring(i, 2), CultureInfo.InvariantCulture))
            Next
            If pairEnd < text.Length Then
                values.Add(SwitchToB)
                values.Add(AscW(text(text.Length - 1)) - 32)
            End If
        Else
            values.Add(StartB)
            For Each ch In text
                values.Add(AscW(ch) - 32)
            Next
        End If

        Dim sum = values(0)
        For i = 1 To values.Count - 1
            sum += values(i) * i
        Next
        values.Add(sum Mod 103)
        values.Add(StopCode)
        Return values
    End Function

    ''' <summary>Widths (in modules) of alternating bars and spaces, starting with a bar. Quiet zones excluded.</summary>
    Public Shared Function GetElementWidths(text As String) As List(Of Integer)
        Dim widths As New List(Of Integer)
        For Each v In GetSymbolValues(text)
            For Each ch In Patterns(v)
                widths.Add(AscW(ch) - AscW("0"c))
            Next
        Next
        Return widths
    End Function

    ''' <summary>Draws the barcode scaled to fit <paramref name="bounds"/> (works for screen and printer).</summary>
    Public Shared Sub Draw(g As Graphics, text As String, bounds As RectangleF,
                           Optional showText As Boolean = True, Optional textFont As Font = Nothing)
        Dim widths = GetElementWidths(text)
        Dim totalModules = widths.Sum() + QuietZone * 2
        Dim moduleWidth = bounds.Width / totalModules

        Dim ownFont = textFont Is Nothing
        Dim fnt = If(textFont, New Font("Arial", 8.0F))
        Try
            Dim textHeight = If(showText, fnt.GetHeight(g), 0.0F)
            Dim barHeight = Math.Max(1.0F, bounds.Height - textHeight)

            Dim oldSmoothing = g.SmoothingMode
            g.SmoothingMode = SmoothingMode.None
            Dim x = bounds.X + QuietZone * moduleWidth
            Dim isBar = True
            For Each w In widths
                If isBar Then g.FillRectangle(Brushes.Black, x, bounds.Y, w * moduleWidth, barHeight)
                x += w * moduleWidth
                isBar = Not isBar
            Next
            g.SmoothingMode = oldSmoothing

            If showText Then
                Using sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Near}
                    g.DrawString(text, fnt, Brushes.Black, New RectangleF(bounds.X, bounds.Y + barHeight, bounds.Width, textHeight + 2), sf)
                End Using
            End If
        Finally
            If ownFont Then fnt.Dispose()
        End Try
    End Sub

    ''' <summary>Creates a crisp bitmap (whole-pixel module width) for on-screen preview.</summary>
    Public Shared Function Render(text As String, Optional moduleWidth As Integer = 2, Optional barHeight As Integer = 70,
                                 Optional showText As Boolean = True) As Bitmap
        Dim totalModules = GetElementWidths(text).Sum() + QuietZone * 2
        Dim width = totalModules * moduleWidth
        Using fnt As New Font("Arial", 11.0F, GraphicsUnit.Pixel)
            Dim textHeight = If(showText, CInt(Math.Ceiling(fnt.GetHeight())) + 4, 0)
            Dim bmp As New Bitmap(width, barHeight + textHeight + 8)
            Using g = Graphics.FromImage(bmp)
                g.Clear(Color.White)
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit
                Draw(g, text, New RectangleF(0, 4, width, barHeight + textHeight), showText, fnt)
            End Using
            Return bmp
        End Using
    End Function

End Class
