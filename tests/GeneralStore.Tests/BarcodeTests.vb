Imports Xunit

Public Class BarcodeTests

    <Theory>
    <InlineData("400638133393", 1)>
    <InlineData("590123412345", 7)>
    <InlineData("200000000000", 8)>
    Public Sub Ean13CheckDigit_MatchesKnownValues(first12 As String, expected As Integer)
        Assert.Equal(expected, BarcodeHelper.Ean13CheckDigit(first12))
    End Sub

    <Fact>
    Public Sub Code128_EverySymbolIsElevenModulesWide()
        For Each value In {"2001234567890", "INV260927-0001", "Hello World!", "1234"}
            Dim widths = Code128.GetElementWidths(value)
            ' n symbols of 11 modules + stop symbol of 13 modules
            Assert.Equal(0, (widths.Sum() - 13) Mod 11)
        Next
    End Sub

    <Fact>
    Public Sub Code128_RejectsNonAscii()
        Assert.False(Code128.IsEncodable("چینی"))
        Assert.False(Code128.IsEncodable(""))
        Assert.Throws(Of ArgumentException)(Function() Code128.GetSymbolValues("é"))
    End Sub

    ''' <summary>
    ''' Renders our own Code 128 output to pixels and reads it back with ZXing (the camera decoder):
    ''' proves printed labels and receipts can be scanned.
    ''' </summary>
    <Theory>
    <InlineData("2001234567890")>
    <InlineData("INV260927-0001")>
    <InlineData("Hello-123")>
    <InlineData("12345678")>
    <InlineData("A")>
    Public Sub Code128_IsReadableByDecoder(text As String)
        Const moduleWidth As Integer = 3, height As Integer = 80
        Dim modules As New List(Of Boolean)
        modules.AddRange(Enumerable.Repeat(False, Code128.QuietZone))
        Dim isBar = True
        For Each w In Code128.GetElementWidths(text)
            modules.AddRange(Enumerable.Repeat(isBar, w))
            isBar = Not isBar
        Next
        modules.AddRange(Enumerable.Repeat(False, Code128.QuietZone))

        Dim width = modules.Count * moduleWidth
        Dim pixels(width * height - 1) As Byte
        For y = 0 To height - 1
            For x = 0 To width - 1
                pixels(y * width + x) = If(modules(x \ moduleWidth), CByte(0), CByte(255))
            Next
        Next

        Assert.Equal(text, BarcodeDecoder.DecodeGray(pixels, width, height))
    End Sub

    <Fact>
    Public Sub Decoder_ReturnsNothingForBlankImage()
        Dim pixels(200 * 100 - 1) As Byte
        For i = 0 To pixels.Length - 1
            pixels(i) = 255
        Next
        Assert.Null(BarcodeDecoder.DecodeGray(pixels, 200, 100))
    End Sub

End Class
