@echo off
chcp 65001 >nul
rem 웹 빌드 올리기. 이 파일을 Unity 빌드 폴더(index.html 이 있는 곳)에 deploy.bat 이라는 이름으로 복사해 두고 거기서 실행한다.
rem 자기가 있는 폴더를 통째로 올리고, deploy.bat 자신은 올리지 않는다.
setlocal
cd /d "%~dp0"

echo ============================================
echo  Flamong 웹 빌드 배포
echo  이 폴더의 빌드를 https://flamong.lhsdev.info 에 올립니다.
echo ============================================
if not exist "index.html" (
  echo [오류] index.html 이 없습니다. Unity 에서 먼저 빌드해 주세요.
  pause
  exit /b 1
)
if not exist "Build" (
  echo [오류] Build 폴더가 없습니다. Unity 에서 먼저 빌드해 주세요.
  pause
  exit /b 1
)

echo.
echo [올릴 빌드]
"C:\Program Files\Git\bin\bash.exe" -c "ls -l --time-style=+%%m-%%d_%%H:%%M Build | awk 'NR>1{print \"  \" $6, $7}'; stat -c %%s Build/*.data.br Build/*.wasm.br | awk '{s+=$1} END{printf \"  data + wasm = %%.2f MB (limit 15 MB)\n\", s/1000000}'"
echo.
echo 실제 서비스에 올라갑니다. 계속하려면 아무 키나 누르고, 그만두려면 창을 닫으세요.
pause >nul

echo.
echo [1/2] 올리는 중...
"C:\Program Files\Git\bin\bash.exe" -c "set -e; set -o pipefail; trap 'rm -f ./OFL.txt' EXIT; cp /c/Github/Flamong/Assets/Content/Common/03_Resources/Font/OFL.txt ./OFL.txt; tar -czf - --exclude=./deploy.bat . | ssh -i ~/.ssh/flamong_oci -o BatchMode=yes ubuntu@flamong.lhsdev.info 'set -e; sudo rm -rf /var/www/flamong.new /var/www/flamong.old; sudo mkdir /var/www/flamong.new; sudo tar -C /var/www/flamong.new -xzf -; sudo chown -R root:root /var/www/flamong.new; sudo mv /var/www/flamong /var/www/flamong.old; sudo mv /var/www/flamong.new /var/www/flamong; sudo rm -rf /var/www/flamong.old'"
if errorlevel 1 (
  echo.
  echo [실패] 올리지 못했습니다. 위의 오류를 확인해 주세요. 사이트는 이전 빌드 그대로입니다.
  pause
  exit /b 1
)

echo.
echo [2/2] 사이트가 새 빌드를 내주는지 확인하는 중...
"C:\Program Files\Git\bin\bash.exe" -c "curl -s -m 10 https://flamong.lhsdev.info/ | grep -oE '[0-9a-f]{32}\.[a-z.]+' | sort -u | while read f; do code=$(curl -s -m 30 -o /dev/null -w '%%{http_code}' https://flamong.lhsdev.info/Build/$f); if [ -f Build/$f ]; then same=local-match; else same=NOT-IN-LOCAL-BUILD; fi; echo \"  $f  $code  $same\"; done"

echo.
echo 끝났습니다. 위 네 줄이 모두 200 · local-match 이면 새 빌드가 올라간 것입니다.
echo 폰에서 https://flamong.lhsdev.info 를 열어 Title 왼쪽 위 빌드 ID 를 확인해 주세요.
pause
