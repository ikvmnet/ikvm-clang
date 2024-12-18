#ifdef STATICHELLO
#include <statichello1.h>
#else
#include <hello1.h>
#endif

int main() {
#ifdef STATICHELLO
    statichello1();
#else
    hello1();
#endif
}

#ifdef EMSCRIPTEN
int exit() {
    return 0;
}
#endif
