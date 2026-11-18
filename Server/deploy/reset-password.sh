#!/bin/bash
# reset-password.bat 이 부른다. 닉네임 · 비밀번호는 환경 변수 NICK · PW 로 받는다
# (명령줄에 싣지 않아 따옴표 · 특수 문자 문제가 없고, 프로세스 목록에도 남지 않는다)
# 사용: reset-password.sh check | dry | apply
set -euo pipefail
cd "$(dirname "$0")"
MODE="${1:-check}"

if [ -z "${NICK:-}" ]; then
  echo '[중단] 닉네임이 비었습니다.'
  exit 2
fi
if [ "$MODE" != check ] && ! [[ "${PW:-}" =~ ^[0-9]{4,6}$ ]]; then
  echo '[중단] 비밀번호는 숫자 4~6자리여야 합니다.'
  exit 2
fi

args=$(printf '%s\n%s' "$NICK" "${PW:-}" | base64 -w0)
{
  printf 'const ARGS = "%s";\nconst MODE = "%s";\n' "$args" "$MODE"
  cat reset-password.mjs
} | ssh -i ~/.ssh/flamong_oci -o BatchMode=yes ubuntu@flamong.lhsdev.info \
  'cd / && sudo -u flamong node --no-warnings --input-type=module'
