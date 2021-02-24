#ifndef OLMD_DUMP_TYPES_H
#define OLMD_DUMP_TYPES_H

#include "player_types.h"

#include <vector>
#include <cstdint>

namespace OlmodPlayerDumpState {

enum Command {
	NONE = 0,
	ENQUEUE,
	UPDATE_BEGIN,
	UPDATE_END,
	INTERPOLATE_BEGIN,
	INTERPOLATE_END,
	LERP_BEGIN,
	LERP_END,
	FINISH,

	COMMAND_END_MARKER
};

struct UpdateCycle {
	bool valid;
	float timestamp;
	float m_InterpolationStartTime_before;
	float m_InterpolationStartTime_after;

	UpdateCycle() :
		valid(false)
	{}
};

struct LerpCycle {
	uint32_t waitForRespawn_before;
	uint32_t waitForRespawn_after;
	PlayerSnapshot A,B;
	float t;
};

struct InterpolationCycle {
	bool valid;
	float timestamp;
	int ping;
	std::vector<LerpCycle> lerps;

	InterpolationCycle() :
		valid(false)
	{}

	const LerpCycle* FindLerp(uint32_t id) const {
		for (size_t i=0; i<lerps.size(); i++) {
			if (lerps[i].A.id == id) {
				return &lerps[i];
			}
		}
		return NULL;
	}
};

} // namespace OlmodPlayerDumpState 


#endif // !OLMD_DUMP_TYPES_H
