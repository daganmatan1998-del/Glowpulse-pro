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
set "STAGE=%USERPROFILE%\jarvis-update-tmp"
set "ZIP=%STAGE%\latest.zip"
set "UNPACK=%STAGE%\unpacked"
set "FALLBACK=%USERPROFILE%\Downloads\jarvis-desktop.zip"
set "SOURCE=https://github.com/daganmatan1998-del/Glowpulse-pro/archive/refs/heads/claude/file-download-0u216w.zip"

REM ------------------------------------------------------------
echo.
echo  ============================================
echo    JARVIS UPDATE
echo  ============================================
echo.
echo   project : %PROJECT%
echo   source  : github
echo.

REM --- 1. fetch the latest version ------------------------------
REM     Downloaded here rather than by hand. Nothing to click, and
REM     no way to update from a zip that has been sitting in the
REM     Downloads folder for a fortnight.
if exist "%STAGE%" rmdir /S /Q "%STAGE%"
mkdir "%STAGE%"
echo  [1/8] downloading the latest version...
curl -sSL --fail -o "%ZIP%" "%SOURCE%"
if errorlevel 1 (
  echo        github did not answer - trying PowerShell...
  powershell -NoProfile -Command "try{ Invoke-WebRequest -Uri '%SOURCE%' -OutFile '%ZIP%' -UseBasicParsing } catch { exit 1 }"
)
REM  curl can leave a short or empty file behind when it gives up, and
REM  "the file exists" would then be true of something unusable.
if exist "%ZIP%" for %%Z in ("%ZIP%") do if %%~zZ LSS 100000 del /Q "%ZIP%"
if not exist "%ZIP%" (
  REM  No internet, or the address moved. A zip sent by hand still works.
  if exist "%FALLBACK%" (
    echo        could not download - using the zip in your Downloads instead.
    copy /Y "%FALLBACK%" "%ZIP%" >nul
  ) else (
    echo  [X] STOP - could not download, and no zip in Downloads either.
    echo.
    echo      Check the internet connection and run this again.
    echo      Or download jarvis-desktop.zip into Downloads by hand.
    goto :fail
  )
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
echo  [2/8] closing JARVIS...
taskkill /IM jarvis.exe /F  >nul 2>&1
taskkill /IM JARVIS.exe /F  >nul 2>&1

REM --- 5. unpack beside the zip, never over the project --------
REM     Into a subfolder, NOT over %STAGE% itself: the zip we just
REM     downloaded is sitting in there, and wiping the folder first
REM     would delete the thing about to be unpacked.
echo  [3/8] unpacking...
if exist "%UNPACK%" rmdir /S /Q "%UNPACK%"
powershell -NoProfile -Command "Expand-Archive -LiteralPath '%ZIP%' -DestinationPath '%UNPACK%' -Force"
if errorlevel 1 (
  echo  [X] STOP - the zip could not be unpacked.
  goto :fail
)

REM  Find dist\index.html wherever it landed. A zip from github
REM  unpacks into a folder named after the branch; one sent by hand
REM  unpacks into jarvis-desktop. Rather than know which, look.
set "SRC="
for /f "delims=" %%D in ('dir /S /B "%UNPACK%\index.html" 2^>nul') do (
  if not defined SRC (
    set "FOUND=%%~dpD"
    if /I "!FOUND:~-5!"=="dist\" (
      for %%P in ("!FOUND!..") do set "SRC=%%~fP"
    )
  )
)
if not exist "!SRC!\dist\index.html" (
  echo  [X] STOP - the zip does not have the layout expected.
  echo.
  echo      Looked for dist\index.html under:
  echo      %UNPACK%
  echo.
  echo      Send Claude the output of:  dir /S /B "%UNPACK%\*.html"
  goto :fail
)

REM --- 6. copy EVERYTHING except the things that must survive --
REM     target/ is the build cache - deleting it turns a
REM     3 minute build into a 30 minute one.
REM     node_modules/ is installed, not shipped.
echo  [4/8] copying the new files...
echo.
robocopy "!SRC!" "%PROJECT%" /E /NFL /NDL /NJH /NJS /NP ^
  /XD "node_modules" "target" "gen" ".git" >nul
if errorlevel 8 (
  echo  [X] STOP - copying failed.
  goto :fail
)

REM --- 7. prove the copy actually landed -----------------------
echo  [5/8] checking the files arrived...
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
  echo  [6/8] installing dependencies. First run only, a few minutes.
  pushd "%PROJECT%"
  call npm install
  REM  checked with IF ERRORLEVEL, not by capturing %%ERRORLEVEL%% into a
  REM  variable: inside a parenthesised block that expands when the block
  REM  is PARSED, so it would hold whatever the code was before npm ran.
  if errorlevel 1 (
    popd
    echo  [X] STOP - npm install failed.
    echo.
    echo      On a machine that has never built JARVIS, three things
    echo      are needed and this script cannot install any of them:
    echo.
    echo        1. Node.js            https://nodejs.org
    echo        2. Rust               https://rustup.rs
    echo        3. The MSVC build tools, which rustup offers to install
    echo           for you the first time you run it. Say yes. Without
    echo           them Rust installs fine and then cannot link anything,
    echo           which is a confusing way to fail.
    echo.
    echo      Install those, then run this again.
    goto :fail
  )
  popd
)

REM --- 9. build --------------------------------------------------
echo.
echo  [7/8] building. Do not close this window.
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
  echo.
  echo      On a machine building this for the first time, the usual
  echo      cause is the MSVC build tools missing - re-run rustup and
  echo      accept the Visual Studio component it offers.
  goto :fail
)

REM --- 10. the exe must be NEW, or you are about to run the old one
echo.
echo  [8/8] checking the build produced a new JARVIS...
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

REM --- 11. put the worker where he can reach it ----------------
REM     It cannot be installed from here - it lives on Cloudflare -
REM     but leaving it inside a temp folder about to be deleted is
REM     how it gets forgotten.
set "WORKER="
for /f "delims=" %%W in ('dir /S /B "%UNPACK%\jarvis-worker.js" 2^>nul') do (
  if not defined WORKER (
    copy /Y "%%W" "%USERPROFILE%\Desktop\jarvis-worker.js" >nul 2>&1
    if not errorlevel 1 set "WORKER=1"
  )
)

REM --- 12. clean up and go -------------------------------------
if exist "%STAGE%" rmdir /S /Q "%STAGE%"

echo.
echo  ============================================
echo    DONE. Starting JARVIS.
echo  ============================================
echo.
if defined WORKER (
  echo   jarvis-worker.js has been put on your Desktop.
  echo   It is NOT installed by this script. To install it:
  echo   Cloudflare dashboard - Workers ^& Pages - your worker
  echo   - Edit code - select all - paste - Deploy.
  echo.
)
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
