#ifndef OLMD_SIMULATOR_ORIGINAL_H
#define OLMD_SIMULATOR_ORIGINAL_H

#include "simulator_base.h"
#include "player_types.h"

#include <queue>

namespace OlmodPlayerDumpState {
namespace Simulator {

class Original : public SimulatorBase {

	private:
		typedef enum {
			INSTR_UPDATE_BUFFERS_SKIPPED,
			INSTR_UPDATE_PATH_INIT_3,
			INSTR_UPDATE_PATH_TAKE_1_1,
			INSTR_UPDATE_PATH_TAKE_1_2,
			INSTR_UPDATE_PATH_TAKE_2,
			INSTR_INTERPOLATE_FRAME_01,
			INSTR_INTERPOLATE_FRAME_12,

			INSTR_COUNT
		} InstrumentationPointEnums;

		InstrumentationPoint instrp[INSTR_COUNT];

	protected:
		typedef std::queue<PlayerSnapshotMessage> MsgQueue;


		MsgQueue m_PendingPlayerSnapshotMesages;
		PlayerSnapshotMessage* m_InterpolationBuffer[3];
		PlayerSnapshotMessage  m_InterpolationBuffer_contents[3];
		float m_InterpolationStartTime;

		float fixedDeltaTime;


		virtual const char *GetBaseName() const;

		void SetInterpolationBuffer(int idx, const PlayerSnapshotMessage& msg);
		void ResetInterpolationBuffer(int idx);

		PlayerSnapshot* GetPlayerSnapshotFromInterpolationBuffer(uint32_t playerId, PlayerSnapshotMessage* msg);
		PlayerSnapshotMessage PendingSnapshotDequeue();


		virtual void DoBufferEnqueue(const PlayerSnapshotMessage& msg);
		virtual void DoBufferUpdate(const UpdateCycle& updateInfo);
		virtual bool DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results);
		bool LerpRemotePlayer(PlayerSnapshot& p, size_t idx, const InterpolationCycle& interpolationInfo, const PlayerSnapshot&A, const PlayerSnapshot& B, float t);
		float CalculateLerpParameter(float timestamp);

		virtual void Start();
		virtual void Finish();
	public:
		Original(ResultProcessor& rp);
		virtual ~Original();
};


} // namespace Simulator;
} // namespace OlmodPlayerDumpState 


#endif // !OLMD_SIMULATOR_ORIGINAL_H
