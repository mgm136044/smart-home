#!/usr/bin/env bash
# 제스처 앱을 미리보기(브라우저) + 인식 로그로 실행해요.
#   ./run_preview.sh                         허브 주소는 config(.local).toml 값
#   ./run_preview.sh http://192.168.137.1:5000
# 미리보기: http://<라즈베리파이 IP>:8080  (PC 허브 대시보드의 "제스처 카메라" 칸에서도 보여요)
cd "$(dirname "$0")/gesture" || exit 1
export PYTHONUNBUFFERED=1
HUB_ARGS=()
[ -n "$1" ] && HUB_ARGS=(--hub "$1")
../.venv/bin/python gesture_app.py --no-window --preview-port 8080 --verbose "${HUB_ARGS[@]}" 2>&1 \
  | grep -a --line-buffered -v -E '^(W0000|I0000|WARNING: All log|INFO: Created TensorFlow|Error in cpuinfo)'
