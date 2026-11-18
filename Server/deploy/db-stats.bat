@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo ============================================
echo  Flamong 운영 DB 현황 (읽기만 합니다)
echo ============================================
echo.
"C:\Program Files\Git\bin\bash.exe" -c "ssh -i ~/.ssh/flamong_oci -o BatchMode=yes ubuntu@flamong.lhsdev.info 'cd / && sudo -u flamong node --input-type=module 2>&1 | grep -v -i -e experimental -e trace-warnings' < db-stats.mjs"
if errorlevel 1 (
  echo.
  echo [실패] 서버에 접속하지 못했거나 조회 중 오류가 났습니다.
  pause
  exit /b 1
)

echo.
echo [백업 파일] 최근 3개
"C:\Program Files\Git\bin\bash.exe" -c "ssh -i ~/.ssh/flamong_oci -o BatchMode=yes ubuntu@flamong.lhsdev.info 'sudo ls -l --time-style=+%%m-%%d /var/backups/flamong | tail -3' | awk '{print \"  \" $6, $7, $5}'"
echo.
pause
