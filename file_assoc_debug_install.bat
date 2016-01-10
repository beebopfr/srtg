@ECHO OFF

SET CURRPATH=%~dp0
SET BINPATH=%CURRPATH%Srtg\bin\Debug\Srtg.exe

REG ADD "HKEY_CLASSES_ROOT\.srtg" /t REG_SZ /d beebop.srtg.1 /f
REG ADD "HKEY_CLASSES_ROOT\.srtg\OpenWithProgIds" /v srtg.exe /t REG_SZ /f
REG ADD "HKEY_CLASSES_ROOT\.srtg\DefaultIcon" /t REG_SZ /d "\"%BINPATH%\",-1" /f
REG ADD "HKEY_CLASSES_ROOT\Applications\srtg.exe\shell\open\command" /t REG_SZ /d "\"%BINPATH%\" \"%%1\"" /f

ECHO.
PAUSE