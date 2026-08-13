# Run in an elevated PowerShell. Opens only CloudPad's configured TCP/UDP port.
[CmdletBinding()] param([ValidateRange(1,65535)][int]$Port=26760)
$ErrorActionPreference='Stop'
$name="CloudPad Receiver $Port"
Get-NetFirewallRule -DisplayName "$name TCP" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
Get-NetFirewallRule -DisplayName "$name UDP" -ErrorAction SilentlyContinue | Remove-NetFirewallRule
New-NetFirewallRule -DisplayName "$name TCP" -Direction Inbound -Action Allow -Protocol TCP -LocalPort $Port -Profile Private
New-NetFirewallRule -DisplayName "$name UDP" -Direction Inbound -Action Allow -Protocol UDP -LocalPort $Port -Profile Private
Write-Host "CloudPad TCP/UDP port $Port is allowed on Private networks."
