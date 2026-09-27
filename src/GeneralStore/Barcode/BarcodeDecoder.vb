Imports System.Drawing
Imports System.Drawing.Imaging
Imports System.Runtime.InteropServices
Imports ZXing
Imports ZXing.Common

''' <summary>
''' Reads barcodes from camera images (ZXing.Net). Supports the retail formats found
''' on products (EAN-13, EAN-8, UPC) plus Code 128 / 39 / 93, ITF and Codabar,
''' so labels printed by this app can be read too.
''' </summary>
Public Module BarcodeDecoder

    Private ReadOnly Reader As BarcodeReaderGeneric = CreateReader()

    Private Function CreateReader() As BarcodeReaderGeneric
        Dim r As New BarcodeReaderGeneric()
        r.AutoRotate = True
        r.Options = New DecodingOptions With {
            .TryHarder = True,
            .PossibleFormats = New List(Of BarcodeFormat) From {
                BarcodeFormat.EAN_13, BarcodeFormat.EAN_8, BarcodeFormat.UPC_A, BarcodeFormat.UPC_E,
                BarcodeFormat.CODE_128, BarcodeFormat.CODE_39, BarcodeFormat.CODE_93, BarcodeFormat.ITF,
                BarcodeFormat.CODABAR
            }
        }
        Return r
    End Function

    ''' <summary>Decodes an 8-bit grayscale image (one byte per pixel). Returns Nothing if no barcode is found.</summary>
    Public Function DecodeGray(pixels As Byte(), width As Integer, height As Integer) As String
        SyncLock Reader
            Dim result = Reader.Decode(New RGBLuminanceSource(pixels, width, height, RGBLuminanceSource.BitmapFormat.Gray8))
            Return result?.Text
        End SyncLock
    End Function

    ''' <summary>Decodes a camera frame. Returns Nothing if no barcode is found.</summary>
    Public Function Decode(image As Bitmap) As String
        Dim rect As New Rectangle(0, 0, image.Width, image.Height)
        Dim data = image.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppRgb)
        Try
            Dim rowBytes = image.Width * 4
            Dim pixels(rowBytes * image.Height - 1) As Byte
            For y = 0 To image.Height - 1
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), pixels, y * rowBytes, rowBytes)
            Next
            SyncLock Reader
                Dim result = Reader.Decode(New RGBLuminanceSource(pixels, image.Width, image.Height, RGBLuminanceSource.BitmapFormat.BGR32))
                Return result?.Text
            End SyncLock
        Finally
            image.UnlockBits(data)
        End Try
    End Function

End Module
