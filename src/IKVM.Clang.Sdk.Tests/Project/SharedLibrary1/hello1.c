#include <hello2.h>
#include <statichello1.h>
#include <stdio.h>

#include "hello1.h"

EXPORT void hello1() {
    hello2();
    statichello1();
    printf(STRING);
}
