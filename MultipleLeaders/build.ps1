[CmdletBinding()]
param(
    [string] $ArcGISProInstallDir = (Join-Path $env:ProgramFiles 'ArcGIS\Pro')
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$workspaceRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$projectPath = Join-Path $workspaceRoot 'src\MultipleLeaders\MultipleLeaders.csproj'
$checksPath = Join-Path $workspaceRoot 'tests\MultipleLeaders.Checks\MultipleLeaders.Checks.csproj'
$damlPath = Join-Path $workspaceRoot 'src\MultipleLeaders\Config.daml'
$schemaPath = Join-Path $ArcGISProInstallDir 'bin\ArcGIS.Desktop.Framework.xsd'
$artifactRoot = Join-Path $workspaceRoot 'artifacts'
$stagePath = Join-Path $artifactRoot ('.stage-' + [guid]::NewGuid().ToString('N'))
$finalPackage = Join-Path $artifactRoot 'MultipleLeaders.esriAddinX'
$cachePath = Join-Path $workspaceRoot '.tools\build-cache'
$temporaryPackage = $null
$savedEnvironment = @{}

function Assert-WorkspacePath([string] $Path) {
    $fullPath = [System.IO.Path]::GetFullPath($Path)
    $prefix = $workspaceRoot.TrimEnd('\', '/') + [System.IO.Path]::DirectorySeparatorChar
    if (-not $fullPath.StartsWith($prefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to change a path outside the workspace: $fullPath"
    }
    return $fullPath
}

function Invoke-Dotnet([string[]] $Arguments) {
    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE. The previous add-in artifact was preserved."
    }
}

function Test-Daml {
    $validationErrors = [System.Collections.Generic.List[string]]::new()
    $settings = [System.Xml.XmlReaderSettings]::new()
    $settings.DtdProcessing = [System.Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $settings.ValidationType = [System.Xml.ValidationType]::Schema
    $null = $settings.Schemas.Add('http://schemas.esri.com/DADF/Registry', $schemaPath)
    $settings.add_ValidationEventHandler({
        param($sender, $eventArgs)
        $validationErrors.Add($eventArgs.Message)
    })
    $reader = [System.Xml.XmlReader]::Create($damlPath, $settings)
    try {
        while ($reader.Read()) { }
    }
    finally {
        $reader.Dispose()
    }
    if ($validationErrors.Count -gt 0) {
        throw ("Config.daml failed the ArcGIS Pro schema check:`n" + ($validationErrors -join "`n"))
    }
    [xml] $document = Get-Content -LiteralPath $damlPath -Raw
    if ($document.DocumentElement.defaultAssembly -ne 'MultipleLeaders.dll' -or
        $document.DocumentElement.defaultNamespace -ne 'MultipleLeaders') {
        throw 'Config.daml assembly and namespace must match MultipleLeaders.'
    }
    Write-Host 'Config.daml passed the installed ArcGIS Pro schema.'
}

try {
    $null = Get-Command dotnet -ErrorAction Stop
    foreach ($requiredPath in @($projectPath, $checksPath, $damlPath, $schemaPath)) {
        if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
            throw "Required file not found: $requiredPath"
        }
    }
    $stagePath = Assert-WorkspacePath $stagePath
    $null = New-Item -ItemType Directory -Path $stagePath -Force

    # Keep restore/first-run caches local, and restore the caller's environment on exit.
    $buildEnvironment = @{
        DOTNET_CLI_HOME = (Join-Path $cachePath 'dotnet-home')
        NUGET_PACKAGES = (Join-Path $cachePath 'nuget-packages')
        APPDATA = (Join-Path $cachePath 'appdata')
        DOTNET_CLI_TELEMETRY_OPTOUT = '1'
        DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
        DOTNET_GENERATE_ASPNET_CERTIFICATE = 'false'
        DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = 'false'
        DOTNET_NOLOGO = '1'
    }
    foreach ($name in $buildEnvironment.Keys) {
        $savedEnvironment[$name] = [System.Environment]::GetEnvironmentVariable($name, 'Process')
        [System.Environment]::SetEnvironmentVariable($name, $buildEnvironment[$name], 'Process')
    }

    Test-Daml
    $binaryPath = Join-Path $stagePath 'binaries'
    $installProperty = '-p:ArcGISProInstallDir=' + [System.IO.Path]::GetFullPath($ArcGISProInstallDir)
    Invoke-Dotnet -Arguments @('build', $projectPath, '--configuration', 'Release', '--output', $binaryPath, $installProperty, '--nologo')
    Invoke-Dotnet -Arguments @('run', '--project', $checksPath, '--configuration', 'Release', $installProperty)

    # Only this project's artifacts are distributable. ArcGIS runtime assemblies stay in Pro.
    $allowedBinaries = @('MultipleLeaders.dll', 'MultipleLeaders.pdb', 'MultipleLeaders.deps.json')
    $unexpectedDependencies = @(Get-ChildItem -LiteralPath $binaryPath -Filter '*.dll' -File |
        Where-Object { $_.Name -ne 'MultipleLeaders.dll' -and $_.Name -notmatch '^(ArcGIS|Esri)\.' })
    if ($unexpectedDependencies.Count -gt 0) {
        throw ('Review new runtime dependencies before packaging: ' + ($unexpectedDependencies.Name -join ', '))
    }
    if (-not (Test-Path -LiteralPath (Join-Path $binaryPath 'MultipleLeaders.dll') -PathType Leaf)) {
        throw 'Build did not produce MultipleLeaders.dll.'
    }
    $packageContents = Join-Path $stagePath 'package'
    $installPath = Join-Path $packageContents 'Install'
    $null = New-Item -ItemType Directory -Path $installPath -Force
    Copy-Item -LiteralPath $damlPath -Destination (Join-Path $packageContents 'Config.daml')
    foreach ($fileName in $allowedBinaries) {
        $sourcePath = Join-Path $binaryPath $fileName
        if (Test-Path -LiteralPath $sourcePath -PathType Leaf) {
            Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $installPath $fileName)
        }
    }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $temporaryPackage = Assert-WorkspacePath (Join-Path $artifactRoot ('.MultipleLeaders-' + [guid]::NewGuid().ToString('N') + '.esriAddinX'))
    [System.IO.Compression.ZipFile]::CreateFromDirectory($packageContents, $temporaryPackage)
    $archive = [System.IO.Compression.ZipFile]::OpenRead($temporaryPackage)
    try {
        $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace('\', '/') })
        $allowedEntries = @('Config.daml') + @($allowedBinaries | ForEach-Object { 'Install/' + $_ })
        if ('Config.daml' -notin $entries -or 'Install/MultipleLeaders.dll' -notin $entries -or
            @($entries | Where-Object { $_ -notin $allowedEntries }).Count -gt 0) {
            throw 'Packaged add-in content did not match the expected manifest.'
        }
    }
    finally {
        $archive.Dispose()
    }

    # Replacement happens only after compilation, checks, schema validation, and archive validation.
    if (Test-Path -LiteralPath $finalPackage) {
        # PowerShell converts $null to an empty string for this overload.
        # Use an actual temporary backup path instead.
        [System.IO.File]::Replace($temporaryPackage, $finalPackage, (Join-Path $stagePath 'previous.esriAddinX'))
    }
    else {
        [System.IO.File]::Move($temporaryPackage, $finalPackage)
    }
    Write-Host "Built: $finalPackage"
    Write-Host 'The package has not been installed in ArcGIS Pro.'
}
finally {
    foreach ($name in $savedEnvironment.Keys) {
        [System.Environment]::SetEnvironmentVariable($name, $savedEnvironment[$name], 'Process')
    }
    if (Test-Path -LiteralPath $stagePath) {
        $safeStagePath = Assert-WorkspacePath $stagePath
        Remove-Item -LiteralPath $safeStagePath -Recurse -Force
    }
    if ($temporaryPackage -and (Test-Path -LiteralPath $temporaryPackage)) {
        $safeTemporaryPackage = Assert-WorkspacePath $temporaryPackage
        Remove-Item -LiteralPath $safeTemporaryPackage -Force
    }
}
