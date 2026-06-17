#requires -version 5.1
<#
Configura o RustDesk Client Windows para usar o servidor self-hosted do UniFlowHub.

Execute em PowerShell como Administrador:
  powershell.exe -ExecutionPolicy Bypass -File .\configurar-rustdesk-client.ps1

Para tambem registrar o equipamento no usuario do UniFlowHub:
  powershell.exe -ExecutionPolicy Bypass -File .\configurar-rustdesk-client.ps1 -UniFlowHubApiUrl "http://SEU-SERVIDOR:5000" -Email "usuario@empresa.com" -Senha (Read-Host "Senha UniFlowHub" -AsSecureString)
#>

[CmdletBinding()]
param(
  [string]$Server = '192.168.1.224',
  [string]$RelayServer = '192.168.1.224',
  [string]$Key = '0EEoPPZgEaSLlDamIVqua2oAgP0DTHsiPczrZz4WfiY=',
  [string]$RustDeskPath = '',
  [string]$PermanentPassword = '',
  [string]$ConfigString = '',
  [string]$UniFlowHubApiUrl = '',
  [string]$Email = '',
  [securestring]$Senha,
  [switch]$SkipRegister
)

$ErrorActionPreference = 'Stop'

function Test-Admin {
  $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
  $principal = New-Object Security.Principal.WindowsPrincipal($identity)
  return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Find-RustDeskExe {
  param([string]$ExplicitPath)

  $candidates = @()
  if ($ExplicitPath) {
    $candidates += $ExplicitPath
  }

  $command = Get-Command rustdesk.exe -ErrorAction SilentlyContinue
  if ($command) {
    $candidates += $command.Source
  }

  $candidates += @(
    "$env:ProgramFiles\RustDesk\rustdesk.exe",
    "${env:ProgramFiles(x86)}\RustDesk\rustdesk.exe",
    "$env:LOCALAPPDATA\Programs\RustDesk\rustdesk.exe"
  )

  $match = $candidates | Where-Object { $_ -and (Test-Path -LiteralPath $_) } | Select-Object -First 1
  if (!$match) {
    throw 'RustDesk nao encontrado. Instale o client ou informe -RustDeskPath "C:\Caminho\rustdesk.exe".'
  }

  return (Resolve-Path -LiteralPath $match).Path
}

function Backup-File {
  param([string]$Path)

  if (Test-Path -LiteralPath $Path) {
    $backup = "$Path.bak-$(Get-Date -Format 'yyyyMMddHHmmss')"
    Copy-Item -LiteralPath $Path -Destination $backup -Force
    Write-Host "Backup criado: $backup"
  }
}

function Write-RustDeskConfig {
  param(
    [string]$Path,
    [string]$IdServer,
    [string]$Relay,
    [string]$PublicKey
  )

  $dir = Split-Path -Parent $Path
  New-Item -ItemType Directory -Path $dir -Force | Out-Null
  Backup-File -Path $Path

  $content = @"
rendezvous_server = '$IdServer'
nat_type = 1
serial = 0

[options]
custom-rendezvous-server = '$IdServer'
relay-server = '$Relay'
key = '$PublicKey'
"@

  Set-Content -LiteralPath $Path -Value $content -Encoding UTF8 -Force
  Write-Host "Config gravada: $Path"
}

function ConvertTo-PlainText {
  param([securestring]$Secure)

  if (!$Secure) {
    return ''
  }

  $ptr = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
  try {
    return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
  } finally {
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
  }
}

function Get-RustDeskId {
  param([string]$Exe)

  Start-Sleep -Seconds 2
  $output = & $Exe --get-id 2>$null
  return (($output | Out-String).Trim() -split '\s+' | Where-Object { $_ })[-1]
}

function Register-UniFlowHubRustDesk {
  param(
    [string]$ApiUrl,
    [string]$LoginEmail,
    [securestring]$LoginPassword,
    [string]$RustDeskId,
    [string]$RustDeskPassword
  )

  if (!$ApiUrl -or !$LoginEmail -or !$LoginPassword) {
    Write-Host 'Registro no UniFlowHub ignorado. Informe -UniFlowHubApiUrl, -Email e -Senha para registrar automaticamente.'
    return
  }

  $plainPassword = ConvertTo-PlainText -Secure $LoginPassword
  $api = $ApiUrl.TrimEnd('/')
  $login = Invoke-RestMethod -Method Post -Uri "$api/api/auth/login" -ContentType 'application/json' -Body (@{
    email = $LoginEmail
    senha = $plainPassword
  } | ConvertTo-Json)

  $headers = @{ Authorization = "Bearer $($login.token)" }
  $payload = @{
    rustDeskId = $RustDeskId
    rustDeskSenha = $RustDeskPassword
    hostname = $env:COMPUTERNAME
    sistemaOperacional = (Get-CimInstance Win32_OperatingSystem).Caption
  }

  Invoke-RestMethod -Method Post -Uri "$api/api/users/me/rustdesk" -Headers $headers -ContentType 'application/json' -Body ($payload | ConvertTo-Json) | Out-Null
  Write-Host "RustDesk registrado no UniFlowHub para $LoginEmail."
}

if (!(Test-Admin)) {
  throw 'Execute este script em um PowerShell aberto como Administrador.'
}

$rustDeskExe = Find-RustDeskExe -ExplicitPath $RustDeskPath
Write-Host "RustDesk encontrado: $rustDeskExe"

Get-Service -Name RustDesk -ErrorAction SilentlyContinue | Stop-Service -Force -ErrorAction SilentlyContinue
Get-Process -Name RustDesk -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

if ($ConfigString) {
  & $rustDeskExe --config $ConfigString
  Write-Host 'Config aplicada via --config.'
} else {
  $configTargets = @(
    Join-Path $env:APPDATA 'RustDesk\config\RustDesk2.toml',
    'C:\Windows\ServiceProfiles\LocalService\AppData\Roaming\RustDesk\config\RustDesk2.toml'
  )

  foreach ($target in $configTargets) {
    Write-RustDeskConfig -Path $target -IdServer $Server -Relay $RelayServer -PublicKey $Key
  }
}

if ($PermanentPassword) {
  & $rustDeskExe --password $PermanentPassword | Out-Null
  Write-Host 'Senha permanente RustDesk configurada.'
}

$service = Get-Service -Name RustDesk -ErrorAction SilentlyContinue
if ($service) {
  Start-Service -Name RustDesk
} else {
  Start-Process -FilePath $rustDeskExe -WindowStyle Hidden
}

$rustDeskId = Get-RustDeskId -Exe $rustDeskExe
if (!$rustDeskId) {
  throw 'Nao foi possivel ler o ID do RustDesk. Abra o RustDesk uma vez e rode o script novamente.'
}

Write-Host "ID RustDesk deste equipamento: $rustDeskId"

if (!$SkipRegister) {
  Register-UniFlowHubRustDesk `
    -ApiUrl $UniFlowHubApiUrl `
    -LoginEmail $Email `
    -LoginPassword $Senha `
    -RustDeskId $rustDeskId `
    -RustDeskPassword $PermanentPassword
}

Write-Host 'Concluido.'
