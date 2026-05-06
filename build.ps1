param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$Src = Join-Path $ProjectRoot "src\Program.cs"
$Assets = Join-Path $ProjectRoot "src\Assets"
$BuildDir = Join-Path $ProjectRoot "build"
$DistDir = Join-Path $ProjectRoot "dist"
$Out = Join-Path $DistDir "YellowRabbitAndPenguinPets.exe"
$Rsp = Join-Path $BuildDir "compile.rsp"
$Csc = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"
$Wpf = Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\WPF"
$Icon = Join-Path $Assets "icon.ico"

if (-not (Test-Path -LiteralPath $Csc)) {
    throw "Could not find csc.exe at $Csc. This project builds with the Windows .NET Framework C# compiler."
}

foreach ($required in @($Src, $Icon, (Join-Path $Wpf "WindowsBase.dll"), (Join-Path $Wpf "PresentationCore.dll"), (Join-Path $Wpf "PresentationFramework.dll"))) {
    if (-not (Test-Path -LiteralPath $required)) {
        throw "Missing required file: $required"
    }
}

New-Item -ItemType Directory -Force -Path $BuildDir, $DistDir | Out-Null

$lines = @(
    "/nologo",
    "/target:winexe",
    "/optimize+",
    "/out:`"$Out`"",
    "/reference:System.dll",
    "/reference:System.Core.dll",
    "/reference:System.Xaml.dll",
    "/reference:System.Drawing.dll",
    "/reference:System.Windows.Forms.dll",
    "/reference:`"$(Join-Path $Wpf "WindowsBase.dll")`"",
    "/reference:`"$(Join-Path $Wpf "PresentationCore.dll")`"",
    "/reference:`"$(Join-Path $Wpf "PresentationFramework.dll")`"",
    "`"$Src`"",
    "/win32icon:`"$Icon`"",
    "/resource:`"$Icon`",DesktopPets.Assets.icon.ico"
)

$pets = @("emperor_chick", "yellow_rabbit")
$states = @("idle", "running_right", "running_left", "waving", "jumping", "failed", "waiting", "running", "review")

foreach ($pet in $pets) {
    foreach ($state in $states) {
        $stateDir = Join-Path $Assets (Join-Path $pet $state)
        if (-not (Test-Path -LiteralPath $stateDir)) {
            throw "Missing asset directory: $stateDir"
        }

        Get-ChildItem -LiteralPath $stateDir -Filter "*.png" | Sort-Object Name | ForEach-Object {
            $logicalName = "DesktopPets.Assets.$pet.$state.$($_.BaseName).png"
            $lines += "/resource:`"$($_.FullName)`",$logicalName"
        }
    }
}

Set-Content -LiteralPath $Rsp -Value $lines -Encoding ASCII
& $Csc "@$Rsp"

Write-Host "Built $Out"
