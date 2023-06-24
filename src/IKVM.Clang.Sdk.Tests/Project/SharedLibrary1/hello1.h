#include <stdio.h>

#ifdef _WIN32
#define EXPORT __declspec(dllexport)
#else
#define EXPORT extern "C"
#endif

EXPORT void hello1();
