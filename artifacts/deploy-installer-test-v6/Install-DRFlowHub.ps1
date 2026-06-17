param(
    [string]$TargetPath
)

$ErrorActionPreference = "Stop"

function Read-YesNo($Prompt, $DefaultYes = $true) {
    $suffix = if ($DefaultYes) { "[S/n]" } else { "[s/N]" }
    $answer = Read-Host "$Prompt $suffix"
    if ([string]::IsNullOrWhiteSpace($answer)) {
        return $DefaultYes
    }

    return $answer.Trim().ToLowerInvariant().StartsWith("s") -or $answer.Trim().ToLowerInvariant().StartsWith("y")
}

$sourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($TargetPath)) {
    $TargetPath = Read-Host "Informe a pasta de destino do DRFlowHub"
}

if ([string]::IsNullOrWhiteSpace($TargetPath)) {
    throw "Pasta de destino nao informada."
}

$target = [System.IO.Path]::GetFullPath($TargetPath)
$source = [System.IO.Path]::GetFullPath($sourcePath)

if ($target.TrimEnd('\') -eq $source.TrimEnd('\')) {
    throw "A pasta de destino nao pode ser a mesma pasta do pacote de deploy."
}

Write-Host "Pacote: $source"
Write-Host "Destino: $target"

if (-not (Test-Path -LiteralPath $target)) {
    New-Item -ItemType Directory -Path $target | Out-Null
}

$dbFiles = @("drflowhub.db", "drflowhub.db-wal", "drflowhub.db-shm")
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backupDir = Join-Path $target "backup-$timestamp"
$hasDatabase = $false

foreach ($dbFile in $dbFiles) {
    $path = Join-Path $target $dbFile
    if (Test-Path -LiteralPath $path) {
        if (-not (Test-Path -LiteralPath $backupDir)) {
            New-Item -ItemType Directory -Path $backupDir | Out-Null
        }

        Copy-Item -LiteralPath $path -Destination (Join-Path $backupDir $dbFile) -Force
        $hasDatabase = $true
    }
}

if ($hasDatabase) {
    Write-Host "Backup do banco criado em: $backupDir"
}

$preserveAppSettings = (Test-Path -LiteralPath (Join-Path $target "appsettings.json")) -and (Read-YesNo "Preservar appsettings.json existente?" $true)
$excludedNames = @("drflowhub.db", "drflowhub.db-wal", "drflowhub.db-shm")
if ($preserveAppSettings) {
    $excludedNames += "appsettings.json"
}

Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
    if ($excludedNames -contains $_.Name.ToLowerInvariant()) {
        Write-Host "Preservando: $($_.Name)"
    }
    else {
        $destination = Join-Path $target $_.Name
        Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force
    }
}

$migrationDir = Join-Path $source "migrations"
$hasMigrations = (Test-Path -LiteralPath $migrationDir) -and ((Get-ChildItem -LiteralPath $migrationDir -Filter "*.sql" -File -ErrorAction SilentlyContinue | Measure-Object).Count -gt 0)
$exePath = Join-Path $target "DRFlowHub.exe"

if ($hasMigrations -and (Test-Path -LiteralPath $exePath)) {
    Write-Host "Aplicando migrations no banco existente..."
    Push-Location $target
    try {
        & $exePath --migrate
        if ($LASTEXITCODE -ne 0) {
            throw "Falha ao executar migrations. Codigo: $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }

    Write-Host "Migrations aplicadas com sucesso."
}
elseif ($hasMigrations) {
    Write-Host "Migrations encontradas, mas DRFlowHub.exe nao foi localizado no destino."
}
else {
    Write-Host "Nenhuma migration encontrada no pacote."
}

Write-Host "Deploy concluido."
