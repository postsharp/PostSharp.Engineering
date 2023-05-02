(& dotnet nuget locals http-cache -c) | Out-Null
& dotnet run --project "$PSScriptRoot\eng\src\BuildEngineering.csproj" -- $args
exit $LASTEXITCODE

