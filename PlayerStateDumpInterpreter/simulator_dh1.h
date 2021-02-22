#ifndef OLMD_SIMULATOR_DERHASS1_H
#define OLMD_SIMULATOR_DERHASS1_H

#include "simulator_original.h"

namespace OlmodPlayerDumpState {
namespace Simulator {

class Derhass1 : public Original {
	private:
		typedef enum {
			INSTR_UPDATE_BUFFERS_SKIPPED,
			INSTR_UPDATE_NO_BUFFER,
			INSTR_UPDATE_1_BUFFER,
			INSTR_UPDATE_2_BUFFERS,

			INSTR_COUNT
		} InstrumentationPointEnums;

		InstrumentationPoint instrp[INSTR_COUNT];

	protected:
		float mms_ship_lag_compensation_max;
		float mms_ship_lag_compensation_scale;
		int ping; // set to <= -1000 to use the captured ping

		virtual const char *GetBaseName() const;

		int GetPing();
		float GetShipExtrapolationTime();
		virtual void DoBufferUpdateX(const UpdateCycle& updateInfo);
		virtual void DoBufferUpdate(const UpdateCycle& updateInfo);
		virtual bool DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results);
		bool LerpRemotePlayer(PlayerSnapshot& p, size_t idx, const InterpolationCycle& interpolationInfo, const PlayerSnapshot&A, const PlayerSnapshot& B, float t);
		float CalculateLerpParameter(float timestamp);
		virtual void Finish();

	public:
		Derhass1(ResultProcessor& rp);
		virtual ~Derhass1();
};


} // namespace Simulator;
} // namespace OlmodPlayerDumpState 


#endif // !OLMD_SIMULATOR_DERHASS1_H
