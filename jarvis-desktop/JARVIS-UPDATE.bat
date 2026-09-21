@echo off
setlocal enabledelayedexpansion
title JARVIS - update
color 0B

REM ============================================================
REM   JARVIS - UPDATE TO THE LATEST VERSION
REM
REM   Save this file once. Run it every time, forever.
REM   It does not care which files changed in a given version:
REM   it copies whatever is in the zip over the project and
REM   leaves the build cache alone.
REM
REM   The only three lines you may ever need to touch:
REM ============================================================

set "PROJECT=%USERPROFILE%\jarvis"
set "ZIP=%USERPROFILE%\Downloads\jarvis-desktop.zip"
set "STAGE=%USERPROFILE%\jarvis-update-tmp"

REM ------------------------------------------------------------
echo.
echo  ============================================
echo    JARVIS UPDATE
echo  ============================================
echo.
echo   project : %PROJECT%
echo   zip     : %ZIP%
echo.

REM --- 1. the zip has to be there -----------------------------
if not exist "%ZIP%" (
  echo  [X] STOP - no zip found.
  echo.
  echo      Expected it here:
  echo      %ZIP%
  echo.
  echo      Download jarvis-desktop.zip into your Downloads
  echo      folder and run this again. Do not unpack it yourself.
  goto :fail
)

REM --- 2. first run, or an update? ----------------------------
REM     Either way this script handles it. A missing project is
REM     not an error, it is an install.
if exist "%PROJECT%\src-tauri\tauri.conf.json" (
  echo   mode    : UPDATE
) else (
  echo   mode    : FIRST INSTALL
  echo.
  echo   No project at that path yet, so one will be created there.
  echo   If JARVIS already lives somewhere else, close this window
  echo   and edit the PROJECT line near the top of this file.
  echo.
  if not exist "%PROJECT%" mkdir "%PROJECT%"
)

REM --- 3. remember what the current build looks like -----------
set "EXE=%PROJECT%\src-tauri\target\release\jarvis.exe"
set "BEFORE=none"
if exist "%EXE%" for %%F in ("%EXE%") do set "BEFORE=%%~tF"
echo   current build : !BEFORE!

if exist "%PROJECT%\src-tauri\target" (
  echo   build cache   : present  ^(build takes 2-5 minutes^)
) else (
  echo   build cache   : MISSING  ^(build takes 25-30 minutes^)
)
echo.

REM --- 4. close it, or the exe cannot be replaced --------------
echo  [1/7] closing JARVIS...
taskkill /IM jarvis.exe /F  >nul 2>&1
taskkill /IM JARVIS.exe /F  >nul 2>&1

REM --- 5. unpack into a staging folder, never over the project -
echo  [2/7] unpacking the zip...
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
powershell -NoProfile -Command "Expand-Archive -LiteralPath '%ZIP%' -DestinationPath '%STAGE%' -Force" 
if errorlevel 1 (
  echo  [X] STOP - the zip could not be unpacked.
  goto :fail
)

set "SRC=%STAGE%\jarvis-desktop"
if not exist "%SRC%\dist\index.html" (
  REM some unpackers add an extra level - look one deeper
  if exist "%STAGE%\jarvis-desktop\jarvis-desktop\dist\index.html" (
    set "SRC=%STAGE%\jarvis-desktop\jarvis-desktop"
  )
)
if not exist "!SRC!\dist\index.html" (
  echo  [X] STOP - the zip does not have the layout expected.
  echo.
  echo      Looked for dist\index.html under:
  echo      %STAGE%
  echo.
  echo      Send Claude the output of:  dir /S /B "%STAGE%\*.html"
  goto :fail
)

REM --- 6. copy EVERYTHING except the things that must survive --
REM     target/ is the build cache - deleting it turns a
REM     3 minute build into a 30 minute one.
REM     node_modules/ is installed, not shipped.
echo  [3/7] copying the new files...
echo.
robocopy "!SRC!" "%PROJECT%" /E /NFL /NDL /NJH /NJS /NP ^
  /XD "node_modules" "target" "gen" ".git" >nul
if errorlevel 8 (
  echo  [X] STOP - copying failed.
  goto :fail
)

REM --- 7. prove the copy actually landed -----------------------
echo  [4/7] checking the files arrived...
fc /B "!SRC!\dist\index.html" "%PROJECT%\dist\index.html" >nul 2>&1
if errorlevel 1 (
  echo  [X] STOP - dist\index.html did not copy.
  goto :fail
)
echo        dist\index.html  OK
if exist "!SRC!\src-tauri\src\main.rs" (
  fc /B "!SRC!\src-tauri\src\main.rs" "%PROJECT%\src-tauri\src\main.rs" >nul 2>&1
  if errorlevel 1 (
    echo  [X] STOP - src-tauri\src\main.rs did not copy.
    goto :fail
  )
  echo        src-tauri\src\main.rs  OK
)

REM --- 8. dependencies, on a first run or after a wipe -----------
if not exist "%PROJECT%\node_modules" (
  echo.
  echo  [5/7] installing dependencies. First run only, a few minutes.
  pushd "%PROJECT%"
  call npm install
  REM  checked with IF ERRORLEVEL, not by capturing %%ERRORLEVEL%% into a
  REM  variable: inside a parenthesised block that expands when the block
  REM  is PARSED, so it would hold whatever the code was before npm ran.
  if errorlevel 1 (
    popd
    echo  [X] STOP - npm install failed.
    echo      Node.js and Rust both have to be installed first:
    echo      https://nodejs.org   and   https://rustup.rs
    goto :fail
  )
  popd
)

REM --- 9. build --------------------------------------------------
echo.
echo  [6/7] building. Do not close this window.
echo        "Compiling ..." lines are what should be happening.
echo.
pushd "%PROJECT%"
call npm run tauri build
set "BUILDRC=%ERRORLEVEL%"
popd

if not "%BUILDRC%"=="0" (
  echo.
  echo  [X] STOP - the build failed.
  echo.
  echo      Scroll up and copy the last 20 lines to Claude.
  echo      Look for a red line starting with  error
  goto :fail
)

REM --- 10. the exe must be NEW, or you are about to run the old one
echo.
echo  [7/7] checking the build produced a new JARVIS...
if not exist "%EXE%" (
  echo  [X] STOP - the build finished but there is no jarvis.exe.
  goto :fail
)
for %%F in ("%EXE%") do set "AFTER=%%~tF"
echo        before : !BEFORE!
echo        after  : !AFTER!
if "!AFTER!"=="!BEFORE!" (
  echo.
  echo  [X] STOP - jarvis.exe was NOT rebuilt.
  echo      Running it now would start the old version.
  echo      Send Claude the build output above.
  goto :fail
)

REM --- 11. clean up and go -------------------------------------
if exist "%STAGE%" rmdir /S /Q "%STAGE%"

echo.
echo  ============================================
echo    DONE. Starting JARVIS.
echo  ============================================
echo.
echo   If a jarvis-worker.js was sent too, it is NOT
echo   installed by this script. Paste it into the
echo   Cloudflare dashboard: Workers ^& Pages - your
echo   worker - Edit code - paste - Deploy.
echo.
echo   Press Ctrl+Shift+Space if the orb does not appear:
echo   it starts hidden on purpose.
echo.
start "" "%EXE%"
timeout /t 8 >nul
exit /b 0

:fail
echo.
echo  ============================================
echo    STOPPED. Nothing was started.
echo  ============================================
echo.
pause
exit /b 1
