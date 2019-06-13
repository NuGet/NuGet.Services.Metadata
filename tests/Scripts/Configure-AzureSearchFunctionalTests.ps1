[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [string]$ServiceRoot,
    [string]$Slot,
    [string]$CloudServiceName,
    [string]$SubscriptionId,
    [string]$ApplicationId,
    [string]$TenantId,
    [string]$AzureCertificateThumbprint
)

Write-Host "Setting environment variable for the slot: " $Slot
$env:Slot = $Slot
Write-Host "##vso[task.setvariable variable=Slot;]$Slot"