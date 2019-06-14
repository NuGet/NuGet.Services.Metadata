[CmdletBinding(DefaultParameterSetName='RegularBuild')]
param (
    [string]$Slot
)

Write-Host "Setting environment variable for the slot: " $Slot
$env:Slot = $Slot
Write-Host "##vso[task.setvariable variable=Slot;]$Slot"