@echo off
chcp 65001 >nul
setlocal
cd /d "%~dp0"

echo ============================================
echo  Flamong 비밀번호 새로 정하기 (운영 서버)
echo  본인 확인을 한 뒤에만 쓰세요. 로그인 잠금도 함께 풀립니다.
echo ============================================
echo.
set NICK=
set PW=
set /p NICK=닉네임:
if not defined NICK (
  echo 닉네임을 넣지 않아 그만둡니다.
  pause
  exit /b 1
)

echo.
echo [1/2] 계정을 찾는 중...
"C:\Program Files\Git\bin\bash.exe" reset-password.sh check
if errorlevel 1 (
  echo.
  echo 그만둡니다. 아무것도 바뀌지 않았습니다.
  pause
  exit /b 1
)

echo.
set /p PW=새 비밀번호(숫자 4~6자리):
echo.
echo 위 계정의 비밀번호를 바꿉니다. 계속하려면 아무 키나 누르고, 그만두려면 창을 닫으세요.
pause >nul

echo.
echo [2/2] 바꾸는 중...
"C:\Program Files\Git\bin\bash.exe" reset-password.sh apply
if errorlevel 1 (
  echo.
  echo 바꾸지 못했습니다. 아무것도 바뀌지 않았습니다.
  pause
  exit /b 1
)

echo.
echo 새 비밀번호는 본인에게 직접 알려 주세요. 게임 안에 비밀번호 변경 화면은 없습니다.
pause
