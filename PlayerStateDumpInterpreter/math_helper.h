#ifndef OLMD_MATH_HELPER_H
#define OLMD_MATH_HELPER_H

#include <cstdlib>

namespace OlmodPlayerDumpState {

extern void lerp(const float *a, const float *b, float *c, float t, size_t dims=3);
extern void slerp(const float a[4], const float b[4], float c[4], float t);
extern float clamp(float x, float a, float b);

} // namespace OlmodPlayerDumpState

#endif // !OLMD_MATH_HELPER_H
