param(
    [string]$TargetPath,
    [string]$SiteName = "DrFlowHub",
    [string]$AppPoolName = "DrFlowHub",
    [switch]$SkipIisRestart
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

function Invoke-AppCmd($Arguments, $WarningMessage) {
    $appCmd = Join-Path $env:SystemRoot "System32\inetsrv\appcmd.exe"
    if (-not (Test-Path -LiteralPath $appCmd)) {
        Write-Host "appcmd.exe nao encontrado. Pulando controle do IIS."
        return $false
    }

    & $appCmd @Arguments | Write-Host
    if ($LASTEXITCODE -ne 0) {
        Write-Host $WarningMessage
        return $false
    }

    return $true
}

function Stop-IisTarget() {
    if ($SkipIisRestart) {
        Write-Host "Controle do IIS desativado por parametro."
        return
    }

    if (-not [string]::IsNullOrWhiteSpace($SiteName)) {
        Write-Host "Parando site IIS: $SiteName"
        Invoke-AppCmd -Arguments @("stop", "site", "/site.name:$SiteName") -WarningMessage "Nao foi possivel parar o site. Execute como Administrador ou confira o nome do site." | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($AppPoolName)) {
        Write-Host "Parando App Pool IIS: $AppPoolName"
        Invoke-AppCmd -Arguments @("stop", "apppool", "/apppool.name:$AppPoolName") -WarningMessage "Nao foi possivel parar o App Pool. Execute como Administrador ou confira o nome do App Pool." | Out-Null
    }
}

function Start-IisTarget() {
    if ($SkipIisRestart) {
        return
    }

    if (-not [string]::IsNullOrWhiteSpace($AppPoolName)) {
        Write-Host "Iniciando App Pool IIS: $AppPoolName"
        Invoke-AppCmd -Arguments @("start", "apppool", "/apppool.name:$AppPoolName") -WarningMessage "Nao foi possivel iniciar o App Pool. Inicie manualmente pelo IIS." | Out-Null
    }

    if (-not [string]::IsNullOrWhiteSpace($SiteName)) {
        Write-Host "Iniciando site IIS: $SiteName"
        Invoke-AppCmd -Arguments @("start", "site", "/site.name:$SiteName") -WarningMessage "Nao foi possivel iniciar o site. Inicie manualmente pelo IIS." | Out-Null
    }
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

Write-Host "Banco de dados PostgreSQL externo: nenhum arquivo SQLite sera usado."

Stop-IisTarget

try {
    $preserveAppSettings = (Test-Path -LiteralPath (Join-Path $target "appsettings.json")) -and (Read-YesNo "Preservar appsettings.json existente?" $true)
    $excludedNames = @()
    if ($preserveAppSettings) {
        $excludedNames += "appsettings.json"
    }

    Get-ChildItem -LiteralPath $source -Force | ForEach-Object {
        if ($excludedNames -contains $_.Name.ToLowerInvariant()) {
            Write-Host "Preservando: $($_.Name)"
        }
        else {
            $destination = Join-Path $target $_.Name
            if ($_.PSIsContainer -and $_.Name -ieq "wwwroot" -and (Test-Path -LiteralPath $destination)) {
                Write-Host "Limpando wwwroot antigo..."
                Remove-Item -LiteralPath $destination -Recurse -Force
            }

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
}
finally {
    Start-IisTarget
}

Write-Host "Deploy concluido."
