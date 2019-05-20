function Deploy-Package
{
    param(
        [Parameter(Mandatory=$true)][string]$SolutionDir,
        [Parameter(Mandatory=$true)][string]$BuildDir,
        [Parameter(Mandatory=$true)][string]$Namespace,
        [Parameter(Mandatory=$true)][string]$Assembly
    )	

    $projFolder = $SolutionDir + '\' + $Namespace + '\'
    $proj = $projFolder + $Namespace + '.csproj'
	$assm = $BuildDir + '\' + $Assembly
	$nuspec = $SolutionDir + '\' + $Namespace + '\' + $Namespace + '.nuspec'
    $nupkg = $SolutionDir + '\Assets\' + $Namespace + '*.nupkg'
	$apiKey = $env:ApiKey
	$source = $env:NugetSource

    Set-Location $projFolder

    & "C:\Program Files (x86)\Microsoft Visual Studio\2017\MSBuild\15.0\Bin\msbuild.exe" $proj /p:Configuration=Release

	if($LASTEXITCODE -eq 0) {
		& nuget.exe pack $nuspec -OutputDirectory ..\Assets\
        if($LASTEXITCODE -eq 0) {
            $package = Get-ChildItem $nupkg | Sort-Object | Select-Object -Last 1
            & nuget.exe push $package.FullName -ApiKey $apiKey -Source $source
        }
	}
}

Clear-Host

#Deploy-Package -SolutionDir %1 -BuildDir %2 -Namespace %3 -Assembly %4
Deploy-Package `
	-SolutionDir 'C:\GitHub\SimpleThreading\SimpleThreading'`
	-BuildDir 'C:\GitHub\SimpleThreading\SimpleThreading\GPS.SimpleThreading\bin\Release'`
	-Namespace "GPS.SimpleThreading"`
	-Assembly "GPS.SimpleThreading.dll"