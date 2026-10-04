param([switch]$SelfContained)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'PortProxyGUI.csproj'
$repository = Split-Path $PSScriptRoot -Parent
$profiles = if ($SelfContained) {
    @('win-x64-singlefile', 'win-x64-selfcontained-singlefile')
} else { @('win-x64-singlefile') }

foreach ($profile in $profiles) {
    $directory = if ($profile -eq 'win-x64-singlefile') {
        'PortProxyGUI-win-x64-single'
    } else { 'PortProxyGUI-win-x64-selfcontained-single' }
    $output = Join-Path $repository "artifacts\$directory"
    dotnet publish $project "-p:PublishProfile=$profile" --output $output
    if ($LASTEXITCODE -ne 0) { throw "Publish failed: $profile" }
}
