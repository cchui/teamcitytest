cls

# '[p]sake' is the same as 'psake' but $Error is not polluted
remove-module [p]sake

# find psake's path
$psakeModule = (Get-ChildItem ("..\Packages\psake*\tools\psake.psm1")).FullName | Sort-Object $_ | select -last 1
 
Import-Module $psakeModule

# you can put arguments to task in multiple lines using `
Invoke-psake -buildFile .\default.ps1 `
			 -taskList Test `
			 -framework 4.5.2 `
		     -properties @{ 
				 "buildConfiguration" = "Release"
				 "buildPlatform" = "Any CPU"} `
			 -parameters @{ 
				 "solutionFile" = "..\ICAPInterface.sln"}

Write-Host "Build exit code:" $LastExitCode

# Propagating the exit code so that builds actually fail when there is a problem
exit $LastExitCode