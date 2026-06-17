param(
    [Parameter(Mandatory = $true)]
    [string]$ApiUrl,

    [Parameter(Mandatory = $true)]
    [string]$Token,

    [string]$RustDeskPath = "$env:ProgramFiles\RustDesk\rustdesk.exe"
)

if (-not (Test-Path -LiteralPath $RustDeskPath)) {
    throw "RustDesk nao encontrado em $RustDeskPath"
}

$rustDeskId = (& $RustDeskPath --get-id | Out-String).Trim()
if ([string]::IsNullOrWhiteSpace($rustDeskId)) {
    throw "Nao foi possivel obter o ID do RustDesk."
}

$alphabet = 'ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789'
$passwordChars = 1..14 | ForEach-Object { $alphabet[(Get-Random -Minimum 0 -Maximum $alphabet.Length)] }
$rustDeskPassword = -join $passwordChars

& $RustDeskPath --password $rustDeskPassword | Out-Null

$body = @{
    rustDeskId = $rustDeskId
    rustDeskSenha = $rustDeskPassword
    hostname = $env:COMPUTERNAME
    sistemaOperacional = (Get-CimInstance Win32_OperatingSystem).Caption
} | ConvertTo-Json

$headers = @{
    Authorization = "Bearer $Token"
}

Invoke-RestMethod -Method Post -Uri "$ApiUrl/api/users/me/rustdesk" -Headers $headers -ContentType 'application/json' -Body $body
Write-Output "RustDesk registrado: $rustDeskId"
