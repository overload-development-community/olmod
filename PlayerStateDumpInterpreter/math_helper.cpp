#include "math_helper.h"

namespace OlmodPlayerDumpState {

extern void lerp(const float *a, const float *b, float *c, float t, size_t dims)
{
	size_t i;
	float s = 1.0f - t;
	for (i=0; i<dims; i++) {
		c[i] = s * a[i] + t * b[i];
	}
}

extern void slerp(const float a[4], const float b[4], float c[4], float t)
{
	// TODO...
	c[0] = a[0];
	c[1] = a[1];
	c[2] = a[2];
	c[3] = a[3];
}

extern float clamp(float x, float a, float b)
{
	if (x < a) {
		x = a;
	}
	if (x > b) {
		x = b;
	}
	return x;
}

} // namespace OlmodPlayerDumpState

