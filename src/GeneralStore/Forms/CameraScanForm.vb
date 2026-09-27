Imports System.ComponentModel
Imports System.Drawing
Imports System.IO
Imports System.Media
Imports System.Threading
Imports System.Windows.Forms
Imports FlashCap

''' <summary>
''' Scan barcodes with a webcam / laptop camera (for shops without a barcode scanner).
''' Single mode closes after the first code (use <see cref="ScannedCode"/>);
''' continuous mode stays open and raises <see cref="BarcodeScanned"/> for every item.
''' </summary>
<DesignerCategory("Code")>
Public Class CameraScanForm
    Inherits Form

    ''' <summary>Raised on the UI thread for each barcode read (continuous mode).</summary>
    Public Event BarcodeScanned As Action(Of String)

    Public Property ScannedCode As String

    Private ReadOnly _continuous As Boolean
    Private ReadOnly cboCamera As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 320, .Margin = New Padding(4, 8, 4, 4)}
    Private ReadOnly picPreview As New PictureBox With {.Dock = DockStyle.Fill, .SizeMode = PictureBoxSizeMode.Zoom, .BackColor = Color.Black}
    Private ReadOnly lblStatus As New Label With {
        .Dock = DockStyle.Bottom, .Height = 40, .Font = New Font("Segoe UI", 11.0F, FontStyle.Bold),
        .TextAlign = ContentAlignment.MiddleCenter, .BackColor = Color.White
    }
    Private ReadOnly descriptors As New List(Of CaptureDeviceDescriptor)
    Private device As CaptureDevice
    Private lastDecodeTick As Long
    Private lastCode As String
    Private lastCodeTick As Long
    Private finishing As Boolean

    Public Sub New(Optional continuous As Boolean = False)
        _continuous = continuous
        Ui.SetupForm(Me, If(continuous, "Camera Scanner (continuous)", "Scan with Camera"), 720, 560, dialog:=True)
        FormBorderStyle = FormBorderStyle.Sizable
        MinimizeBox = continuous
        ShowInTaskbar = continuous

        Dim bar = Ui.MakeToolbar()
        Dim btnClose = Ui.MakeButton("Close (Esc)", Ui.NeutralColor, 120)
        AddHandler btnClose.Click, Sub() Close()
        bar.Controls.AddRange(New Control() {Ui.MakeLabel("Camera:"), cboCamera, btnClose})

        Dim hint As New Label With {
            .Dock = DockStyle.Bottom, .Height = 28, .ForeColor = Ui.NeutralColor, .TextAlign = ContentAlignment.MiddleCenter,
            .Text = "Hold the barcode 10-20 cm from the camera, straight and in good light."
        }

        Controls.Add(picPreview)
        Controls.Add(hint)
        Controls.Add(lblStatus)
        Controls.Add(bar)
        CancelButton = btnClose

        AddHandler cboCamera.SelectedIndexChanged, Sub() StartSelectedCamera()
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        SetStatus("Looking for cameras...", Ui.NeutralColor)
        Try
            descriptors.AddRange(New CaptureDevices().EnumerateDescriptors().
                                 Where(Function(d) d.Characteristics IsNot Nothing AndAlso d.Characteristics.Length > 0))
        Catch ex As Exception
            SetStatus("Camera error: " & ex.Message, Ui.DangerColor)
            Return
        End Try
        If descriptors.Count = 0 Then
            SetStatus("No camera found. Connect a webcam and open this window again.", Ui.DangerColor)
            Return
        End If

        Dim remembered = AppSettings.GetValue("camera_name")
        Dim selectIndex = 0
        For i = 0 To descriptors.Count - 1
            cboCamera.Items.Add(descriptors(i).Name)
            If descriptors(i).Name = remembered Then selectIndex = i
        Next
        cboCamera.SelectedIndex = selectIndex ' starts the camera
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If e.CloseReason = CloseReason.FormOwnerClosing Then
            ' The POS window is closing: never block it, just release the camera.
            Dim unused = StopCamera()
        ElseIf device IsNot Nothing Then
            ' Stop the camera first, then close for real.
            e.Cancel = True
            Dim result = If(finishing, DialogResult.OK, DialogResult.Cancel)
            StopThen(Sub()
                         If Modal Then DialogResult = result Else Close()
                     End Sub)
            Return
        End If
        MyBase.OnFormClosing(e)
    End Sub

    Private Sub SetStatus(text As String, color As Color)
        lblStatus.Text = text
        lblStatus.ForeColor = color
    End Sub

    ''' <summary>Picks a video mode close to 1280 px wide (enough detail for barcodes, still fast).</summary>
    Private Shared Function ChooseMode(d As CaptureDeviceDescriptor) As VideoCharacteristics
        Return d.Characteristics.
            Where(Function(c) c.PixelFormat <> PixelFormats.Unknown AndAlso c.Width >= 320).
            OrderBy(Function(c) Math.Abs(c.Width - 1280)).
            ThenBy(Function(c) If(c.PixelFormat = PixelFormats.JPEG, 1, 0)).
            FirstOrDefault()
    End Function

    Private Async Sub StartSelectedCamera()
        Dim index = cboCamera.SelectedIndex
        If index < 0 Then Return
        Await StopCamera()

        Dim d = descriptors(index)
        Dim mode = ChooseMode(d)
        If mode Is Nothing Then
            SetStatus("This camera's video format is not supported.", Ui.DangerColor)
            Return
        End If
        Try
            SetStatus("Starting camera...", Ui.NeutralColor)
            device = Await d.OpenAsync(mode, New PixelBufferArrivedDelegate(AddressOf OnFrame), CancellationToken.None)
            Await device.StartAsync(CancellationToken.None)
            AppSettings.SetValue("camera_name", d.Name)
            SetStatus("Point the camera at a barcode", Ui.PrimaryColor)
        Catch ex As Exception
            device = Nothing
            SetStatus("Camera could not be started: " & ex.Message, Ui.DangerColor)
        End Try
    End Sub

    Private Async Function StopCamera() As Task
        Dim d = device
        device = Nothing
        If d Is Nothing Then Return
        Try
            Await d.StopAsync(CancellationToken.None)
        Catch
            ' Device may already be gone (unplugged).
        End Try
        d.Dispose()
    End Function

    Private Async Sub StopThen(action As Action)
        Await StopCamera()
        If Not IsDisposed Then action()
    End Sub

    ''' <summary>Runs on the camera thread for every frame.</summary>
    Private Sub OnFrame(scope As PixelBufferScope)
        If device Is Nothing OrElse IsDisposed Then Return
        Dim frame As Bitmap
        Try
            Dim bytes = scope.Buffer.ExtractImage()
            Using ms As New MemoryStream(bytes), img = Image.FromStream(ms)
                frame = New Bitmap(img)
            End Using
        Catch
            Return
        End Try

        ' Decoding every frame is wasteful; ~6 times per second is plenty.
        Dim code As String = Nothing
        Dim now = Environment.TickCount64
        If now - lastDecodeTick >= 150 Then
            lastDecodeTick = now
            Try
                code = BarcodeDecoder.Decode(frame)
            Catch
                code = Nothing
            End Try
        End If

        Try
            BeginInvoke(Sub() ShowFrame(frame, code))
        Catch ex As Exception When TypeOf ex Is InvalidOperationException OrElse TypeOf ex Is ObjectDisposedException
            frame.Dispose()
        End Try
    End Sub

    Private Sub ShowFrame(frame As Bitmap, code As String)
        If IsDisposed Then
            frame.Dispose()
            Return
        End If
        Dim old = picPreview.Image
        picPreview.Image = frame
        old?.Dispose()

        If String.IsNullOrWhiteSpace(code) OrElse finishing Then Return
        code = code.Trim()
        Dim now = Environment.TickCount64
        ' The same code stays in view for a while: ignore repeats for 2 seconds.
        If code = lastCode AndAlso now - lastCodeTick < 2000 Then Return
        lastCode = code
        lastCodeTick = now
        SystemSounds.Beep.Play()

        If _continuous Then
            SetStatus("Scanned: " & code, Ui.SuccessColor)
            RaiseEvent BarcodeScanned(code)
        Else
            ScannedCode = code
            finishing = True
            SetStatus("Scanned: " & code, Ui.SuccessColor)
            StopThen(Sub() DialogResult = DialogResult.OK)
        End If
    End Sub

    ''' <summary>Opens the camera once and returns the first barcode read, or Nothing.</summary>
    Public Shared Function ScanOnce(owner As IWin32Window) As String
        Using f As New CameraScanForm(False)
            Return If(f.ShowDialog(owner) = DialogResult.OK, f.ScannedCode, Nothing)
        End Using
    End Function

End Class
