$ErrorActionPreference='Stop'
$here=Split-Path -Parent $MyInvocation.MyCommand.Path
$source=Join-Path $here 'NectarTaskbarSafetyWatch.cs'
$csc=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(!(Test-Path $csc)){throw 'Windows .NET Framework 4.x 64-bit compiler not found'}
if(!(Test-Path $source)){throw 'C# source file missing'}
$test=Join-Path $here 'NectarTaskbarSafetyWatch.Test.exe'
$watch=Join-Path $here 'NectarTaskbarSafetyWatch.exe'
& $csc /nologo /optimize+ /target:exe ('/out:'+$test) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $source
if($LASTEXITCODE -ne 0){throw 'Console build failed'}
$result=& $test --self-test
$result|ForEach-Object{Write-Output $_}
if($LASTEXITCODE -ne 0 -or $result -notcontains 'DECISION_TESTS=8/8'){throw 'Decision self-tests failed'}
& $csc /nologo /optimize+ /target:winexe ('/out:'+$watch) /reference:System.Windows.Forms.dll /reference:System.Drawing.dll $source
if($LASTEXITCODE -ne 0){throw 'Windowless build failed'}
'COMPILED_FILE='+$watch
'SHA256='+ (Get-FileHash -Algorithm SHA256 -LiteralPath $watch).Hash.ToLower()
