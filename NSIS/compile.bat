@ECHO OFF

SET NSISPATH=C:\Program Files (x86)\NSIS

"%NSISPATH%\makensis.exe" Srtg.nsi
"%NSISPATH%\makensis.exe" /DARCH64 Srtg.nsi

PAUSE