// 줄 단위 입력 버퍼 타입.
// Arduino 빌드는 .ino의 함수 선언을 파일 맨 위로 자동으로 끌어올려서,
// 그 선언에 쓰이는 타입은 이렇게 헤더에 따로 둬야 해요.
#pragma once
#include <stddef.h>

struct LineBuffer {
  char data[160];
  size_t len;
};

typedef void (*LineHandler)(const char* line);
