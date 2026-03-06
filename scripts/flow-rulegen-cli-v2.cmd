@echo off
setlocal

set "RULEGEN_PROJECT=%~1"
if "%RULEGEN_PROJECT%"=="" set "RULEGEN_PROJECT=D:\sources\Core\MuonroiBuildingBlock\tools\Muonroi.RuleGen\Muonroi.RuleGen.csproj"

py -3 "%~dp0flow-rulegen-cli-v2.py" --rulegen-project "%RULEGEN_PROJECT%"

endlocal

