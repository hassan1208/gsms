Imports System.Data
Imports System.Globalization

''' <summary>One line in the POS cart.</summary>
Public Class CartItem
    Public Property ProductId As Long
    Public Property Barcode As String
    Public Property Name As String
    Public Property Unit As String
    Public Property Price As Decimal
    Public Property Cost As Decimal
    Public Property Qty As Decimal
    Public Property Available As Decimal

    Public ReadOnly Property Total As Decimal
        Get
            Return Math.Round(Price * Qty, 2, MidpointRounding.AwayFromZero)
        End Get
    End Property
End Class

Public Module ProductRepo

    Private Const SelectSql As String =
        "SELECT p.id, p.barcode, p.name, p.unit, p.cost_price, p.sale_price, p.stock, p.reorder_level,
                p.category_id, p.is_active
         FROM products p "

    Public Function FindByBarcode(barcode As String) As DataRow
        Dim dt = Db.Query(SelectSql & "WHERE p.barcode = @b AND p.is_active = 1", Db.P("@b", barcode.Trim()))
        Return If(dt.Rows.Count = 0, Nothing, dt.Rows(0))
    End Function

    Public Function GetById(id As Long) As DataRow
        Dim dt = Db.Query(SelectSql & "WHERE p.id = @id", Db.P("@id", id))
        Return If(dt.Rows.Count = 0, Nothing, dt.Rows(0))
    End Function

End Module

Public Module SalesService

    ''' <summary>
    ''' Saves a sale in one transaction: invoice, line items and stock deduction.
    ''' With a <paramref name="customerId"/> the bill may be paid partly or not at all;
    ''' the unpaid part is recorded as the customer's udhaar (credit).
    ''' </summary>
    Public Function SaveSale(items As IList(Of CartItem), discount As Decimal, paid As Decimal,
                             paymentMethod As String, customer As String,
                             Optional customerId As Long = 0) As (Id As Long, InvoiceNo As String)
        If items Is Nothing OrElse items.Count = 0 Then Throw New InvalidOperationException("Cart is empty.")

        Dim subTotal = items.Sum(Function(i) i.Total)
        discount = Math.Min(Math.Max(discount, 0D), subTotal)
        Dim total = subTotal - discount
        paid = Math.Max(0D, paid)
        If paid < total AndAlso customerId = 0 Then
            Throw New InvalidOperationException("Paid amount is less than the bill total. Select a customer to give udhaar.")
        End If
        Dim due = Math.Max(0D, total - paid)
        Dim change = Math.Max(0D, paid - total)

        Using cn = Db.Open(), tx = cn.BeginTransaction()
            ' Check stock with the latest numbers from the database
            If Not AppSettings.AllowNegativeStock Then
                For Each it In items
                    Dim stock = Ui.ToDec(Db.ScalarTx(cn, tx, "SELECT stock FROM products WHERE id = @id", Db.P("@id", it.ProductId)))
                    If stock < it.Qty Then
                        Throw New InvalidOperationException($"Not enough stock for '{it.Name}'. Available: {AppSettings.Qty(stock)}")
                    End If
                Next
            End If

            Dim prefix = "INV" & Date.Now.ToString("yyMMdd", CultureInfo.InvariantCulture) & "-"
            Dim lastSeq = Ui.ToLng(Db.ScalarTx(cn, tx,
                "SELECT COALESCE(MAX(CAST(SUBSTR(invoice_no, @start) AS INTEGER)), 0) FROM sales WHERE invoice_no LIKE @p",
                Db.P("@start", prefix.Length + 1), Db.P("@p", prefix & "%")))
            Dim invoiceNo = prefix & (lastSeq + 1).ToString("D4", CultureInfo.InvariantCulture)

            Dim saleId = Ui.ToLng(Db.ScalarTx(cn, tx,
                "INSERT INTO sales(invoice_no, sale_date, user_id, customer_id, customer_name, sub_total, discount, total, paid, change_due, due, payment_method)
                 VALUES(@inv, @date, @user, @cid, @cust, @sub, @disc, @total, @paid, @change, @due, @method);
                 SELECT last_insert_rowid();",
                Db.P("@inv", invoiceNo), Db.P("@date", Db.NowText()), Db.P("@user", Session.UserId),
                Db.P("@cid", If(customerId = 0, Nothing, CObj(customerId))),
                Db.P("@cust", If(String.IsNullOrWhiteSpace(customer), Nothing, customer.Trim())),
                Db.P("@sub", subTotal), Db.P("@disc", discount), Db.P("@total", total),
                Db.P("@paid", paid), Db.P("@change", change), Db.P("@due", due), Db.P("@method", paymentMethod)))

            For Each it In items
                Db.ExecuteTx(cn, tx,
                    "INSERT INTO sale_items(sale_id, product_id, barcode, product_name, qty, unit_price, cost_price, line_total)
                     VALUES(@sale, @pid, @bc, @name, @qty, @price, @cost, @line)",
                    Db.P("@sale", saleId), Db.P("@pid", it.ProductId), Db.P("@bc", it.Barcode), Db.P("@name", it.Name),
                    Db.P("@qty", it.Qty), Db.P("@price", it.Price), Db.P("@cost", it.Cost), Db.P("@line", it.Total))
                Db.ExecuteTx(cn, tx, "UPDATE products SET stock = stock - @qty WHERE id = @id",
                             Db.P("@qty", it.Qty), Db.P("@id", it.ProductId))
            Next

            tx.Commit()
            Return (saleId, invoiceNo)
        End Using
    End Function

    ''' <summary>Cancels a sale and puts the items back in stock.</summary>
    Public Sub VoidSale(saleId As Long)
        Using cn = Db.Open(), tx = cn.BeginTransaction()
            Db.ExecuteTx(cn, tx,
                "UPDATE products SET stock = stock + (SELECT COALESCE(SUM(si.qty), 0) FROM sale_items si
                                                     WHERE si.sale_id = @id AND si.product_id = products.id)
                 WHERE id IN (SELECT product_id FROM sale_items WHERE sale_id = @id)", Db.P("@id", saleId))
            Db.ExecuteTx(cn, tx, "DELETE FROM sale_items WHERE sale_id = @id", Db.P("@id", saleId))
            Db.ExecuteTx(cn, tx, "DELETE FROM sales WHERE id = @id", Db.P("@id", saleId))
            tx.Commit()
        End Using
    End Sub

    ''' <summary>Receives stock (purchase) and updates the product prices.</summary>
    Public Sub AddStock(productId As Long, qty As Decimal, costPrice As Decimal, salePrice As Decimal,
                        supplier As String, note As String)
        Using cn = Db.Open(), tx = cn.BeginTransaction()
            Db.ExecuteTx(cn, tx,
                "INSERT INTO stock_in(product_id, qty, cost_price, supplier, note, user_id, created_at)
                 VALUES(@pid, @qty, @cost, @sup, @note, @user, @now)",
                Db.P("@pid", productId), Db.P("@qty", qty), Db.P("@cost", costPrice),
                Db.P("@sup", supplier), Db.P("@note", note), Db.P("@user", Session.UserId), Db.P("@now", Db.NowText()))
            Db.ExecuteTx(cn, tx,
                "UPDATE products SET stock = stock + @qty, cost_price = @cost, sale_price = @price WHERE id = @id",
                Db.P("@qty", qty), Db.P("@cost", costPrice), Db.P("@price", salePrice), Db.P("@id", productId))
            tx.Commit()
        End Using
    End Sub

End Module

Public Module ReportsRepo

    Public Structure SalesSummary
        Public Invoices As Long
        Public Gross As Decimal
        Public Discount As Decimal
        Public Net As Decimal
        Public Profit As Decimal
        ''' <summary>Udhaar given on these sales.</summary>
        Public Due As Decimal
    End Structure

    ''' <summary>Totals for sales between dFrom (inclusive) and dTo (exclusive).</summary>
    Public Function GetSummary(dFrom As Date, dTo As Date) As SalesSummary
        Dim pf = Db.P("@f", Db.DateKey(dFrom)), pt = Db.P("@t", Db.DateKey(dTo))
        Dim t = Db.Query("SELECT COUNT(*) AS cnt, COALESCE(SUM(sub_total), 0) AS gross,
                                 COALESCE(SUM(discount), 0) AS disc, COALESCE(SUM(total), 0) AS net,
                                 COALESCE(SUM(due), 0) AS due
                          FROM sales WHERE sale_date >= @f AND sale_date < @t", pf, pt).Rows(0)
        Dim itemProfit = Ui.ToDec(Db.Scalar(
            "SELECT COALESCE(SUM(si.line_total - si.cost_price * si.qty), 0)
             FROM sale_items si JOIN sales s ON s.id = si.sale_id
             WHERE s.sale_date >= @f AND s.sale_date < @t",
            Db.P("@f", Db.DateKey(dFrom)), Db.P("@t", Db.DateKey(dTo))))

        Return New SalesSummary With {
            .Invoices = Ui.ToLng(t("cnt")),
            .Gross = Ui.ToDec(t("gross")),
            .Discount = Ui.ToDec(t("disc")),
            .Net = Ui.ToDec(t("net")),
            .Profit = itemProfit - Ui.ToDec(t("disc")),
            .Due = Ui.ToDec(t("due"))
        }
    End Function

End Module

''' <summary>Customers and their udhaar (credit) accounts.</summary>
Public Module CustomerRepo

    ' Balance = udhaar left on sales minus payments received later.
    Private Const BalanceSql As String =
        "SELECT id, name, phone, address, credit, received, credit - received AS balance, last_activity FROM (
            SELECT c.id, c.name, COALESCE(c.phone, '') AS phone, COALESCE(c.address, '') AS address,
                   COALESCE((SELECT SUM(s.due) FROM sales s WHERE s.customer_id = c.id), 0) AS credit,
                   COALESCE((SELECT SUM(p.amount) FROM customer_payments p WHERE p.customer_id = c.id), 0) AS received,
                   MAX(COALESCE((SELECT MAX(s.sale_date) FROM sales s WHERE s.customer_id = c.id), ''),
                       COALESCE((SELECT MAX(p.created_at) FROM customer_payments p WHERE p.customer_id = c.id), '')) AS last_activity
            FROM customers c) "

    Public Function List(Optional search As String = "", Optional onlyWithBalance As Boolean = False) As DataTable
        Dim sql = BalanceSql & "WHERE (name LIKE @q OR phone LIKE @q)"
        If onlyWithBalance Then sql &= " AND credit - received > 0.004"
        Return Db.Query(sql & " ORDER BY name", Db.P("@q", "%" & search.Trim() & "%"))
    End Function

    Public Function Balance(customerId As Long) As Decimal
        Dim dt = Db.Query(BalanceSql & "WHERE id = @id", Db.P("@id", customerId))
        Return If(dt.Rows.Count = 0, 0D, Ui.ToDec(dt.Rows(0)("balance")))
    End Function

    Public Function TotalOutstanding() As Decimal
        Return Ui.ToDec(Db.Scalar("SELECT COALESCE(SUM(balance), 0) FROM (" & BalanceSql & ") WHERE balance > 0"))
    End Function

    Public Function FindByName(name As String) As Long
        Return Ui.ToLng(Db.Scalar("SELECT id FROM customers WHERE name = @n", Db.P("@n", name.Trim())))
    End Function

    Public Function Names() As String()
        Return Db.Query("SELECT name FROM customers ORDER BY name").Rows.Cast(Of DataRow)().
            Select(Function(r) Ui.ToStr(r("name"))).ToArray()
    End Function

    Public Function Save(id As Long, name As String, phone As String, address As String) As Long
        If String.IsNullOrWhiteSpace(name) Then Throw New ArgumentException("Customer name is required.")
        name = name.Trim()
        phone = If(phone, "").Trim()
        address = If(address, "").Trim()
        Dim other = FindByName(name)
        If other <> 0 AndAlso other <> id Then Throw New InvalidOperationException($"Customer '{name}' already exists.")
        If id = 0 Then
            Return Ui.ToLng(Db.Scalar("INSERT INTO customers(name, phone, address, created_at) VALUES(@n, @p, @a, @now);
                                       SELECT last_insert_rowid();",
                                      Db.P("@n", name), Db.P("@p", phone), Db.P("@a", address), Db.P("@now", Db.NowText())))
        End If
        Db.Execute("UPDATE customers SET name = @n, phone = @p, address = @a WHERE id = @id",
                   Db.P("@n", name), Db.P("@p", phone), Db.P("@a", address), Db.P("@id", id))
        Db.Execute("UPDATE sales SET customer_name = @n WHERE customer_id = @id", Db.P("@n", name), Db.P("@id", id))
        Return id
    End Function

    Public Sub ReceivePayment(customerId As Long, amount As Decimal, note As String)
        If amount <= 0 Then Throw New ArgumentException("Amount must be more than zero.")
        Db.Execute("INSERT INTO customer_payments(customer_id, amount, note, user_id, created_at) VALUES(@c, @a, @n, @u, @now)",
                   Db.P("@c", customerId), Db.P("@a", amount), Db.P("@n", note), Db.P("@u", Session.UserId), Db.P("@now", Db.NowText()))
    End Sub

    ''' <summary>Deletes a customer that has no sales or payments. Returns False if history exists.</summary>
    Public Function Delete(customerId As Long) As Boolean
        Dim used = Ui.ToLng(Db.Scalar("SELECT (SELECT COUNT(*) FROM sales WHERE customer_id = @id) +
                                              (SELECT COUNT(*) FROM customer_payments WHERE customer_id = @id)", Db.P("@id", customerId)))
        If used > 0 Then Return False
        Db.Execute("DELETE FROM customers WHERE id = @id", Db.P("@id", customerId))
        Return True
    End Function

    ''' <summary>Khata: every udhaar sale and payment, oldest first, with a running balance.</summary>
    Public Function Ledger(customerId As Long) As DataTable
        Dim dt = Db.Query(
            "SELECT entry_date, details, debit, credit FROM (
                SELECT s.sale_date AS entry_date,
                       'Sale ' || s.invoice_no || '  (bill ' || printf('%.2f', s.total) || ', paid ' || printf('%.2f', s.paid) || ')' AS details,
                       s.due AS debit, 0.0 AS credit, s.id AS seq, 0 AS kind
                FROM sales s WHERE s.customer_id = @id AND s.due > 0
                UNION ALL
                SELECT p.created_at, 'Payment received' || CASE WHEN COALESCE(p.note, '') <> '' THEN ' - ' || p.note ELSE '' END,
                       0.0, p.amount, p.id, 1
                FROM customer_payments p WHERE p.customer_id = @id)
             ORDER BY entry_date, kind, seq", Db.P("@id", customerId))
        dt.Columns.Add("balance", GetType(Double))
        Dim running = 0D
        For Each r As DataRow In dt.Rows
            running += Ui.ToDec(r("debit")) - Ui.ToDec(r("credit"))
            r("balance") = CDbl(running)
        Next
        Return dt
    End Function

End Module
