param([string]$OutputDirectory = (Join-Path $PSScriptRoot 'publish\portable-framework-dependent-win-x64'))
$ErrorActionPreference = 'Stop'

$projectFile = Join-Path $PSScriptRoot 'src\TaskbarMonitor.App\TaskbarMonitor.App.csproj'
$readmeFile = Join-Path $PSScriptRoot 'PORTABLE-FRAMEWORK-DEPENDENT-README.txt'
$licenseFile = Join-Path $PSScriptRoot 'licenses\LibreHardwareMonitor-LICENSE.txt'
$artifactsDirectory = Join-Path $PSScriptRoot 'build-artifacts'

& dotnet publish $projectFile -c Release -r win-x64 --self-contained false --artifacts-path $artifactsDirectory `
    '-p:PublishSingleFile=true' `
    '-p:IncludeNativeLibrariesForSelfExtract=true' `
    '-p:PublishTrimmed=false' `
    '-p:PortableMode=true' `
    '-p:NuGetAudit=false' `
    '-p:RestoreIgnoreFailedSources=true' `
    -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw 'Framework-dependent publish failed.' }

Copy-Item -LiteralPath $readmeFile -Destination (Join-Path $OutputDirectory 'README.txt') -Force
$licenseDirectory = Join-Path $OutputDirectory 'Licenses'
New-Item -ItemType Directory -Force -Path $licenseDirectory | Out-Null
if (Test-Path -LiteralPath $licenseFile) {
    Copy-Item -LiteralPath $licenseFile -Destination $licenseDirectory -Force
}

$dependencyNotices = [System.Collections.Generic.List[string]]::new()
$dependencyNotices.Add('Taskbar-monitor framework-dependent portable build. .NET runtime files are not bundled.')
$dependencyNotices.Add('Requires the Microsoft .NET 10 Desktop Runtime for Windows x64.')
$assetsFile = Join-Path $artifactsDirectory 'obj\TaskbarMonitor.App\project.assets.json'
if (Test-Path -LiteralPath $assetsFile) {
    $assets = Get-Content $assetsFile -Raw | ConvertFrom-Json
    $packageRoot = @($assets.packageFolders.PSObject.Properties)[0].Name
    foreach ($library in $assets.libraries.PSObject.Properties) {
        if ($library.Value.type -ne 'package') { continue }
        $packageDirectory = Join-Path $packageRoot $library.Value.path
        $nuspec = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' -File | Select-Object -First 1
        if ($null -eq $nuspec) { continue }
        [xml]$metadata = Get-Content -LiteralPath $nuspec.FullName -Raw
        $dependencyNotices.Add("$($library.Name) | license: $($metadata.package.metadata.license.InnerText) | $($metadata.package.metadata.projectUrl)")
        Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object { $_.Name -match '^(LICENSE|LICENCE|COPYING|THIRD.PARTY|NOTICE)' } | ForEach-Object {
            $safePackageName = $library.Name.Replace('/', '-')
            Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $licenseDirectory "$safePackageName-$($_.Name)") -Force
        }
    }
}
$dependencyNotices | Set-Content -LiteralPath (Join-Path $licenseDirectory 'DEPENDENCIES.txt') -Encoding utf8

$zipPath = Join-Path (Split-Path $OutputDirectory -Parent) 'Taskbar-monitor-portable-framework-dependent-win-x64.zip'
Compress-Archive -LiteralPath (Join-Path $OutputDirectory 'TaskbarMonitor.App.exe'), (Join-Path $OutputDirectory 'portable.flag'), (Join-Path $OutputDirectory 'README.txt'), $licenseDirectory -DestinationPath $zipPath -Force
Write-Output "Framework-dependent portable ZIP ready: $zipPath"
