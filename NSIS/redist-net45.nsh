!include LogicLib.nsh
!define REDIST_NET45_URL "http://go.microsoft.com/fwlink/?linkid=397707"
Var REDIST_NET45_INSTALLED
Var REDIST_NET45_PATH
Function REDIST_NET45_Check 
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" Release
  ${If} $0 < 379893
    StrCpy $REDIST_NET45_INSTALLED "No"    
    Return
  ${EndIf}
  StrCpy $REDIST_NET45_INSTALLED "Yes"
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" InstallPath
  StrCpy $REDIST_NET45_PATH $0
FunctionEnd
Function REDIST_NET45_Install 
  !define  DOTNET_EXE_PATH "$TEMP\NDP452-KB2901954-Web.exe"
	Push ""
  inetc::get /CAPTION "Downloading Microsoft .NET Framework 4.5.2" /CANCELTEXT "Cancel" ${REDIST_NET45_URL} ${DOTNET_EXE_PATH} /END  
	Pop $R0
  ${If} $R0 != "OK"
		DetailPrint "Error : $R0"
    Delete "${DOTNET_EXE_PATH}"
    Abort ".NET Framework 4.5.2 installation has been cancelled by user."
  ${EndIf}
  ExecWait "${DOTNET_EXE_PATH} /norestart /passive /showrmui"
  Delete "${DOTNET_EXE_PATH}"
FunctionEnd