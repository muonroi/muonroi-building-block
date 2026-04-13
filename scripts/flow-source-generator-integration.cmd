@echo off
setlocal

set "SG_PROJECT=%~1"
if "%SG_PROJECT%"=="" set "SG_PROJECT=D:\sources\Core\MuonroiBuildingBlock\src\Muonroi.RuleEngine.SourceGenerators\Muonroi.RuleEngine.SourceGenerators.csproj"

py -3 "%~dp0flow-source-generator-integration.py" --source-generator-project "%SG_PROJECT%"

endlocal

