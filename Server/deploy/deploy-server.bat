@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0.."

echo ============================================
echo  Flamong 서버 배포
echo  이 폴더(Server)의 코드를 https://flamong.lhsdev.info 서버에 올립니다.
echo  DB 와 웹 빌드는 건드리지 않습니다.
echo ============================================
if not exist "index.js" (
  echo [오류] index.js 가 없습니다. Server\deploy 폴더 안에서 실행해 주세요.
  pause
  exit /b 1
)

echo.
echo [1/3] 테스트 중...
"C:\Program Files\Git\bin\bash.exe" -c "set -o pipefail; npm test 2>&1 | grep -aE ' (tests|pass|fail) [0-9]+$' | sed 's/^/  /'"
if errorlevel 1 (
  echo.
  echo [중단] 테스트가 실패했습니다. 올리지 않았습니다. npm test 로 실패한 항목을 확인해 주세요.
  pause
  exit /b 1
)

echo.
echo 테스트를 통과했습니다. 실제 서비스에 올리고 서버를 다시 켭니다(2초쯤 끊깁니다).
echo 계속하려면 아무 키나 누르고, 그만두려면 창을 닫으세요.
pause >nul

echo.
echo [2/3] 올리고 다시 켜는 중...
"C:\Program Files\Git\bin\bash.exe" -c "set -e; set -o pipefail; tar -czf - db.js index.js package.json package-lock.json schema.sql routes rules | ssh -i ~/.ssh/flamong_oci -o BatchMode=yes ubuntu@flamong.lhsdev.info 'set -e; cd /opt/flamong/server; sudo find . -mindepth 1 -maxdepth 1 ! -name node_modules -exec rm -rf {} + ; sudo tar -xzf - -C /opt/flamong/server; sudo chown -R root:root db.js index.js package.json package-lock.json schema.sql routes rules; sudo systemctl restart flamong; sleep 2; echo service: $(systemctl is-active flamong); systemctl is-active --quiet flamong' 2>&1 | grep -v daemon-reload | sed 's/^/  /'"
if errorlevel 1 (
  echo.
  echo [실패] 올리지 못했거나 서버가 켜지지 않았습니다. 위의 오류를 확인해 주세요.
  pause
  exit /b 1
)

echo.
echo [3/3] 서버가 응답하는지 확인하는 중...
"C:\Program Files\Git\bin\bash.exe" -c "code=$(curl -s -m 15 -o /dev/null -w '%%{http_code}' -H 'X-Player-Id: deploy-check' https://flamong.lhsdev.info/api/me); printf '  /api/me: %%s\n' $code; test x$code = x401"
if errorlevel 1 (
  echo.
  echo [확인 필요] 기대한 응답(401)이 아닙니다. 서버 로그를 확인해 주세요.
  pause
  exit /b 1
)

echo.
echo 끝났습니다. 서버가 새 코드로 켜져 있습니다. (없는 계정으로 물어 401 이 오면 정상입니다)
echo 화면도 바뀌었다면 웹 빌드를 따로 올려 주세요(빌드 폴더의 deploy.bat).
pause
