$paramHash = @{
 Path = ".\Scripts\Publish-SimpleThreadingManifest.psd1"
 RootModule = ".\Scripts\Publish-SimpleThreading.psm1"
 Author = "The Sharp Ninja"
 CompanyName = "Gateway Programming School"
 ModuleVersion = "2.3.0"
 Guid = [System.Guid]::NewGuid()
 PowerShellVersion = "3.0"
 Description = "Publish GPS.SimpleThreading to your repository."
 FunctionsToExport = "Publish-SimpleThreading"
 AliasesToExport = ""
 VariablesToExport = ""
 CmdletsToExport = ""
}
New-ModuleManifest @paramHash | Write-Host