param([switch]$RemoveProgramFile)
$ErrorActionPreference='Stop'
$dir=Join-Path $env:LOCALAPPDATA 'NectarSafetyWatch'
$exe=Join-Path $dir 'NectarTaskbarSafetyWatch.exe'
$runPath='HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
$name='NectarSafetyWatch'
$value=(Get-ItemProperty -Path $runPath -Name $name -ErrorAction SilentlyContinue).$name
$expected='"'+$exe+'" --watch'
if($value -and $value -ne $expected){throw 'Run key differs from expected path. Refusing to remove unrelated entry.'}
if($value){Remove-ItemProperty -Path $runPath -Name $name -ErrorAction Stop}
if(Test-Path $exe){
 & $exe --stop
 for($i=0;$i -lt 20;$i++){
  if(@(Get-Process NectarTaskbarSafetyWatch -ErrorAction SilentlyContinue).Count -eq 0){break}
  Start-Sleep -Milliseconds 200
 }
}
$running=@(Get-Process NectarTaskbarSafetyWatch -ErrorAction SilentlyContinue)
if($running.Count -gt 0){throw 'Watchdog did not stop; EXE left unchanged'}
if($RemoveProgramFile -and (Test-Path $exe)){Remove-Item -LiteralPath $exe -Force}
'SAFETY_WATCH_AUTOSTART_REMOVED=True'
'SAFETY_WATCH_RUNNING=False'
'NECTAR_UNCHANGED=True'
'LOGS_PRESERVED='+ (Test-Path (Join-Path $dir 'watchdog.log'))
