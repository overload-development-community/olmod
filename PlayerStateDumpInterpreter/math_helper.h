#ifndef OLMD_MATH_HELPER_H
#define OLMD_MATH_HELPER_H

#include <cstdlib>

namespace OlmodPlayerDumpState {

class CQuaternion {
	public:
		float v[4];
		void set(float x, float y, float z, float w);
		void set(const float data[4]);
		void get(float data[4]) const;
		CQuaternion operator + (const CQuaternion &b) const;
		CQuaternion operator * (const CQuaternion &b) const;
		CQuaternion operator * (float s) const;
		float norm() const;
		void normalize();
		float dot(const CQuaternion& b) const;
		void lerp(const CQuaternion& a, const CQuaternion& b, float t);
		void slerp(const CQuaternion& a, const CQuaternion& b, float t);
		void ToEuler(float euler[3]) const;
};

extern void lerp(const float *a, const float *b, float *c, float t, size_t dims=3);
extern void slerp(const CQuaternion& a, const CQuaternion& b, CQuaternion& c, float t);
extern float clamp(float x, float a, float b);

} // namespace OlmodPlayerDumpState

#endif // !OLMD_MATH_HELPER_H
