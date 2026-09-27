Imports System.ComponentModel
Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.Media
Imports System.Windows.Forms

''' <summary>
''' Point of Sale. Scan a barcode (or type it and press Enter) to add the item to the bill.
''' "3*barcode" adds 3 at once. Everything works from the keyboard.
''' </summary>
<DesignerCategory("Code")>
Public Class PosForm
    Inherits Form

    Private ReadOnly cart As New BindingList(Of CartItem)()
    Private ReadOnly grid As New DataGridView()
    Private ReadOnly txtScan As New TextBox With {.Font = New Font("Segoe UI", 16.0F), .Width = 360, .Margin = New Padding(4, 6, 4, 4)}
    Private ReadOnly numQty As NumericUpDown = Ui.MakeNumber(3, 0.001D, 100000D)
    Private ReadOnly lblLastItem As New Label With {
        .Dock = DockStyle.Top, .Height = 40, .Font = New Font("Segoe UI", 13.0F, FontStyle.Bold), .ForeColor = Ui.PrimaryColor,
        .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(10, 0, 0, 0), .Text = "Scan a product to start the bill"
    }
    Private ReadOnly lblStatus As New Label With {
        .Dock = DockStyle.Bottom, .Height = 32, .Font = New Font("Segoe UI", 10.5F, FontStyle.Bold),
        .TextAlign = ContentAlignment.MiddleLeft, .Padding = New Padding(10, 0, 0, 0)
    }

    Private ReadOnly lblItems As New Label With {.AutoSize = True, .Font = New Font("Segoe UI", 11.0F)}
    Private ReadOnly lblSubtotal As New Label With {.AutoSize = True, .Font = New Font("Segoe UI", 12.0F, FontStyle.Bold)}
    Private ReadOnly numDiscount As NumericUpDown = Ui.MakeNumber(2)
    Private ReadOnly cboDiscountType As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList, .Width = 70}
    Private ReadOnly lblDiscount As New Label With {.AutoSize = True, .ForeColor = Ui.DangerColor}
    Private ReadOnly lblTotal As New Label With {
        .Height = 74, .BackColor = Color.FromArgb(22, 34, 48), .ForeColor = Color.FromArgb(80, 230, 140),
        .Font = New Font("Segoe UI", 26.0F, FontStyle.Bold), .TextAlign = ContentAlignment.MiddleRight,
        .Padding = New Padding(0, 0, 10, 0), .Margin = New Padding(0, 8, 0, 8)
    }
    Private ReadOnly cboPayment As New ComboBox With {.DropDownStyle = ComboBoxStyle.DropDownList}
    Private ReadOnly txtCustomer As New TextBox With {
        .AutoCompleteMode = AutoCompleteMode.SuggestAppend, .AutoCompleteSource = AutoCompleteSource.CustomSource
    }
    Private ReadOnly lblCustomerBalance As New Label With {.AutoSize = True, .ForeColor = Ui.WarningColor}
    Private ReadOnly numPaid As NumericUpDown = Ui.MakeNumber(2)
    Private ReadOnly lblChange As New Label With {.AutoSize = True, .Font = New Font("Segoe UI", 16.0F, FontStyle.Bold)}
    Private ReadOnly chkPrint As New CheckBox With {.Text = "Print receipt", .AutoSize = True}
    Private ReadOnly clockTimer As New System.Windows.Forms.Timer With {.Interval = 1000}
    Private ReadOnly lblClock As New Label With {.Dock = DockStyle.Right, .Width = 420, .ForeColor = Color.White, .TextAlign = ContentAlignment.MiddleRight, .Padding = New Padding(0, 0, 14, 0)}

    Private lastSaleId As Long
    Private cameraForm As CameraScanForm

    Public Sub New()
        Ui.SetupForm(Me, "Point of Sale - " & AppSettings.StoreName, 1250, 740)
        WindowState = FormWindowState.Maximized
        KeyPreview = True

        ' ---- Header ----
        Dim header = Ui.MakeHeader("POINT OF SALE")
        header.Controls.Add(lblClock)

        ' ---- Scan bar ----
        numQty.Value = 1
        numQty.Width = 90
        numQty.Font = New Font("Segoe UI", 14.0F)
        numQty.Margin = New Padding(4, 8, 4, 4)
        Dim btnAdd = Ui.MakeButton("Add", Ui.SuccessColor, 80, 40)
        Dim btnSearch = Ui.MakeButton("Search Item (F3)", Ui.InfoColor, 160, 40)
        Dim btnCamera = Ui.MakeButton("Camera (F8)", Ui.PrimaryColor, 130, 40)
        Dim scanBar As New FlowLayoutPanel With {.Dock = DockStyle.Top, .Height = 60, .BackColor = Color.White, .Padding = New Padding(8, 4, 8, 4), .WrapContents = False}
        scanBar.Controls.Add(Ui.MakeLabel("Barcode (F2):", True, 12.0F))
        scanBar.Controls.Add(txtScan)
        scanBar.Controls.Add(Ui.MakeLabel("Qty:", True, 12.0F))
        scanBar.Controls.Add(numQty)
        scanBar.Controls.Add(btnAdd)
        scanBar.Controls.Add(btnSearch)
        scanBar.Controls.Add(btnCamera)

        ' ---- Cart grid ----
        Ui.StyleGrid(grid)
        grid.Font = New Font("Segoe UI", 11.5F)
        grid.RowTemplate.Height = 34
        Dim colNo = Ui.AddColumn(grid, "", "#", fillWeight:=30)
        Ui.AddColumn(grid, "Barcode", "Barcode", fillWeight:=110)
        Ui.AddColumn(grid, "Name", "Item", fillWeight:=260)
        Ui.AddColumn(grid, "Price", "Price", "N2", 80, True)
        Ui.AddColumn(grid, "Qty", "Qty", "#,##0.###", 60, True)
        Ui.AddColumn(grid, "Unit", "Unit", fillWeight:=45)
        Ui.AddColumn(grid, "Total", "Total", "N2", 90, True)
        For Each c As DataGridViewColumn In grid.Columns
            c.SortMode = DataGridViewColumnSortMode.NotSortable
        Next
        grid.DataSource = cart
        AddHandler grid.CellFormatting, Sub(s, e)
                                            If e.ColumnIndex = colNo.Index AndAlso e.RowIndex >= 0 Then
                                                e.Value = (e.RowIndex + 1).ToString(CultureInfo.CurrentCulture)
                                                e.FormattingApplied = True
                                            End If
                                        End Sub

        Dim cartButtons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 54, .Padding = New Padding(6, 6, 6, 4), .WrapContents = False, .AutoScroll = True}
        Dim btnPlus = Ui.MakeButton("+1  (+)", Ui.SuccessColor, 90)
        Dim btnMinus = Ui.MakeButton("-1  (-)", Ui.WarningColor, 90)
        Dim btnSetQty = Ui.MakeButton("Change Qty (F4)", Ui.InfoColor, 150)
        Dim btnRemove = Ui.MakeButton("Remove (Del)", Ui.DangerColor, 130)
        Dim btnClear = Ui.MakeButton("Cancel Bill", Ui.NeutralColor, 120)
        cartButtons.Controls.AddRange(New Control() {btnPlus, btnMinus, btnSetQty, btnRemove, btnClear})

        Dim center As New Panel With {.Dock = DockStyle.Fill, .Padding = New Padding(8)}
        center.Controls.Add(grid)
        center.Controls.Add(cartButtons)
        center.Controls.Add(lblStatus)
        center.Controls.Add(lblLastItem)
        center.Controls.Add(scanBar)

        ' ---- Right: totals & payment ----
        cboDiscountType.Items.AddRange(New Object() {AppSettings.CurrencySymbol, "%"})
        cboDiscountType.SelectedIndex = 0
        cboPayment.Items.AddRange(New Object() {"Cash", "Card", "Online / Wallet", "Credit (Udhaar)"})
        cboPayment.SelectedIndex = 0
        numPaid.Font = New Font("Segoe UI", 14.0F)
        chkPrint.Checked = AppSettings.AutoPrintReceipt

        Dim discountBox As New TableLayoutPanel With {.ColumnCount = 2, .RowCount = 1, .AutoSize = True, .Margin = New Padding(0)}
        discountBox.ColumnStyles.Add(New ColumnStyle(SizeType.Percent, 100.0F))
        discountBox.ColumnStyles.Add(New ColumnStyle(SizeType.AutoSize))
        numDiscount.Dock = DockStyle.Fill
        discountBox.Controls.Add(numDiscount, 0, 0)
        discountBox.Controls.Add(cboDiscountType, 1, 0)

        Dim summary As New FieldTable(128)
        summary.AddRow("Items", lblItems)
        summary.AddRow("Sub Total", lblSubtotal)
        summary.AddRow("Discount (F6)", discountBox)
        summary.AddRow("", lblDiscount)
        summary.AddFull(lblTotal)
        summary.AddRow("Payment", cboPayment)
        summary.AddRow("Customer", txtCustomer)
        summary.AddRow("", lblCustomerBalance)
        summary.AddRow("Cash Recv. (F7)", numPaid)
        summary.AddRow("Change", lblChange)
        summary.AddRow("", chkPrint)

        Dim btnComplete = Ui.MakeButton("COMPLETE SALE  (F9)", Ui.SuccessColor, 330, 62)
        btnComplete.Font = New Font("Segoe UI", 14.0F, FontStyle.Bold)
        Dim btnReprint = Ui.MakeButton("Reprint Last (Ctrl+P)", Ui.PrimaryColor, 330, 40)
        Dim btnClose = Ui.MakeButton("Close POS (Esc)", Ui.NeutralColor, 330, 40)
        Dim rightButtons As New FlowLayoutPanel With {.Dock = DockStyle.Bottom, .Height = 170, .FlowDirection = FlowDirection.TopDown, .Padding = New Padding(12, 0, 0, 0)}
        rightButtons.Controls.AddRange(New Control() {btnComplete, btnReprint, btnClose})

        Dim right As New Panel With {.Dock = DockStyle.Right, .Width = 370, .BackColor = Color.White, .Padding = New Padding(4, 8, 4, 8)}
        right.Controls.Add(summary)
        right.Controls.Add(rightButtons)

        Controls.Add(center)
        Controls.Add(right)
        Controls.Add(header)

        ' ---- Events ----
        AddHandler txtScan.KeyDown, AddressOf Scan_KeyDown
        AddHandler btnAdd.Click, Sub() ProcessScan()
        AddHandler btnSearch.Click, Sub() SearchProduct("")
        AddHandler btnCamera.Click, Sub() OpenCamera()
        AddHandler numQty.KeyDown, Sub(s, e)
                                       If e.KeyCode = Keys.Enter Then
                                           e.SuppressKeyPress = True
                                           If txtScan.Text.Trim() <> "" Then ProcessScan() Else txtScan.Focus()
                                       End If
                                   End Sub
        AddHandler btnPlus.Click, Sub() ChangeQty(1D)
        AddHandler btnMinus.Click, Sub() ChangeQty(-1D)
        AddHandler btnSetQty.Click, Sub() SetQtyOfSelected()
        AddHandler btnRemove.Click, Sub() RemoveSelected()
        AddHandler btnClear.Click, Sub() CancelBill()
        AddHandler btnComplete.Click, Sub() CompleteSale()
        AddHandler btnReprint.Click, Sub() ReprintLast()
        AddHandler btnClose.Click, Sub() Close()
        AddHandler numDiscount.ValueChanged, Sub() Recalculate()
        AddHandler numDiscount.KeyUp, Sub() Recalculate()
        AddHandler cboDiscountType.SelectedIndexChanged, Sub()
                                                             numDiscount.Maximum = If(cboDiscountType.SelectedIndex = 1, 100D, 100000000D)
                                                             Recalculate()
                                                         End Sub
        AddHandler numPaid.ValueChanged, Sub() Recalculate()
        AddHandler numPaid.KeyUp, Sub() Recalculate()
        AddHandler numPaid.KeyDown, Sub(s, e)
                                        If e.KeyCode = Keys.Enter Then
                                            e.SuppressKeyPress = True
                                            CompleteSale()
                                        End If
                                    End Sub
        AddHandler clockTimer.Tick, Sub() UpdateClock()
        AddHandler txtCustomer.TextChanged, Sub() ShowCustomerBalance()
        AddHandler cboPayment.SelectedIndexChanged, Sub() Recalculate()
        LoadCustomerNames()

        ScannerInput.RedirectTypingTo(Me, txtScan)
        Recalculate()
        Ui.FinishLayout(Me)
    End Sub

    Protected Overrides Sub OnLoad(e As EventArgs)
        MyBase.OnLoad(e)
        UpdateClock()
        clockTimer.Start()
    End Sub

    Protected Overrides Sub OnShown(e As EventArgs)
        MyBase.OnShown(e)
        txtScan.Focus()
    End Sub

    Protected Overrides Sub OnFormClosing(e As FormClosingEventArgs)
        If cart.Count > 0 AndAlso Not Ui.Confirm("The current bill has items. Close POS and discard it?") Then
            e.Cancel = True
            Return
        End If
        clockTimer.Stop()
        MyBase.OnFormClosing(e)
    End Sub

    Protected Overrides Sub OnKeyDown(e As KeyEventArgs)
        MyBase.OnKeyDown(e)
        Dim typing = ScannerInput.IsTextEntry(ScannerInput.FocusedControl(Me))
        Select Case True
            Case e.KeyCode = Keys.F2 : FocusScan()
            Case e.KeyCode = Keys.F3 : SearchProduct("")
            Case e.KeyCode = Keys.F8 : OpenCamera()
            Case e.KeyCode = Keys.F4 : SetQtyOfSelected()
            Case e.KeyCode = Keys.F6
                numDiscount.Focus()
                numDiscount.Select(0, numDiscount.Text.Length)
            Case e.KeyCode = Keys.F7
                numPaid.Focus()
                numPaid.Select(0, numPaid.Text.Length)
            Case e.KeyCode = Keys.F9 : CompleteSale()
            Case e.KeyCode = Keys.P AndAlso e.Control : ReprintLast()
            Case e.KeyCode = Keys.Escape
                If txtScan.Focused AndAlso txtScan.Text <> "" Then txtScan.Clear() Else Close()
            Case e.KeyCode = Keys.Delete AndAlso Not typing : RemoveSelected()
            Case (e.KeyCode = Keys.Add OrElse e.KeyCode = Keys.Oemplus) AndAlso (Not typing OrElse txtScan.Focused AndAlso txtScan.Text = "")
                ChangeQty(1D)
            Case (e.KeyCode = Keys.Subtract OrElse e.KeyCode = Keys.OemMinus) AndAlso (Not typing OrElse txtScan.Focused AndAlso txtScan.Text = "")
                ChangeQty(-1D)
            Case e.KeyCode = Keys.Up AndAlso txtScan.Focused : MoveSelection(-1)
            Case e.KeyCode = Keys.Down AndAlso txtScan.Focused : MoveSelection(1)
            Case Else : Return
        End Select
        e.Handled = True
        e.SuppressKeyPress = True
    End Sub

    Private Sub UpdateClock()
        lblClock.Text = $"Cashier: {Session.FullName}    |    {Date.Now:dd-MMM-yyyy  hh:mm:ss tt}"
    End Sub

    Private Sub FocusScan()
        txtScan.Focus()
        txtScan.SelectAll()
    End Sub

    Private Sub SetStatus(message As String, Optional isError As Boolean = False)
        lblStatus.Text = message
        lblStatus.ForeColor = If(isError, Ui.DangerColor, Ui.SuccessColor)
        If isError Then SystemSounds.Exclamation.Play()
    End Sub

    ' ---------- Scanning ----------

    ''' <summary>Camera scanner window stays open next to the bill; every code read is added.</summary>
    Private Sub OpenCamera()
        If cameraForm IsNot Nothing AndAlso Not cameraForm.IsDisposed Then
            cameraForm.Activate()
            Return
        End If
        cameraForm = New CameraScanForm(continuous:=True)
        AddHandler cameraForm.BarcodeScanned, Sub(code)
                                                  txtScan.Text = code
                                                  ProcessScan()
                                              End Sub
        cameraForm.StartPosition = FormStartPosition.Manual
        Dim area = Screen.FromControl(Me).WorkingArea
        cameraForm.Size = New Size(520, 440)
        cameraForm.Location = New Point(area.Right - cameraForm.Width - 20, area.Bottom - cameraForm.Height - 20)
        cameraForm.Show(Me)
    End Sub

    Private Sub Scan_KeyDown(sender As Object, e As KeyEventArgs)
        If e.KeyCode = Keys.Enter Then
            e.SuppressKeyPress = True
            ProcessScan()
        End If
    End Sub

    Private Sub ProcessScan()
        Dim raw = txtScan.Text.Trim()
        txtScan.Clear()
        FocusScan()
        If raw = "" Then Return

        ' "qty*barcode" shortcut, e.g. 6*8964000123456
        Dim qty = numQty.Value
        Dim code = raw
        Dim star = raw.IndexOf("*"c)
        If star > 0 Then
            Dim q As Decimal
            If Decimal.TryParse(raw.Substring(0, star), NumberStyles.Number, CultureInfo.CurrentCulture, q) AndAlso q > 0 Then
                qty = q
                code = raw.Substring(star + 1).Trim()
            End If
        End If

        Dim product = ProductRepo.FindByBarcode(code)
        If product Is Nothing Then
            If code.Any(AddressOf Char.IsLetter) Then
                ' Looks like a name: let the cashier pick from a list.
                SearchProduct(code, qty)
            Else
                SetStatus($"Product not found for barcode: {code}", isError:=True)
            End If
            Return
        End If

        AddToCart(product, qty)
        numQty.Value = 1
    End Sub

    Private Sub SearchProduct(text As String, Optional qty As Decimal = 0D)
        Using dlg As New ProductSearchDialog(text)
            If dlg.ShowDialog(Me) = DialogResult.OK Then
                Dim product = ProductRepo.GetById(dlg.SelectedProductId)
                If product IsNot Nothing Then
                    AddToCart(product, If(qty > 0, qty, numQty.Value))
                    numQty.Value = 1
                End If
            End If
        End Using
        FocusScan()
    End Sub

    Private Sub AddToCart(product As DataRow, qty As Decimal)
        Dim id = Ui.ToLng(product("id"))
        Dim available = Ui.ToDec(product("stock"))
        Dim existing = cart.FirstOrDefault(Function(c) c.ProductId = id)
        Dim newQty = qty + If(existing Is Nothing, 0D, existing.Qty)

        If Not AppSettings.AllowNegativeStock AndAlso newQty > available Then
            SetStatus($"Not enough stock for '{Ui.ToStr(product("name"))}'. Available: {AppSettings.Qty(available)}", isError:=True)
            Return
        End If

        Dim index As Integer
        If existing IsNot Nothing Then
            existing.Qty = newQty
            existing.Available = available
            index = cart.IndexOf(existing)
            cart.ResetItem(index)
        Else
            cart.Add(New CartItem With {
                .ProductId = id,
                .Barcode = Ui.ToStr(product("barcode")),
                .Name = Ui.ToStr(product("name")),
                .Unit = Ui.ToStr(product("unit")),
                .Price = Ui.ToDec(product("sale_price")),
                .Cost = Ui.ToDec(product("cost_price")),
                .Qty = qty,
                .Available = available
            })
            index = cart.Count - 1
        End If

        SelectIndex(index)
        Dim item = cart(index)
        lblLastItem.Text = $"{item.Name}   {AppSettings.Money(item.Price)} x {AppSettings.Qty(item.Qty)} = {AppSettings.Money(item.Total)}"
        SetStatus($"Added: {item.Name}")
        Recalculate()
    End Sub

    ' ---------- Cart editing ----------

    Private Function SelectedItem() As CartItem
        If grid.CurrentRow Is Nothing OrElse grid.CurrentRow.Index < 0 OrElse grid.CurrentRow.Index >= cart.Count Then Return Nothing
        Return cart(grid.CurrentRow.Index)
    End Function

    Private Sub SelectIndex(index As Integer)
        If index < 0 OrElse index >= grid.Rows.Count Then Return
        grid.CurrentCell = grid.Rows(index).Cells(1)
        grid.FirstDisplayedScrollingRowIndex = Math.Max(0, index - 5)
    End Sub

    Private Sub MoveSelection(delta As Integer)
        If cart.Count = 0 Then Return
        Dim current = If(grid.CurrentRow Is Nothing, 0, grid.CurrentRow.Index)
        SelectIndex(Math.Min(cart.Count - 1, Math.Max(0, current + delta)))
    End Sub

    Private Sub ChangeQty(delta As Decimal)
        Dim item = SelectedItem()
        If item Is Nothing Then Return
        Dim newQty = item.Qty + delta
        If newQty <= 0 Then
            RemoveSelected()
        Else
            SetQty(item, newQty)
        End If
    End Sub

    Private Sub SetQtyOfSelected()
        Dim item = SelectedItem()
        If item Is Nothing Then
            SetStatus("Select an item in the bill first.", isError:=True)
            Return
        End If
        Dim q = Ui.PromptNumber(Me, "Change Quantity", item.Name, item.Qty)
        If q.HasValue Then
            If q.Value <= 0 Then RemoveSelected() Else SetQty(item, q.Value)
        End If
        FocusScan()
    End Sub

    Private Sub SetQty(item As CartItem, qty As Decimal)
        If Not AppSettings.AllowNegativeStock AndAlso qty > item.Available Then
            SetStatus($"Only {AppSettings.Qty(item.Available)} of '{item.Name}' in stock.", isError:=True)
            Return
        End If
        item.Qty = qty
        Dim index = cart.IndexOf(item)
        cart.ResetItem(index)
        SelectIndex(index)
        Recalculate()
    End Sub

    Private Sub RemoveSelected()
        Dim item = SelectedItem()
        If item Is Nothing Then Return
        Dim index = cart.IndexOf(item)
        cart.Remove(item)
        SetStatus($"Removed: {item.Name}")
        SelectIndex(Math.Min(index, cart.Count - 1))
        Recalculate()
        FocusScan()
    End Sub

    Private Sub CancelBill()
        If cart.Count = 0 Then Return
        If Not Ui.Confirm("Cancel this bill and clear all items?") Then Return
        ResetSale()
        SetStatus("Bill cancelled.")
    End Sub

    Private Sub ResetSale()
        cart.Clear()
        numDiscount.Value = 0
        numPaid.Value = 0
        txtCustomer.Clear()
        LoadCustomerNames()
        cboPayment.SelectedIndex = 0
        numQty.Value = 1
        lblLastItem.Text = "Scan a product to start the bill"
        Recalculate()
        FocusScan()
    End Sub

    ' ---------- Totals & payment ----------

    Private Function Totals() As (SubTotal As Decimal, Discount As Decimal, Total As Decimal)
        Dim subTotal = cart.Sum(Function(c) c.Total)
        Dim value = Ui.TypedValue(numDiscount)
        Dim discount = If(cboDiscountType.SelectedIndex = 1, Math.Round(subTotal * value / 100D, 2), value)
        discount = Math.Min(discount, subTotal)
        Return (subTotal, discount, subTotal - discount)
    End Function

    Private Sub Recalculate()
        Dim t = Totals()
        lblItems.Text = $"{cart.Count} items  /  {AppSettings.Qty(cart.Sum(Function(c) c.Qty))} qty"
        lblSubtotal.Text = AppSettings.Money(t.SubTotal)
        lblDiscount.Text = If(t.Discount > 0, "- " & AppSettings.Money(t.Discount), "")
        lblTotal.Text = AppSettings.Money(t.Total)

        Dim paid = Ui.TypedValue(numPaid)
        If paid <= 0 Then
            lblChange.Text = "-"
            lblChange.ForeColor = Ui.NeutralColor
        ElseIf paid >= t.Total Then
            lblChange.Text = AppSettings.Money(paid - t.Total)
            lblChange.ForeColor = Ui.SuccessColor
        ElseIf IsCreditSale Then
            lblChange.Text = "Udhaar " & AppSettings.Money(t.Total - paid)
            lblChange.ForeColor = Ui.WarningColor
        Else
            lblChange.Text = "Short " & AppSettings.Money(t.Total - paid)
            lblChange.ForeColor = Ui.DangerColor
        End If
    End Sub

    Private Sub CompleteSale()
        If cart.Count = 0 Then
            SetStatus("Bill is empty. Scan some products first.", isError:=True)
            FocusScan()
            Return
        End If

        Dim t = Totals()
        Dim paid = Ui.TypedValue(numPaid)
        Dim method = cboPayment.Text
        Dim isCredit = IsCreditSale
        Dim customerName = txtCustomer.Text.Trim()
        Dim customerId = If(customerName = "", 0L, CustomerRepo.FindByName(customerName))

        If isCredit Then
            ' Udhaar: the customer must be known; whatever is not paid now goes to their khata.
            If customerName = "" Then
                Ui.ShowError("Enter the customer name for a credit (udhaar) sale.")
                txtCustomer.Focus()
                Return
            End If
            If customerId = 0 Then
                If Not Ui.Confirm($"'{customerName}' is a new customer.{vbCrLf}Create the customer's account (khata)?") Then
                    txtCustomer.Focus()
                    Return
                End If
                customerId = CustomerRepo.Save(0, customerName, "", "")
            End If
        Else
            ' No amount entered = customer paid the exact amount.
            If paid <= 0 Then paid = t.Total
            If paid < t.Total Then
                Ui.ShowError($"Received amount is less than the total ({AppSettings.Money(t.Total)}).{vbCrLf}" &
                             "Choose payment 'Credit (Udhaar)' to put the rest on the customer's account.")
                numPaid.Focus()
                Return
            End If
        End If

        Dim result As (Id As Long, InvoiceNo As String)
        Try
            result = SalesService.SaveSale(cart.ToList(), t.Discount, paid, method, customerName, customerId)
        Catch ex As Exception
            Ui.ShowError("Sale could not be saved: " & ex.Message)
            Return
        End Try

        lastSaleId = result.Id
        If chkPrint.Checked Then ReceiptPrinter.PrintSale(result.Id, owner:=Me)

        Dim change = Math.Max(0D, paid - t.Total)
        Dim due = Math.Max(0D, t.Total - paid)
        ResetSale()
        Dim message = $"Invoice: {result.InvoiceNo}{vbCrLf}{vbCrLf}Total:   {AppSettings.Money(t.Total)}{vbCrLf}Received:   {AppSettings.Money(paid)}{vbCrLf}{vbCrLf}"
        If due > 0 Then
            Dim balance = CustomerRepo.Balance(customerId)
            SetStatus($"Sale saved: {result.InvoiceNo}   Udhaar {AppSettings.Money(due)} added to {customerName}")
            lblLastItem.Text = $"UDHAAR: {AppSettings.Money(due)}   ({customerName} total balance {AppSettings.Money(balance)})"
            message &= $"UDHAAR (credit):   {AppSettings.Money(due)}{vbCrLf}{customerName} total balance:   {AppSettings.Money(balance)}"
        Else
            SetStatus($"Sale saved: {result.InvoiceNo}   Total {AppSettings.Money(t.Total)}   Change {AppSettings.Money(change)}")
            lblLastItem.Text = $"CHANGE TO RETURN:  {AppSettings.Money(change)}"
            message &= $"CHANGE:   {AppSettings.Money(change)}"
        End If
        MessageBox.Show(Me, message, "Sale Completed", MessageBoxButtons.OK, MessageBoxIcon.Information)
        FocusScan()
    End Sub

    Private ReadOnly Property IsCreditSale As Boolean
        Get
            Return cboPayment.SelectedIndex = 3
        End Get
    End Property

    Private Sub LoadCustomerNames()
        Try
            Dim names As New AutoCompleteStringCollection()
            names.AddRange(CustomerRepo.Names())
            txtCustomer.AutoCompleteCustomSource = names
        Catch ex As Exception
            ' Autocomplete is a convenience only.
        End Try
    End Sub

    Private Sub ShowCustomerBalance()
        Dim name = txtCustomer.Text.Trim()
        Dim id = If(name = "", 0L, CustomerRepo.FindByName(name))
        If id = 0 Then
            lblCustomerBalance.Text = If(name = "", "", "New customer")
            Return
        End If
        Dim balance = CustomerRepo.Balance(id)
        lblCustomerBalance.Text = If(balance > 0, "Previous udhaar: " & AppSettings.Money(balance), "No udhaar pending")
    End Sub

    Private Sub ReprintLast()
        If lastSaleId = 0 Then
            lastSaleId = Ui.ToLng(Db.Scalar("SELECT id FROM sales WHERE user_id = @u ORDER BY id DESC LIMIT 1", Db.P("@u", Session.UserId)))
        End If
        If lastSaleId = 0 Then
            SetStatus("No previous sale to reprint.", isError:=True)
            Return
        End If
        ReceiptPrinter.PrintSale(lastSaleId, preview:=True, owner:=Me)
        FocusScan()
    End Sub

End Class
