if ( $env:VisualStudioVersion -eq $null ) {
    Import-Module "C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\Tools\Microsoft.VisualStudio.DevShell.dll"
    Enter-VsDevShell -VsInstallPath "C:\Program File\Microsoft Visual Studio\2022\Enterprise\" -StartInPath $(Get-Location)
}

& dotnet run --project "$PSScriptRoot\eng\src\BuildEngineering.csproj" -- $args
exit $LASTEXITCODE

