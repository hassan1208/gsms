Imports System.Data
Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Drawing.Printing
Imports System.Windows.Forms

''' <summary>Prints 80mm thermal-style receipts (works on any Windows printer).</summary>
Public Module ReceiptPrinter

    Public Sub PrintSale(saleId As Long, Optional preview As Boolean = False, Optional owner As IWin32Window = Nothing)
        Dim sale = Db.Query("SELECT s.*, COALESCE(u.full_name, u.username, '') AS cashier
                             FROM sales s LEFT JOIN users u ON u.id = s.user_id WHERE s.id = @id", Db.P("@id", saleId))
        If sale.Rows.Count = 0 Then Return
        Dim items = Db.Query("SELECT product_name, qty, unit_price, line_total FROM sale_items WHERE sale_id = @id ORDER BY id",
                             Db.P("@id", saleId))

        Using doc = CreateDocument(sale.Rows(0), items)
            Try
                If preview Then
                    Using dlg As New PrintPreviewDialog With {.Document = doc, .Width = 480, .Height = 760, .UseAntiAlias = True}
                        dlg.ShowDialog(owner)
                    End Using
                Else
                    doc.Print()
                End If
            Catch ex As Exception
                Ui.ShowError("Receipt could not be printed: " & ex.Message & vbCrLf &
                             "Check the printer in Settings.")
            End Try
        End Using
    End Sub

    Private Function CreateDocument(sale As DataRow, items As DataTable) As PrintDocument
        Dim doc As New PrintDocument With {.DocumentName = "Receipt " & Ui.ToStr(sale("invoice_no"))}
        Dim printer = AppSettings.GetValue("receipt_printer")
        If printer <> "" Then doc.PrinterSettings.PrinterName = printer

        ' 80mm roll: ~3 inch printable width; height grows with the number of items.
        Dim footerLines = AppSettings.GetValue("receipt_footer").Split(ControlChars.Lf).Length
        Dim height = 500 + items.Rows.Count * 34 + footerLines * 14
        doc.DefaultPageSettings.PaperSize = New PaperSize("Receipt 80mm", 300, height)
        doc.DefaultPageSettings.Margins = New Margins(8, 8, 8, 8)
        AddHandler doc.PrintPage, Sub(s, e) DrawReceipt(e.Graphics, e.MarginBounds, sale, items)
        Return doc
    End Function

    Private Sub DrawReceipt(g As Graphics, bounds As Rectangle, sale As DataRow, items As DataTable)
        Dim left = CSng(bounds.Left), width = CSng(bounds.Width), y = CSng(bounds.Top)

        Using fTitle As New Font("Arial", 12.0F, FontStyle.Bold),
              fNormal As New Font("Arial", 8.0F),
              fBold As New Font("Arial", 8.0F, FontStyle.Bold),
              fTotal As New Font("Arial", 11.0F, FontStyle.Bold),
              center As New StringFormat With {.Alignment = StringAlignment.Center},
              right As New StringFormat With {.Alignment = StringAlignment.Far},
              trim As New StringFormat With {.Trimming = StringTrimming.EllipsisCharacter, .FormatFlags = StringFormatFlags.NoWrap}

            Dim centered = Sub(text As String, f As Font)
                               Dim h = g.MeasureString(text, f, CInt(width), center).Height
                               g.DrawString(text, f, Brushes.Black, New RectangleF(left, y, width, h), center)
                               y += h
                           End Sub
            Dim leftRight = Sub(l As String, r As String, f As Font)
                                Dim h = f.GetHeight(g)
                                g.DrawString(l, f, Brushes.Black, New RectangleF(left, y, width * 0.6F, h), trim)
                                g.DrawString(r, f, Brushes.Black, New RectangleF(left, y, width, h), right)
                                y += h
                            End Sub
            Dim separator = Sub()
                                y += 3
                                Using pen As New Pen(Color.Black, 1) With {.DashStyle = DashStyle.Dash}
                                    g.DrawLine(pen, left, y, left + width, y)
                                End Using
                                y += 4
                            End Sub

            centered(AppSettings.StoreName, fTitle)
            Dim address = AppSettings.GetValue("store_address")
            If address <> "" Then centered(address, fNormal)
            Dim phone = AppSettings.GetValue("store_phone")
            If phone <> "" Then centered("Ph: " & phone, fNormal)
            separator()

            leftRight("Invoice: " & Ui.ToStr(sale("invoice_no")), "", fBold)
            leftRight("Date: " & Ui.ToStr(sale("sale_date")), "", fNormal)
            leftRight("Cashier: " & Ui.ToStr(sale("cashier")), "", fNormal)
            Dim customer = Ui.ToStr(sale("customer_name"))
            If customer <> "" Then leftRight("Customer: " & customer, "", fNormal)
            separator()

            leftRight("Item", "Amount", fBold)
            separator()
            For Each r As DataRow In items.Rows
                g.DrawString(Ui.ToStr(r("product_name")), fNormal, Brushes.Black, New RectangleF(left, y, width, fNormal.GetHeight(g)), trim)
                y += fNormal.GetHeight(g)
                leftRight("   " & AppSettings.Qty(Ui.ToDec(r("qty"))) & " x " & Ui.ToDec(r("unit_price")).ToString("N2"),
                          Ui.ToDec(r("line_total")).ToString("N2"), fNormal)
                y += 2
            Next
            separator()

            leftRight("Items: " & items.Rows.Count.ToString(), "", fNormal)
            leftRight("Sub Total", AppSettings.Money(Ui.ToDec(sale("sub_total"))), fNormal)
            Dim discount = Ui.ToDec(sale("discount"))
            If discount > 0 Then leftRight("Discount", "-" & AppSettings.Money(discount), fNormal)
            y += 2
            leftRight("TOTAL", AppSettings.Money(Ui.ToDec(sale("total"))), fTotal)
            y += 2
            leftRight("Paid (" & Ui.ToStr(sale("payment_method")) & ")", AppSettings.Money(Ui.ToDec(sale("paid"))), fNormal)
            Dim due = Ui.ToDec(sale("due"))
            If due > 0 Then
                leftRight("UDHAAR (this bill)", AppSettings.Money(due), fBold)
                Dim customerId = Ui.ToLng(sale("customer_id"))
                If customerId <> 0 Then
                    leftRight("Total balance now", AppSettings.Money(CustomerRepo.Balance(customerId)), fBold)
                End If
            Else
                leftRight("Change", AppSettings.Money(Ui.ToDec(sale("change_due"))), fBold)
            End If
            separator()

            ' Invoice barcode: scan it in Sales Reports to find this bill again.
            Dim barWidth = width * 0.85F
            Code128.Draw(g, Ui.ToStr(sale("invoice_no")), New RectangleF(left + (width - barWidth) / 2, y, barWidth, 48), True, fNormal)
            y += 52

            Dim footer = AppSettings.GetValue("receipt_footer")
            If footer <> "" Then centered(footer.Replace(vbCr, ""), fNormal)
        End Using
    End Sub

End Module

Public Enum LabelLayout
    A4Sheet3x8
    LabelRoll50x25
End Enum

''' <summary>What to put on the barcode stickers.</summary>
Public Class LabelJob
    Public Property Barcode As String
    Public Property ProductName As String
    Public Property Price As Decimal
    Public Property ShowPrice As Boolean = True
    Public Property ShowStoreName As Boolean = True
    Public Property Copies As Integer = 1
    Public Property Layout As LabelLayout = LabelLayout.A4Sheet3x8
End Class

''' <summary>Prints barcode stickers on A4 sticker sheets or a label printer.</summary>
Public Module LabelPrinter

    Public Function CreateDocument(job As LabelJob) As PrintDocument
        Dim doc As New PrintDocument With {.DocumentName = "Barcode labels - " & job.ProductName}
        Dim printer = AppSettings.GetValue("label_printer")
        If printer <> "" Then doc.PrinterSettings.PrinterName = printer

        If job.Layout = LabelLayout.LabelRoll50x25 Then
            doc.DefaultPageSettings.PaperSize = New PaperSize("Label 50x25mm", 197, 98)
            doc.DefaultPageSettings.Margins = New Margins(0, 0, 0, 0)
        Else
            doc.DefaultPageSettings.PaperSize = New PaperSize("A4", 827, 1169)
            doc.DefaultPageSettings.Margins = New Margins(25, 25, 40, 40)
        End If

        Dim printed = 0
        AddHandler doc.BeginPrint, Sub(s, e) printed = 0
        AddHandler doc.PrintPage,
            Sub(s, e)
                Dim b = e.MarginBounds
                If job.Layout = LabelLayout.LabelRoll50x25 Then
                    DrawLabel(e.Graphics, job, New RectangleF(b.X + 4, b.Y + 3, b.Width - 8, b.Height - 6))
                    printed += 1
                Else
                    Const cols As Integer = 3, rows As Integer = 8
                    Dim cellW = b.Width / CSng(cols), cellH = b.Height / CSng(rows)
                    Using guide As New Pen(Color.LightGray) With {.DashStyle = DashStyle.Dot}
                        For r = 0 To rows - 1
                            For c = 0 To cols - 1
                                If printed >= job.Copies Then Exit For
                                Dim cell As New RectangleF(b.X + c * cellW, b.Y + r * cellH, cellW, cellH)
                                e.Graphics.DrawRectangle(guide, cell.X, cell.Y, cell.Width, cell.Height)
                                cell.Inflate(-8, -6)
                                DrawLabel(e.Graphics, job, cell)
                                printed += 1
                            Next
                        Next
                    End Using
                End If
                e.HasMorePages = printed < job.Copies
            End Sub
        Return doc
    End Function

    ''' <summary>Draws one sticker inside <paramref name="rect"/>.</summary>
    Public Sub DrawLabel(g As Graphics, job As LabelJob, rect As RectangleF)
        Using fStore As New Font("Arial", 6.0F),
              fName As New Font("Arial", 7.5F, FontStyle.Bold),
              fCode As New Font("Arial", 6.5F),
              fPrice As New Font("Arial", 9.0F, FontStyle.Bold),
              sf As New StringFormat With {.Alignment = StringAlignment.Center, .Trimming = StringTrimming.EllipsisCharacter, .FormatFlags = StringFormatFlags.NoWrap}

            Dim y = rect.Y
            If job.ShowStoreName Then
                Dim h = fStore.GetHeight(g)
                g.DrawString(AppSettings.StoreName, fStore, Brushes.Black, New RectangleF(rect.X, y, rect.Width, h), sf)
                y += h
            End If
            Dim nameH = fName.GetHeight(g)
            g.DrawString(job.ProductName, fName, Brushes.Black, New RectangleF(rect.X, y, rect.Width, nameH), sf)
            y += nameH + 1

            Dim priceH = If(job.ShowPrice, fPrice.GetHeight(g), 0.0F)
            Dim barRect As New RectangleF(rect.X, y, rect.Width, Math.Max(10.0F, rect.Bottom - y - priceH))
            Code128.Draw(g, job.Barcode, barRect, True, fCode)

            If job.ShowPrice Then
                g.DrawString(AppSettings.Money(job.Price), fPrice, Brushes.Black,
                             New RectangleF(rect.X, rect.Bottom - priceH, rect.Width, priceH), sf)
            End If
        End Using
    End Sub

    ''' <summary>On-screen preview of one sticker (roughly 2x real size).</summary>
    Public Function RenderPreview(job As LabelJob) As Bitmap
        Dim bmp As New Bitmap(400, 200)
        Using g = Graphics.FromImage(bmp)
            g.Clear(Color.White)
            g.SmoothingMode = SmoothingMode.AntiAlias
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit
            g.ScaleTransform(2.0F, 2.0F)
            g.DrawRectangle(Pens.Silver, 0, 0, 199, 99)
            DrawLabel(g, job, New RectangleF(5, 4, 190, 92))
        End Using
        Return bmp
    End Function

End Module
