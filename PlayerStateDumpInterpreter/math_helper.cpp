#include "math_helper.h"

#define _USE_MATH_DEFINES
#include <cmath>

namespace OlmodPlayerDumpState {

void CQuaternion::set(float x, float y, float z, float w)
{
	v[0]=x;
	v[1]=y;
	v[2]=z;
	v[3]=w;
}

void CQuaternion::set(const float data[4])
{
	v[0]=data[0];
	v[1]=data[1];
	v[2]=data[2];
	v[3]=data[3];
}

void CQuaternion::get(float data[4]) const 
{
	data[0]=v[0];
	data[1]=v[1];
	data[2]=v[2];
	data[3]=v[3];
}

CQuaternion CQuaternion::operator+(const CQuaternion& b) const
{
	CQuaternion c;
	c.v[0]=v[0] + b.v[0];
	c.v[1]=v[1] + b.v[1];
	c.v[2]=v[2] + b.v[2];
	c.v[3]=v[3] + b.v[3];
	return c;
}

CQuaternion CQuaternion::operator*(const CQuaternion& b) const
{
	CQuaternion c;
	c.v[0] = v[3] * b.v[0]  +  v[0] * b.v[3]  +  v[1] * b.v[2] - v[2] * b.v[1];
	c.v[1] = v[3] * b.v[1]  +  v[1] * b.v[3]  +  v[2] * b.v[0] - v[0] * b.v[2];
	c.v[2] = v[3] * b.v[2]  +  v[2] * b.v[3]  +  v[0] * b.v[1] - v[1] * b.v[0];
	c.v[3] = v[3] * b.v[3]  -  v[0] * b.v[0]  -  v[1] * b.v[1] - v[2] * b.v[2];
	return c;
}

CQuaternion CQuaternion::operator*(float s) const
{
	CQuaternion c;
	c.v[0] = s * v[0];
	c.v[1] = s * v[1];
	c.v[2] = s * v[2];
	c.v[3] = s * v[3];
	return c;
}

float CQuaternion::norm() const
{
	return v[0]*v[0] + v[1]*v[1] + v[2]*v[2] + v[3]*v[3];
}

void CQuaternion::normalize()
{
	float n=norm();

	if (n > 0.0f) {
		float s = 1.0f / sqrtf(n);
		v[0] *=s;
		v[1] *=s;
		v[2] *=s;
		v[3] *=s;
	}
}

float CQuaternion::dot(const CQuaternion& b) const
{
	return v[0]*b.v[0] + v[1]*b.v[1]+ v[2]*b.v[2] + v[3]*b.v[3];
}

void CQuaternion::lerp(const CQuaternion& a, const CQuaternion& b, float t)
{
	*this=a * (1.0f - t) + b * t;
	normalize();
}

void CQuaternion::slerp(const CQuaternion& a, const CQuaternion& b, float t)
{
	CQuaternion c;
	float dot=a.dot(b);

	if (dot < 0.0f) {
		dot = -dot;
		c=b * -1.0f;
	} else {
		c=b;
	}

	if (dot < 0.99999f) {
		float w=acosf(dot);
		*this=(a * sinf( w * (1.0f-t) ) + c * sinf(w*t)) * (1.0f/sinf(w));
	} else {
		lerp(a,b,t);
	}
}

void CQuaternion::ToEuler(float euler[3]) const
{
	// roll (x-axis rotation)
	double sinr_cosp = 2.0 * (v[3] * v[0] + v[1] * v[2]);
	double cosr_cosp = 1.0 - 2.0 * (v[0] * v[0] + v[1] * v[1]);
	euler[2] = (float) std::atan2(sinr_cosp, cosr_cosp);

	// pitch (y-axis rotation)
	double sinp = 2.0 * (v[3] * v[1] - v[2] * v[0]);
	if (std::abs(sinp) >= 1.0) {
		euler[1] = (float)std::copysign(M_PI / 2, sinp); // use 90 degrees if out of range
	} else {
		euler[1] = (float)std::asin(sinp);
	}

	// yaw (z-axis rotation)
	double siny_cosp = 2.0 * (v[3] * v[2] + v[0] * v[1]);
	double cosy_cosp = 1.0 - 2.0 * (v[1] * v[1] + v[2] * v[2]);
	euler[0] = (float)std::atan2(siny_cosp, cosy_cosp);
}

extern void lerp(const float *a, const float *b, float *c, float t, size_t dims)
{
	size_t i;
	float s = 1.0f - t;
	for (i=0; i<dims; i++) {
		c[i] = s * a[i] + t * b[i];
	}
}

extern void slerp(const CQuaternion& a, const CQuaternion& b, CQuaternion& c, float t)
{
	c.lerp(a,b,t);
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

