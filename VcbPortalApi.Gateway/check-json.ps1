# Tim dong gay loi cu phap trong cac file appsettings.
#
#   .\check-json.ps1
#   .\check-json.ps1 -Path D:\develop\vcbportalapi\VcbPortalApi
#
# CHU Y: PowerShell kho tinh hon .NET. .NET chap nhan comment // va dau phay
# thua cuoi mang, PowerShell thi bao SAI. Neu chi bao loi vi hai thu do thi
# la bao dong gia — app van chay duoc.

param([string]$Path = '.')

$files = Get-ChildItem (Join-Path $Path 'appsettings*.json') -ErrorAction SilentlyContinue
if (-not $files) { Write-Host "Khong tim thay file appsettings*.json trong $Path" -ForegroundColor Red; exit 1 }

# ── 1. Dau xung dot rebase — khong bao gio la bao dong gia ────────────────────
$marks = Select-String -Path $files.FullName -Pattern '^<<<<<<<|^=======|^>>>>>>>'
if ($marks) {
    Write-Host ''
    Write-Host 'CON DAU XUNG DOT REBASE:' -ForegroundColor Red
    $marks | ForEach-Object { Write-Host ("  {0}:{1}  {2}" -f $_.Filename, $_.LineNumber, $_.Line) -ForegroundColor Red }
    Write-Host '  -> Xoa cac dong nay, giu lai noi dung dung.' -ForegroundColor Red
}

# ── 2. Cu phap JSON ───────────────────────────────────────────────────────────
Write-Host ''
foreach ($f in $files) {
    $raw = Get-Content $f.FullName -Raw
    try {
        $null = $raw | ConvertFrom-Json
        Write-Host ("  OK   {0}" -f $f.Name) -ForegroundColor Green
    }
    catch {
        $msg = $_.Exception.Message
        Write-Host ("  SAI  {0}" -f $f.Name) -ForegroundColor Red

        # Thong bao co dang "... expected. (41): {noi dung file}" — so trong
        # ngoac la VI TRI KY TU, doi sang so dong cho de tim.
        if ($msg -match '\((\d+)\)') {
            $pos  = [int]$Matches[1]
            $head = $raw.Substring(0, [Math]::Min($pos, $raw.Length))
            $line = ($head -split "`n").Count
            Write-Host ("       Dong $line (ky tu thu $pos)") -ForegroundColor Yellow

            # In 3 dong quanh cho sai
            $all = $raw -split "`r?`n"
            for ($i = [Math]::Max(0, $line - 3); $i -lt [Math]::Min($all.Count, $line + 2); $i++) {
                $mark = if (($i + 1) -eq $line) { '>>' } else { '  ' }
                $color = if (($i + 1) -eq $line) { 'Yellow' } else { 'DarkGray' }
                Write-Host ("       {0} {1,4}: {2}" -f $mark, ($i + 1), $all[$i]) -ForegroundColor $color
            }
        }
        else {
            Write-Host ("       $msg") -ForegroundColor Yellow
        }
    }
}
Write-Host ''
