param(
    [string]$TargetPath,
    [string]$SiteName = "UniFlowHub",
    [string]$AppPoolName = "UniFlowHub",
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

function Sync-OracleProductionConnection($SourceConfigPath, $TargetConfigPath) {
    if (-not (Test-Path -LiteralPath $SourceConfigPath) -or -not (Test-Path -LiteralPath $TargetConfigPath)) {
        return
    }

    try {
        $sourceConfig = Get-Content -LiteralPath $SourceConfigPath -Raw | ConvertFrom-Json
        $targetConfig = Get-Content -LiteralPath $TargetConfigPath -Raw | ConvertFrom-Json
        $productionConnection = $sourceConfig.ConnectionStrings.OracleConnectionProduction

        if ([string]::IsNullOrWhiteSpace($productionConnection)) {
            return
        }

        if ($null -eq $targetConfig.ConnectionStrings) {
            $targetConfig | Add-Member -MemberType NoteProperty -Name "ConnectionStrings" -Value ([pscustomobject]@{})
        }

        $currentConnection = $targetConfig.ConnectionStrings.OracleConnectionProduction
        if ([string]::IsNullOrWhiteSpace($currentConnection)) {
            if ($targetConfig.ConnectionStrings.PSObject.Properties.Name -contains "OracleConnectionProduction") {
                $targetConfig.ConnectionStrings.OracleConnectionProduction = $productionConnection
            }
            else {
                $targetConfig.ConnectionStrings | Add-Member -MemberType NoteProperty -Name "OracleConnectionProduction" -Value $productionConnection
            }

            $targetConfig | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $TargetConfigPath -Encoding UTF8
            Write-Host "OracleConnectionProduction preenchida no appsettings.json preservado."
        }
    }
    catch {
        Write-Host "Nao foi possivel sincronizar OracleConnectionProduction no appsettings preservado: $($_.Exception.Message)"
    }
}

$sourcePath = Split-Path -Parent $MyInvocation.MyCommand.Path

if ([string]::IsNullOrWhiteSpace($TargetPath)) {
    $TargetPath = Read-Host "Informe a pasta de destino do UniFlowHub"
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

    if ($preserveAppSettings) {
        Sync-OracleProductionConnection `
            -SourceConfigPath (Join-Path $source "appsettings.json") `
            -TargetConfigPath (Join-Path $target "appsettings.json")
    }

    $exePath = Join-Path $target "UniFlowHub.exe"

    if (Test-Path -LiteralPath $exePath) {
        Write-Host "Aplicando migrations pendentes no banco PostgreSQL..."
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
    else {
        Write-Host "UniFlowHub.exe nao foi localizado no destino. Pulando migrations."
    }
}
finally {
    Start-IisTarget
}

Write-Host "Deploy concluido."
