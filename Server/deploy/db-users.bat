@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo ============================================
echo  Flamong 운영 DB 사용자 목록 (읽기만 합니다)
echo  https://flamong.lhsdev.info 서버의 실제 계정입니다.
echo ============================================
echo.
echo 팀과 이름(실명)은 기본으로 가립니다. 닉네임 찾아 주기처럼 꼭 필요할 때만 보세요.
set NAMES=0
set ANSWER=
set /p ANSWER=팀과 이름도 보려면 y 를 입력하고 Enter, 아니면 그냥 Enter:
if /i "%ANSWER%"=="y" set NAMES=1
echo.

"C:\Program Files\Git\bin\bash.exe" -c "ssh -i ~/.ssh/flamong_oci -o BatchMode=yes ubuntu@flamong.lhsdev.info 'cd / && sudo -u flamong env FLAMONG_SERVER=/opt/flamong/server FLAMONG_DB=/var/lib/flamong/flamong.db SHOW_NAMES=%NAMES% node --no-warnings --input-type=module' < db-users.mjs"
if errorlevel 1 (
  echo.
  echo [실패] 서버에 접속하지 못했거나 조회 중 오류가 났습니다.
)
echo.
pause
