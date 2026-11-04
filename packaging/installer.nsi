Unicode True
!include "MUI2.nsh"
Name "PhoFit"
OutFile "..\artifacts\PhoFit-Setup-Windows-v1.2.2.exe"
InstallDir "$LOCALAPPDATA\Programs\PhoFit"
RequestExecutionLevel user
!define MUI_ABORTWARNING
!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!define MUI_FINISHPAGE_RUN "$INSTDIR\PhoFit-Colegram.exe"
!insertmacro MUI_PAGE_FINISH
!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES
!insertmacro MUI_LANGUAGE "English"
Function .onInit
  ReadRegDWORD $0 HKLM "SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full" "Release"
  IntCmp $0 378389 runtime_ok runtime_missing runtime_ok
runtime_missing:
  MessageBox MB_OK|MB_ICONSTOP "PhoFit requires .NET Framework 4.5 or later. Install .NET Framework 4.8 from Microsoft, then run this installer again."
  Abort
runtime_ok:
FunctionEnd
Section "PhoFit"
  SetShellVarContext current
  SetOutPath "$INSTDIR"
  File "..\dist\PhoFit-Colegram.exe"
  File "..\dist\PhoFit-Colegram.exe.config"
  File "..\README.md"
  File "..\HUONG-DAN.txt"
  File "..\demo-counts.txt"
  File "..\demo-energy.csv"
  WriteUninstaller "$INSTDIR\Uninstall.exe"
  CreateDirectory "$SMPROGRAMS\PhoFit"
  CreateShortcut "$SMPROGRAMS\PhoFit\PhoFit.lnk" "$INSTDIR\PhoFit-Colegram.exe"
  CreateShortcut "$SMPROGRAMS\PhoFit\Uninstall.lnk" "$INSTDIR\Uninstall.exe"
  CreateShortcut "$DESKTOP\PhoFit.lnk" "$INSTDIR\PhoFit-Colegram.exe"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PhoFit" "DisplayName" "PhoFit"
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PhoFit" "UninstallString" '$\"$INSTDIR\Uninstall.exe$\"'
  WriteRegStr HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PhoFit" "DisplayVersion" "1.2.2"
SectionEnd
Section "Uninstall"
  Delete "$INSTDIR\PhoFit-Colegram.exe"
  Delete "$INSTDIR\PhoFit-Colegram.exe.config"
  Delete "$INSTDIR\README.md"
  Delete "$INSTDIR\HUONG-DAN.txt"
  Delete "$INSTDIR\demo-counts.txt"
  Delete "$INSTDIR\demo-energy.csv"
  Delete "$INSTDIR\Uninstall.exe"
  Delete "$DESKTOP\PhoFit.lnk"
  Delete "$SMPROGRAMS\PhoFit\PhoFit.lnk"
  Delete "$SMPROGRAMS\PhoFit\Uninstall.lnk"
  RMDir "$SMPROGRAMS\PhoFit"
  RMDir "$INSTDIR"
  DeleteRegKey HKCU "Software\Microsoft\Windows\CurrentVersion\Uninstall\PhoFit"
SectionEnd
