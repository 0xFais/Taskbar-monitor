param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'publish\portable-win-x64'))
$ErrorActionPreference = 'Stop'
$projectFile = Join-Path $PSScriptRoot 'src\TaskbarMonitor.App\TaskbarMonitor.App.csproj'
& dotnet publish $projectFile -c Release -r win-x64 --self-contained true '-p:PublishProfile=Portable' '-p:NuGetAudit=false' '-p:RestoreIgnoreFailedSources=true' -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Portable publish failed.' }

Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'PORTABLE-README.txt') -Destination (Join-Path $OutputDirectory 'README.txt') -Force
$licenseDirectory = Join-Path $OutputDirectory 'Licenses'
New-Item -ItemType Directory -Force -Path $licenseDirectory | Out-Null
$assets = Get-Content (Join-Path $PSScriptRoot 'src\TaskbarMonitor.App\obj\project.assets.json') -Raw | ConvertFrom-Json
$packageRoot = @($assets.packageFolders.PSObject.Properties)[0].Name
$dependencyNotices = [System.Collections.Generic.List[string]]::new()
$dependencyNotices.Add('Taskbar-monitor portable: bundled .NET runtime and NuGet dependency notices')
$dependencyNotices.Add('PawnIO is downloaded from its official publisher when needed and is not bundled in this ZIP.')
foreach ($library in $assets.libraries.PSObject.Properties) {
    if ($library.Value.type -ne 'package') { continue }
    $packageDirectory = Join-Path $packageRoot $library.Value.path
    $nuspec = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' -File | Select-Object -First 1
    if ($null -eq $nuspec) { continue }
    [xml]$metadata = Get-Content -LiteralPath $nuspec.FullName -Raw
    $license = $metadata.package.metadata.license
    $dependencyNotices.Add("$($library.Name) | license: $($license.InnerText) | $($metadata.package.metadata.projectUrl)")
    Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING|THIRD.PARTY|NOTICE)' } | ForEach-Object {
        $safePackageName = $library.Name.Replace('/', '-')
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $licenseDirectory "$safePackageName-$($_.Name)") -Force
    }
}
$dependencyNotices.Add('LibreHardwareMonitorLib source: https://github.com/LibreHardwareMonitor/LibreHardwareMonitor/tree/v0.9.6')
$dependencyNotices.Add('LibreHardwareMonitorLib license: Mozilla Public License 2.0 — https://www.mozilla.org/MPL/2.0/')
$runtimeConfig = Get-Content (Join-Path $PSScriptRoot 'src\TaskbarMonitor.App\bin\Release\net10.0-windows\win-x64\TaskbarMonitor.App.runtimeconfig.json') -Raw | ConvertFrom-Json
foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
    $runtimePackageName = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $runtimePackage = Join-Path $packageRoot "$runtimePackageName\$($framework.version)"
    $dependencyNotices.Add("$($framework.name) $($framework.version): MIT license; runtime notices are included alongside this file.")
    Get-ChildItem -LiteralPath $runtimePackage -File | Where-Object { $_.Name -match '^(LICENSE|THIRD.PARTY)' } | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $licenseDirectory "$runtimePackageName-$($_.Name)") -Force
    }
}
$lhmLicense = Join-Path $PSScriptRoot 'licenses\LibreHardwareMonitor-LICENSE.txt'
if (Test-Path -LiteralPath $lhmLicense) { Copy-Item -LiteralPath $lhmLicense -Destination $licenseDirectory -Force }
$dependencyNotices | Set-Content -LiteralPath (Join-Path $licenseDirectory 'DEPENDENCIES.txt') -Encoding utf8
$zipPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'Taskbar-monitor-portable-win-x64.zip'
Compress-Archive -LiteralPath (Join-Path $OutputDirectory 'TaskbarMonitor.App.exe'), (Join-Path $OutputDirectory 'portable.flag'), (Join-Path $OutputDirectory 'README.txt'), $licenseDirectory -DestinationPath $zipPath -Force
Write-Output "Portable ZIP ready: $zipPath"
