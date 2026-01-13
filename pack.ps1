cd D:\Projects\Il2CppInterop\bin\NuGet
Get-ChildItem *.nupkg | ForEach-Object {
    dotnet nuget push $_.Name --source "https://nuget.pkg.github.com/EnoPM/index.json" --api-key $env:GITHUB_TOKEN
}
