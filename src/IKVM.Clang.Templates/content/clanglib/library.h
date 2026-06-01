#ifndef CLANGLIBRARY_H
#define CLANGLIBRARY_H

#ifdef _WIN32
	#ifdef BUILDING_CLANGLIB
		#define CLANGLIB_API __declspec(dllexport)
	#else
		#define CLANGLIB_API __declspec(dllimport)
	#endif
#else
	#define CLANGLIB_API __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

CLANGLIB_API int add(int a, int b);

#ifdef __cplusplus
}
#endif

#endif // CLANGLIBRARY_H
