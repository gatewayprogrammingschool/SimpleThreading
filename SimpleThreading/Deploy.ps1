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

&    "F:\Program Files (x86)\Microsoft Visual Studio\2017\Community\MSBuild\15.0\Bin\amd64\msbuild.exe" $proj /p:Configuration=Release

	if($LASTEXITCODE -eq 0) {


		$AssemblyVersion = 
			[Diagnostics.FileVersionInfo]::GetVersionInfo($assm).FileVersion
    
		$packageName = $Namespace +'.' +$AssemblyVersion+'.nupkg'
		$package = $BuildDir +'\' +$packageName
		$destination = $BuildDir + '\' + $packageName + "\" + $packageName
		$sourceDestination = $BuildDir + '\' + $packageName + "\" + $Namespace +'.' +$AssemblyVersion+'.symbols.nupkg'
		Set-Location $SolutionDir

		$releaseNotes = [IO.File]::ReadAllText($SolutionDir + "\ReleaseNotes.txt")

		.paket/paket.exe pack --release-notes $releaseNotes --symbols -v $package
		Paket-Push -File $destination -ApiKey $env:NugetAPIKey -url https://www.nuget.org -endpoint /api/v2/package -Verbose
	}
}

Clear-Host

#Deploy-Package -SolutionDir %1 -BuildDir %2 -Namespace %3 -Assembly %4
Deploy-Package -SolutionDir 'F:\GPS\SimpleThreading\SimpleThreading' -BuildDir 'F:\GPS\SimpleThreading\SimpleThreading\GPS.SimpleThreading\bin\Release' -Namespace "GPS.SimpleThreading" -Assembly "GPS.SimpleThreading.dll"