#include <stdio.h>
#include <hello2.h>

#include "hello1.h"

EXPORT void hello1() {
    printf("hello1\n");
    hello2();
}
