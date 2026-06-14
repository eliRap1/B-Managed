# Bootstraps WcfServiceLibrary1/ViewDB/Database/BManaged.accdb with all tables + seed rows.

$ErrorActionPreference = 'Stop'

$dbPath  = "D:\yudb\WcfServiceLibrary1\ViewDB\Database\BManaged.accdb"
# TODO(audit): Remove the plaintext password comment below. The hash is a demo-only
# value; never commit real-account hashes or plaintext passwords to source control.
$adminHash = "Wh07bZCwhjvwj4IsSR2nOWYpk6fWPUt6PZFFTLC6S8jg3qMC"  # demo seed only — see README
$today   = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")

if (Test-Path $dbPath) { Remove-Item $dbPath -Force }

# Create empty .accdb via ADOX (uses ACE 12 provider)
$cat = New-Object -ComObject ADOX.Catalog
$null = $cat.Create("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$dbPath;")
[Runtime.InteropServices.Marshal]::ReleaseComObject($cat) | Out-Null
[GC]::Collect()

Add-Type -AssemblyName 'System.Data'
$conn = New-Object System.Data.OleDb.OleDbConnection("Provider=Microsoft.ACE.OLEDB.12.0;Data Source=$dbPath;Persist Security Info=True")
$conn.Open()

function Exec($sql) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    [void]$cmd.ExecuteNonQuery()
}

# ===== DDL =====
Exec @"
CREATE TABLE [Users] (
  [id]            COUNTER PRIMARY KEY,
  [username]      TEXT(50) NOT NULL,
  [passwordHash]  TEXT(255) NOT NULL,
  [email]         TEXT(100),
  [phone]         TEXT(20),
  [role]          TEXT(20) NOT NULL,
  [isActive]      BIT NOT NULL,
  [createdAt]     DATETIME,
  [preferredCurrency] TEXT(3)
)
"@

Exec @"
CREATE TABLE [Customers] (
  [id]              COUNTER PRIMARY KEY,
  [businessName]    TEXT(100) NOT NULL,
  [contactName]     TEXT(80),
  [email]           TEXT(100),
  [phone]           TEXT(20),
  [taxId]           TEXT(20),
  [address]         TEXT(200),
  [ownerId]         LONG,
  [preferredCurrency] TEXT(3),
  [notes]           MEMO
)
"@

Exec @"
CREATE TABLE [Projects] (
  [id]                  COUNTER PRIMARY KEY,
  [customerId]          LONG NOT NULL,
  [title]               TEXT(120) NOT NULL,
  [description]         MEMO,
  [status]              TEXT(20),
  [startDate]           DATETIME,
  [dueDate]             DATETIME,
  [assignedEmployeeId]  LONG,
  [totalBudget]         CURRENCY,
  [currency]            TEXT(3)
)
"@

Exec @"
CREATE TABLE [Invoices] (
  [id]            COUNTER PRIMARY KEY,
  [invoiceNumber] TEXT(20) NOT NULL,
  [projectId]     LONG,
  [customerId]    LONG NOT NULL,
  [issueDate]     DATETIME,
  [dueDate]       DATETIME,
  [subtotal]      CURRENCY,
  [vatRate]       DOUBLE,
  [vatAmount]     CURRENCY,
  [total]         CURRENCY,
  [currency]      TEXT(3),
  [status]        TEXT(20),
  [paidDate]      DATETIME,
  [notes]         MEMO
)
"@

Exec @"
CREATE TABLE [InvoiceLines] (
  [id]          COUNTER PRIMARY KEY,
  [invoiceId]   LONG NOT NULL,
  [description] TEXT(255),
  [quantity]    DOUBLE,
  [unitPrice]   CURRENCY,
  [lineTotal]   CURRENCY,
  [currency]    TEXT(3)
)
"@

Exec @"
CREATE TABLE [ExpenseCategories] (
  [id]                COUNTER PRIMARY KEY,
  [name]              TEXT(50) NOT NULL,
  [isVatDeductible]   BIT
)
"@

Exec @"
CREATE TABLE [Expenses] (
  [id]            COUNTER PRIMARY KEY,
  [ownerId]       LONG NOT NULL,
  [categoryId]    LONG,
  [date]          DATETIME,
  [amount]        CURRENCY,
  [vatPaid]       CURRENCY,
  [vendor]        TEXT(100),
  [description]   MEMO,
  [projectId]     LONG,
  [receiptPath]   TEXT(255),
  [currency]      TEXT(3)
)
"@

Exec @"
CREATE TABLE [TaxPeriods] (
  [id]              COUNTER PRIMARY KEY,
  [year]            LONG,
  [month]           LONG,
  [vatCollected]    CURRENCY,
  [vatPaid]         CURRENCY,
  [vatDue]          CURRENCY,
  [incomeTotal]     CURRENCY,
  [expensesTotal]   CURRENCY,
  [profitEstimate]  CURRENCY,
  [taxSetAside]     CURRENCY,
  [currency]        TEXT(3)
)
"@

Exec @"
CREATE TABLE [Notifications] (
  [id]                COUNTER PRIMARY KEY,
  [userId]            LONG NOT NULL,
  [title]             TEXT(150),
  [message]           MEMO,
  [notificationType]  TEXT(30),
  [isRead]            BIT,
  [createdAt]         DATETIME,
  [readAt]            DATETIME
)
"@

Exec @"
CREATE TABLE [ExchangeRates] (
  [id]              COUNTER PRIMARY KEY,
  [fromCurrency]    TEXT(3) NOT NULL,
  [toCurrency]      TEXT(3) NOT NULL,
  [rate]            DOUBLE NOT NULL,
  [effectiveDate]   DATETIME NOT NULL
)
"@

# ===== seed rows =====
function MakeParam($name, $value) {
    $p = New-Object System.Data.OleDb.OleDbParameter
    $p.ParameterName = $name
    if ($null -eq $value) {
        $p.Value = [DBNull]::Value
    } elseif ($value -is [bool]) {
        $p.OleDbType = [System.Data.OleDb.OleDbType]::Boolean
        $p.Value = $value
    } elseif ($value -is [datetime]) {
        $p.OleDbType = [System.Data.OleDb.OleDbType]::Date
        $p.Value = $value
    } elseif ($value -is [int] -or $value -is [long]) {
        $p.OleDbType = [System.Data.OleDb.OleDbType]::Integer
        $p.Value = $value
    } elseif ($value -is [double] -or $value -is [decimal]) {
        $p.OleDbType = [System.Data.OleDb.OleDbType]::Double
        $p.Value = [double]$value
    } else {
        $p.OleDbType = [System.Data.OleDb.OleDbType]::VarWChar
        $p.Value = [string]$value
    }
    return $p
}

function ExecP($sql, $params) {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $sql
    foreach ($p in $params) {
        [void]$cmd.Parameters.Add((MakeParam $p[0] $p[1]))
    }
    [void]$cmd.ExecuteNonQuery()
}

# admin owner
ExecP "INSERT INTO [Users] ([username],[passwordHash],[email],[phone],[role],[isActive],[createdAt],[preferredCurrency]) VALUES (?,?,?,?,?,?,?,?)" @(
  @('@u','admin'),
  @('@p',$adminHash),
  @('@e','admin@b-managed.local'),
  @('@ph','0500000000'),
  @('@r','Owner'),
  @('@a',$true),
  @('@c',(Get-Date)),
  @('@cur','ILS')
)

# expense categories
$cats = @(
  @{ name='Equipment';  vat=$true },
  @{ name='Fuel';       vat=$true },
  @{ name='Rent';       vat=$true },
  @{ name='Utilities';  vat=$true },
  @{ name='Marketing';  vat=$true },
  @{ name='Travel';     vat=$true },
  @{ name='Meals';      vat=$false },
  @{ name='Other';      vat=$false }
)
foreach ($c in $cats) {
  ExecP "INSERT INTO [ExpenseCategories] ([name],[isVatDeductible]) VALUES (?,?)" @(
    @('@n', $c.name),
    @('@v', $c.vat)
  )
}

# exchange rates seed (manual, latest USD ≈ 3.7 ILS)
$today2 = Get-Date
ExecP "INSERT INTO [ExchangeRates] ([fromCurrency],[toCurrency],[rate],[effectiveDate]) VALUES (?,?,?,?)" @(
  @('@f','USD'), @('@t','ILS'), @('@r',3.7), @('@d',$today2)
)
ExecP "INSERT INTO [ExchangeRates] ([fromCurrency],[toCurrency],[rate],[effectiveDate]) VALUES (?,?,?,?)" @(
  @('@f','ILS'), @('@t','USD'), @('@r',0.27), @('@d',$today2)
)

function LastId() {
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT @@IDENTITY"
    return [int]$cmd.ExecuteScalar()
}

# ===== Demo data =====
# An employee + a couple clients
$empHash = "Wh07bZCwhjvwj4IsSR2nOWYpk6fWPUt6PZFFTLC6S8jg3qMC"   # password = admin1234 (re-used for demo)
ExecP "INSERT INTO [Users] ([username],[passwordHash],[email],[phone],[role],[isActive],[createdAt],[preferredCurrency]) VALUES (?,?,?,?,?,?,?,?)" @(
  @('@u','dana'), @('@p',$empHash), @('@e','dana@b-managed.local'), @('@ph','0501111111'),
  @('@r','Employee'), @('@a',$true), @('@c',(Get-Date)), @('@cur','ILS'))
$danaId = LastId

ExecP "INSERT INTO [Users] ([username],[passwordHash],[email],[phone],[role],[isActive],[createdAt],[preferredCurrency]) VALUES (?,?,?,?,?,?,?,?)" @(
  @('@u','acme'), @('@p',$empHash), @('@e','contact@acme.co'), @('@ph','0502222222'),
  @('@r','Client'), @('@a',$true), @('@c',(Get-Date)), @('@cur','ILS'))
$acmeUserId = LastId

# Customers (owned by admin, who has id=1 from earlier insert)
$ownerId = 1
ExecP "INSERT INTO [Customers] ([businessName],[contactName],[email],[phone],[taxId],[address],[ownerId],[preferredCurrency],[notes]) VALUES (?,?,?,?,?,?,?,?,?)" @(
  @('@bn','Acme Studios'), @('@cn','Iris Cohen'), @('@e','contact@acme.co'),
  @('@p','0502222222'), @('@tid','512345678'), @('@a','Tel Aviv'),
  @('@o',$ownerId), @('@cur','ILS'), @('@n','Long-term partner since 2024'))
$acmeCustId = LastId

ExecP "INSERT INTO [Customers] ([businessName],[contactName],[email],[phone],[taxId],[address],[ownerId],[preferredCurrency],[notes]) VALUES (?,?,?,?,?,?,?,?,?)" @(
  @('@bn','Globex Ltd'), @('@cn','Yoni Levi'), @('@e','y.levi@globex.io'),
  @('@p','0503333333'), @('@tid','523456789'), @('@a','Haifa'),
  @('@o',$ownerId), @('@cur','USD'), @('@n','New US lead'))
$globexCustId = LastId

# Project assigned to dana
ExecP "INSERT INTO [Projects] ([customerId],[title],[description],[status],[startDate],[dueDate],[assignedEmployeeId],[totalBudget],[currency]) VALUES (?,?,?,?,?,?,?,?,?)" @(
  @('@c',$acmeCustId), @('@t','Website redesign'), @('@d','New marketing site + CMS'),
  @('@s','Active'), @('@sd',(Get-Date).AddDays(-14)), @('@dd',(Get-Date).AddDays(30)),
  @('@ae',$danaId), @('@b',12000), @('@cur','ILS'))
$proj1Id = LastId

ExecP "INSERT INTO [Projects] ([customerId],[title],[description],[status],[startDate],[dueDate],[assignedEmployeeId],[totalBudget],[currency]) VALUES (?,?,?,?,?,?,?,?,?)" @(
  @('@c',$globexCustId), @('@t','Brand book'), @('@d','Logo + style guide PDF'),
  @('@s','AwaitingPayment'), @('@sd',(Get-Date).AddDays(-30)), @('@dd',(Get-Date).AddDays(-2)),
  @('@ae',$null), @('@b',2500), @('@cur','USD'))
$proj2Id = LastId

# Invoice for project 2 (Globex)
ExecP "INSERT INTO [Invoices] ([invoiceNumber],[projectId],[customerId],[issueDate],[dueDate],[subtotal],[vatRate],[vatAmount],[total],[currency],[status],[notes]) VALUES (?,?,?,?,?,?,?,?,?,?,?,?)" @(
  @('@n','INV-2026-00001'), @('@p',$proj2Id), @('@c',$globexCustId),
  @('@id',(Get-Date).AddDays(-2)), @('@dd',(Get-Date).AddDays(28)),
  @('@s',2500), @('@vr',0.0), @('@va',0), @('@t',2500),
  @('@cur','USD'), @('@st','Sent'), @('@no','Brand book delivery'))
$inv1Id = LastId

ExecP "INSERT INTO [InvoiceLines] ([invoiceId],[description],[quantity],[unitPrice],[lineTotal],[currency]) VALUES (?,?,?,?,?,?)" @(
  @('@i',$inv1Id), @('@d','Logo + 3 alt marks'), @('@q',1.0), @('@up',1500), @('@lt',1500), @('@cur','USD'))
ExecP "INSERT INTO [InvoiceLines] ([invoiceId],[description],[quantity],[unitPrice],[lineTotal],[currency]) VALUES (?,?,?,?,?,?)" @(
  @('@i',$inv1Id), @('@d','Style guide PDF (24 pages)'), @('@q',1.0), @('@up',1000), @('@lt',1000), @('@cur','USD'))

# Two expenses
ExecP "INSERT INTO [Expenses] ([ownerId],[categoryId],[date],[amount],[vatPaid],[vendor],[description],[currency]) VALUES (?,?,?,?,?,?,?,?)" @(
  @('@o',$ownerId), @('@c',1), @('@d',(Get-Date).AddDays(-10)),
  @('@a',420), @('@v',71.4), @('@vd','Office Depot'), @('@desc','Monitor stand + cables'), @('@cur','ILS'))

ExecP "INSERT INTO [Expenses] ([ownerId],[categoryId],[date],[amount],[vatPaid],[vendor],[description],[currency]) VALUES (?,?,?,?,?,?,?,?)" @(
  @('@o',$ownerId), @('@c',2), @('@d',(Get-Date).AddDays(-3)),
  @('@a',180), @('@v',30.6), @('@vd','Paz fuel'), @('@desc','Client visit Haifa'), @('@cur','ILS'))

# Welcome notification
# TODO(audit): Notification message previously embedded the plaintext password
# "admin1234". Removed credential from the persisted DB record.
ExecP "INSERT INTO [Notifications] ([userId],[title],[message],[notificationType],[isRead],[createdAt]) VALUES (?,?,?,?,?,?)" @(
  @('@u',$ownerId), @('@t','Welcome to B-Managed'),
  @('@m','Demo data seeded. Acme + Globex are pre-loaded customers. See README for demo login instructions.'),
  @('@nt','Info'), @('@r',$false), @('@c',(Get-Date)))

$conn.Close()
Write-Host "OK BManaged.accdb created at $dbPath"
Write-Host "  Owner login : admin / admin1234"
Write-Host "  Employee    : dana / admin1234"
Write-Host "  Client      : acme / admin1234"
