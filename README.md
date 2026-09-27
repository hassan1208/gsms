# General Store Management System (VB.NET)

General store / kiryana store ke liye Windows desktop app. **VB.NET (WinForms, .NET 8)** mein bana hai aur **SQLite** database use karta hai. Is mein **barcode scanning** built-in hai.

## Features

| Module | Kya karta hai |
|---|---|
| **Login / Users** | Admin aur Cashier roles, passwords PBKDF2 hash mein save hote hain |
| **Dashboard** | Aaj ki sale, bills, profit, total products, low-stock alert, recent sales |
| **POS / Billing** | Barcode scan karo aur item bill mein add ho jata hai. Qty, discount (Rs ya %), Cash/Card/Online/Udhaar, change calculate hota hai, receipt print hoti hai |
| **Products** | Add / edit / delete, category, unit (pcs, kg, ltr...), cost aur sale price, stock, reorder level |
| **Barcode generator** | Jin items par barcode nahi (loose items), un ke liye 13-digit barcode khud generate karta hai (EAN-13 check digit ke sath) |
| **Barcode labels** | A4 sticker sheet (3x8) ya label printer (50x25mm) par stickers print karta hai, price aur store name ke sath |
| **Customers / Udhaar** | Customer ka khata: udhaar par sale (poori ya kuch raqam le kar), baad mein payment receive, har customer ka balance aur ledger (running balance ke sath) |
| **Stock In** | Supplier ka maal scan karke stock mein add karo. Naya barcode aaye to wahin se product ban jata hai |
| **Reports** | Date-wise invoices, product-wise sale, profit, stock value, low stock, CSV export, receipt reprint, sale void/return |
| **Settings** | Store name, address, phone, currency, receipt footer, printers, backup / restore |

## Barcode scanner kaise kaam karta hai

Koi bhi aam **USB ya Bluetooth barcode scanner** chalega. Koi driver ya extra software nahi chahiye.

- Scanner keyboard ki tarah kaam karta hai (HID / "keyboard wedge" mode). Code type karta hai aur aakhir mein **Enter** dabata hai. Zyada tar scanners mein yeh by default on hota hai.
- **POS** mein: bas product scan karo aur woh bill mein add ho jayega. Dobara scan karoge to qty barh jayegi.
- Agar focus kisi aur jagah ho (grid, button waghera), phir bhi scan barcode box mein hi jata hai.
- `6*8964000123456` likh kar Enter dabao to ek hi dafa 6 pieces add ho jate hain.
- **Stock In** aur **Products** screen mein bhi scan kar sakte ho. Product na mile to app naya product banane ka pooch leti hai.
- **Scanner nahi hai?** Laptop ka camera ya koi bhi USB webcam bhi chalega. POS mein **Camera (F8)** dabao, ek chhoti camera window khulti hai jo har scan kiya hua item bill mein daal deti hai (same item 2 second tak dobara nahi parhta). Product banate waqt aur Stock In mein bhi **Camera** button hai. Barcode ko camera se 10-20 cm door, seedha aur roshni mein rakhein.
- Har receipt par invoice number ka barcode print hota hai. **Reports** mein us ko scan karo to woh bill foran khul jata hai (reprint ya return ke liye).
- Barcode images app khud banati hai (Code 128). Camera scanning ke liye FlashCap (camera) aur ZXing.Net (decoding) libraries use hoti hain, jo NuGet se khud aa jati hain.

## Keyboard shortcuts (POS)

| Key | Kaam |
|---|---|
| F2 | Barcode box par focus |
| F3 | Product naam se search |
| F4 | Selected item ki qty change |
| + / - | Selected item ki qty 1 barhao / ghatao |
| Del | Selected item remove |
| F6 | Discount |
| F7 | Cash received |
| F8 | Camera se scan |
| F9 | Sale complete |
| Ctrl+P | Last receipt reprint |
| Esc | Close |

Main screen par: F1 POS, F2 Products, F3 Stock In, F4 Reports, F6 Customers / Udhaar.

## Udhaar (credit) kaise dein

1. POS mein bill banao, **Payment** mein `Credit (Udhaar)` chuno.
2. **Customer** mein naam likho (purane customers ka naam khud suggest hota hai aur un ka pichla udhaar bhi dikhta hai). Naya naam ho to app khata banane ka poochti hai.
3. Customer abhi kuch raqam de raha ho to **Cash Recv.** mein likh do, warna khali chhor do. Baqi raqam customer ke khate mein chali jati hai.
4. Receipt par "UDHAAR (this bill)" aur customer ka total balance print hota hai.
5. Payment wapas milne par **Customers / Udhaar** screen mein customer select karke **Receive Payment** karo. **Khata / Ledger** mein poori history running balance ke sath milti hai.

## Requirements

- Windows 10 / 11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) **ya** Visual Studio 2022 (".NET desktop development" workload ke sath)

## Run kaise karein

**Visual Studio:** `GeneralStore.sln` open karo aur **F5** dabao.

**Command line:**

```bat
cd src\GeneralStore
dotnet run
```

**Default login:** username `admin`, password `admin123`. Pehli dafa login ke baad **Change Password** se password zaroor badal lein.

Pehli dafa chalane par kuch sample categories aur products khud add ho jate hain taake aap foran test kar sakein. Yeh baad mein delete kar sakte hain.

## Setup / EXE banana

**Sab se aasaan tareeqa:** GitHub par repo ke **Actions** tab mein **Build** workflow kholo. Har push par yeh Windows par project build karta hai. Aakhri successful run ke neeche **GeneralStore-win-x64** artifact download karo. Us ke andar `GeneralStore.exe` hai jo .NET install kiye baghair chalti hai.

Ya khud banana ho to:

```bat
cd src\GeneralStore
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

EXE `bin\Release\net8.0-windows\win-x64\publish\` folder mein milegi. Isay store ke computer par copy karke chala lein.

## Tests

`tests/GeneralStore.Tests` mein automated tests hain: sale save, stock, invoice numbering, discount/profit, udhaar ledger, backup/restore, purane database ki migration, aur yeh ke app ke banaye hue barcodes camera decoder se wapas parhe ja sakte hain. Chalane ke liye:

```bat
dotnet test GeneralStore.sln
```

GitHub par har push ke baad yeh tests khud chalte hain.

## Data kahan save hota hai

Database file: `%LOCALAPPDATA%\GeneralStore\store.db`

**Settings > Backup Database** se roz ka backup USB ya Google Drive folder mein save kar lein. **Restore Backup** se wapas la sakte hain.

## Printers

- **Receipt:** 80mm thermal printer ya koi bhi Windows printer. Printer Settings mein select karo.
- **Barcode labels:** A4 sticker sheet (24 labels) ya 50x25mm label printer (jaise Zebra, TSC, Xprinter).

## Project structure

```
src/GeneralStore/
  Program.vb              Startup: login screen phir main window
  Core/Db.vb              SQLite database, tables, backup/restore
  Core/Session.vb         Login, password hashing, settings
  Core/Ui.vb              Common styling aur helpers
  Barcode/Code128.vb      Barcode image generator (Code 128)
  Barcode/BarcodeHelper.vb EAN-13 barcode numbers, scanner input helper
  Barcode/BarcodeDecoder.vb Camera images se barcode parhna (ZXing.Net)
  Services/StoreServices.vb Sale save, stock in, void, reports, customers / udhaar
  Printing/Printing.vb    Receipt aur barcode label printing
  Forms/                  Saari screens (Login, Main, POS, Products, Stock In, Customers, Reports, Users, Settings, Camera)
tests/GeneralStore.Tests/  Automated tests (xUnit)
```

Screens code se bani hain (designer files nahi hain), is liye Visual Studio mein yeh code view mein khulti hain.

---

### English summary

A desktop General Store Management System written in VB.NET (WinForms, .NET 8) with a SQLite database. It includes POS billing driven by a barcode scanner (any USB/Bluetooth scanner in keyboard mode), products and categories, barcode generation and label printing (Code 128, with EAN-13 numbers for in-store items), stock receiving, customer credit (udhaar) accounts with ledgers, sales, profit and stock reports with CSV export, receipt printing, Admin/Cashier users, and backup/restore. Open `GeneralStore.sln` in Visual Studio 2022 and press F5. The default login is `admin` / `admin123`.
