@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0.."

echo ============================================
echo  Flamong 로컬 DB 사용자 목록 (읽기만 합니다)
echo  운영 DB 가 아니라 이 PC 의 Server\flamong.db 입니다.
echo ============================================
echo.
set SHOW_NAMES=1
node --no-warnings deploy/db-users.mjs
echo.
pause
