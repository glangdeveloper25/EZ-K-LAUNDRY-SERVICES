<#
  XAMPP MySQL helper for the VS Code terminal.

    .\db.ps1 start    Start MySQL in the background
    .\db.ps1 stop     Stop MySQL safely (always do this before closing the laptop)
    .\db.ps1 status   Show whether MySQL is running
    .\db.ps1 backup   Save a copy of the laundry_final database
    .\db.ps1 open     Open the MySQL console for laundry_final

  Never close MySQL with End Task, the XAMPP "Stop" button, or by shutting down the
  laptop while it runs: an unclean shutdown is what corrupts the database files.
#>
param([ValidateSet('start', 'stop', 'status', 'backup', 'open')] [string]$Command = 'status')

$ErrorActionPreference = 'Stop'
$bin = 'C:\xampp\mysql\bin'
$database = 'laundry_final'
$backupFolder = 'C:\Users\ADMIN\Documents\EZK_LAUNDRY_SYSTEM\DB_Backups'

function Test-Running {
    # Windows PowerShell 5 treats a native program's error output as a failure, so relax it here.
    $ErrorActionPreference = 'Continue'
    & "$bin\mysqladmin.exe" -u root ping 2>&1 | Out-Null
    return $LASTEXITCODE -eq 0
}

switch ($Command) {
    'start' {
        if (Test-Running) { Write-Host 'MySQL is already running.' -ForegroundColor Green; return }
        Start-Process "$bin\mysqld.exe" -ArgumentList "--defaults-file=$bin\my.ini", '--standalone' -WindowStyle Hidden
        Write-Host 'Starting MySQL' -NoNewline
        foreach ($second in 1..30) {
            Start-Sleep -Seconds 1
            if (Test-Running) { Write-Host ''; Write-Host 'MySQL is running.' -ForegroundColor Green; return }
            Write-Host '.' -NoNewline
        }
        Write-Host ''
        Write-Host 'MySQL did not start. Check C:\xampp\mysql\data\mysql_error.log' -ForegroundColor Red
    }
    'stop' {
        if (-not (Test-Running)) { Write-Host 'MySQL is not running.' -ForegroundColor Yellow; return }
        # A clean shutdown lets MySQL finish writing its files, which prevents corruption.
        & "$bin\mysqladmin.exe" -u root shutdown
        foreach ($second in 1..30) {
            if (-not (Get-Process mysqld -ErrorAction SilentlyContinue)) {
                Write-Host 'MySQL stopped safely. You can now close XAMPP or the laptop.' -ForegroundColor Green
                return
            }
            Start-Sleep -Seconds 1
        }
        Write-Host 'MySQL is still shutting down. Wait a moment and run ".\db.ps1 status".' -ForegroundColor Yellow
    }
    'status' {
        if (Test-Running) { Write-Host 'MySQL is RUNNING.' -ForegroundColor Green }
        else { Write-Host 'MySQL is STOPPED. Run ".\db.ps1 start".' -ForegroundColor Yellow }
    }
    'backup' {
        if (-not (Test-Running)) { Write-Host 'Start MySQL first: .\db.ps1 start' -ForegroundColor Yellow; return }
        New-Item -ItemType Directory -Force $backupFolder | Out-Null
        $file = Join-Path $backupFolder ("{0}_{1:yyyy-MM-dd_HHmm}.sql" -f $database, (Get-Date))
        & "$bin\mysqldump.exe" -u root --routines --triggers --single-transaction "--result-file=$file" $database
        if ($LASTEXITCODE -eq 0) { Write-Host "Backup saved: $file" -ForegroundColor Green }
        else { Write-Host 'Backup failed.' -ForegroundColor Red }
    }
    'open' {
        if (-not (Test-Running)) { Write-Host 'Start MySQL first: .\db.ps1 start' -ForegroundColor Yellow; return }
        Write-Host 'Type "exit" to leave the MySQL console.' -ForegroundColor Cyan
        & "$bin\mysql.exe" -u root $database
    }
}
