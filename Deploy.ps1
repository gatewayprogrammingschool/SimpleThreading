function Deploy-Package {
    param(
        [Parameter(Mandatory = $true)][string]$SolutionDir,
        [Parameter(Mandatory = $true)][string]$BuildDir,
        [Parameter(Mandatory = $true)][string]$Namespace,
        [Parameter(Mandatory = $true)][string]$Assembly
    )	

    $projFolder = $SolutionDir + '/' + $Namespace + '/'
    $proj = $projFolder + $Namespace + '.csproj'
    $assm = $BuildDir + '/' + $Assembly
    $nuspec =  $Namespace + '.nuspec'
    $nupkg = '../Assets/' + $Namespace + '*.nupkg'
    $apiKey = $env:ApiKey
    $source = $env:NugetSource

    Write-Host "`$SolutionDir: $SolutionDir"
    Write-Host "`$BuildDir: $BuildDir"
    Write-Host "`$Namespace: $Namespace"
    Write-Host "`$Assembly: $Assembly"
    Write-Host "`$projFolder: $projFolder"
    Write-Host "`$proj: $proj"
    Write-Host "`$assm: $assm"
    Write-Host "`$nuspec: $nuspec"
    Write-Host "`$nupkg: $nupkg"
    Write-Host "`$proj: $proj"
    Write-Host "`$source: $source"

    if(Test-Path $projFolder) {
        Set-Location $projFolder

        & dotnet build --configuration Release --no-restore

        if ($LASTEXITCODE -eq 0) {
            Write-Host "nuget.exe pack $nuspec -OutputDirectory ../Assets/"
            & nuget.exe pack $nuspec -OutputDirectory ../Assets/
            if ($LASTEXITCODE -eq 0) {
                Write-Host "Get-ChildItem $nupkg | Sort-Object | Select-Object -Last 1"
                $package = Get-ChildItem $nupkg | Sort-Object | Select-Object -Last 1
                Write-Host "nuget.exe push $package.FullName -ApiKey `$apiKey -Source $source"
                & nuget.exe push $package.FullName -ApiKey $apiKey -Source $source
            }
        }
    } else {
        throw "Project Folder not found."
    }
}

Clear-Host

#Deploy-Package -SolutionDir %1 -BuildDir %2 -Namespace %3 -Assembly %4
Deploy-Package `
    -SolutionDir './src'`
    -BuildDir './src/GPS.SimpleThreading/bin/Release'`
    -Namespace "GPS.SimpleThreading"`
    -Assembly "GPS.SimpleThreading.dll"