@echo off
set config=%1
if "%config%" == "" (
   set config=Debug
)
 
set version=1.0.1
if not "%PackageVersion%" == "" (
   set version=%PackageVersion%
)

set nuget=
if "%nuget%" == "" (
	set nuget=tools\nuget.exe
)

set package_id="ReSharper.BananaSplit"

%nuget% pack "BananaSplit.nuspec" -NoPackageAnalysis -Version %version% -Properties "Configuration=%config%;ReSharperDep=Wave;ReSharperVer=[5.0];PackageId=%package_id%"