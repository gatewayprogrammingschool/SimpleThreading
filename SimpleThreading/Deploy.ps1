function Deploy-Package
{
    param(
        [Parameter(Mandatory=$true)][string]$SolutionDir,
        [Parameter(Mandatory=$true)][string]$BuildDir,
        [Parameter(Mandatory=$true)][string]$Namespace,
        [Parameter(Mandatory=$true)][string]$Assembly
    )	

    $proj = $SolutionDir + '\' + $Namespace + '\' + $Namespace + '.csproj'
	$assm = $BuildDir + '\' + $Assembly

&    "F:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\amd64\msbuild.exe" $proj /p:Configuration=Release /p:RunOctoPack=true

	if($LASTEXITCODE -eq 0) {
		$AssemblyVersion = 
			[Diagnostics.FileVersionInfo]::GetVersionInfo($assm).FileVersion
    
		$package = $BuildDir +'\' +$Namespace +'.' +$AssemblyVersion+'.nupkg'

		Set-Location $SolutionDir

		packages\OctoPack.3.6.1\build\nuget.exe setApiKey $env::NugetAPIKey -source https://www.nuget.org
		packages\OctoPack.3.6.1\build\nuget.exe push $package -Source https://www.nuget.org/api/v2/package
	}
}

Clear-Host

#Deploy-Package -SolutionDir %1 -BuildDir %2 -Namespace %3 -Assembly %4
Deploy-Package -SolutionDir 'F:\GPS\SimpleThreading\SimpleThreading' -BuildDir 'F:\GPS\SimpleThreading\SimpleThreading\GPS.SimpleThreading\bin\Release' -Namespace "GPS.SimpleThreading" -Assembly "GPS.SimpleThreading.dll"