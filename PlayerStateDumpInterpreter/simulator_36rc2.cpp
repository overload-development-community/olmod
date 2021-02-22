#include "simulator_36rc2.h"
#include "math_helper.h"

#include <sstream>

namespace OlmodPlayerDumpState {
namespace Simulator {

Olmod36RC2::Olmod36RC2(ResultProcessor& rp) :
	Original(rp),
	mms_ship_lag_compensation_max(100.0f),
	mms_ship_lag_compensation_scale(50.0f),
	ping(-1000)
{
	cfg.Add(ConfigParam(mms_ship_lag_compensation_max,"compensation_max", "max"));
	cfg.Add(ConfigParam(mms_ship_lag_compensation_scale,"compensation_scale", "scale"));
	cfg.Add(ConfigParam(ping,"ping"));

	instrp[INSTR_UPDATE_BUFFERS_SKIPPED].name = "36RC2_BUFFER_UPDATE_SKIPPED";
	instrp[INSTR_UPDATE_NO_BUFFER].name = "36RC2_BUFFER_UPDATE_NONE";
	instrp[INSTR_UPDATE_1_BUFFER].name = "36RC2_BUFFER_UPDATE_1";
	instrp[INSTR_UPDATE_2_BUFFERS].name = "36RC2_BUFFER_UPDATE_2";
}

Olmod36RC2::~Olmod36RC2()
{
}

const char *Olmod36RC2::GetBaseName() const
{
	return "olmod-0.3.6-rc2";
}

int Olmod36RC2::GetPing()
{
	if (ping <= -1000) {
		return ip->GetGameState().ping;
	}
	return ping;
}

// How far ahead to advance ships, in seconds.
float Olmod36RC2::GetShipExtrapolationTime()
{
	float time_ms = (float)GetPing();
	if (time_ms > mms_ship_lag_compensation_max) {
		time_ms = mms_ship_lag_compensation_max;
	}
	return (mms_ship_lag_compensation_scale / 100.0f) * time_ms / 1000.0f;
}

void Olmod36RC2::DoBufferUpdate(const UpdateCycle& updateInfo)
{
	SimulatorBase::DoBufferUpdate(updateInfo);
	if (m_InterpolationBuffer[0] == NULL) {
		Original::DoBufferUpdate(updateInfo);
	} else {
		if (m_PendingPlayerSnapshotMesages.size() < 1) {
			instrp[INSTR_UPDATE_NO_BUFFER].count++;
			return;
		} else if (m_PendingPlayerSnapshotMesages.size() == 1) {
			SetInterpolationBuffer(1, m_InterpolationBuffer_contents[2]);
			SetInterpolationBuffer(2, PendingSnapshotDequeue());
			instrp[INSTR_UPDATE_1_BUFFER].count++;
		} else {
			while (m_PendingPlayerSnapshotMesages.size() > 2) {
				instrp[INSTR_UPDATE_BUFFERS_SKIPPED].count++;
				m_PendingPlayerSnapshotMesages.pop();
			}
			SetInterpolationBuffer(1, PendingSnapshotDequeue());
			SetInterpolationBuffer(2, PendingSnapshotDequeue());
			instrp[INSTR_UPDATE_2_BUFFERS].count++;

		}
		m_InterpolationStartTime = updateInfo.timestamp;
	}
}

bool Olmod36RC2::DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results)
{

	if (m_InterpolationBuffer[0] == NULL || m_InterpolationBuffer[1] == NULL || m_InterpolationBuffer[2] == NULL) {
		return Original::DoInterpolation(interpolationInfo, results);
	}
	SimulatorBase::DoInterpolation(interpolationInfo, results);
	float num = CalculateLerpParameter(interpolationInfo.timestamp);
	PlayerSnapshotMessage *msg = NULL;
	PlayerSnapshotMessage *msg2 = NULL;
	msg = m_InterpolationBuffer[1];
	msg2 = m_InterpolationBuffer[2];

	for (size_t i=0; i<gameState.playerCnt; i++) {
		const Player& cp=gameState.player[i];
		PlayerSnapshot& p=results.player[results.playerCnt];
		p.id=cp.id;
		PlayerSnapshot* playerSnapshotFromInterpolationBuffer = GetPlayerSnapshotFromInterpolationBuffer(p.id, msg);
		PlayerSnapshot* playerSnapshotFromInterpolationBuffer2 = GetPlayerSnapshotFromInterpolationBuffer(p.id, msg2);
		if (playerSnapshotFromInterpolationBuffer != NULL && playerSnapshotFromInterpolationBuffer2 != NULL) {
			if (LerpRemotePlayer(p, i, interpolationInfo, *playerSnapshotFromInterpolationBuffer, *playerSnapshotFromInterpolationBuffer2, num)) {
				results.playerCnt++;
			}
		}
	}

	return true;
}

bool Olmod36RC2::LerpRemotePlayer(PlayerSnapshot& p, size_t idx, const InterpolationCycle& interpolationInfo, const PlayerSnapshot&A, const PlayerSnapshot& B, float t)
{
	Player& gp=gameState.player[idx];

	if (gp.waitForRespawn) {
		return Original::LerpRemotePlayer(p, idx, interpolationInfo, A, B, t);
	} 

	// Lookahead in frames
	float lookahead = 1.0f + (GetShipExtrapolationTime() / fixedDeltaTime);
	
       	// reduce oversteer by extrapolating less for rotation
	float rot_lookahead = lookahead * .5f;

	lerp(A.state.pos,B.state.pos,p.state.pos,t + lookahead);
	slerp(A.state.rot, B.state.rot,p.state.rot,t + rot_lookahead);
	p.state.timestamp = interpolationInfo.timestamp;

	return true;	
}

// Not the same as vanilla
float Olmod36RC2::CalculateLerpParameter(float timestamp)
{
	float num = timestamp - m_InterpolationStartTime;
	if (num < 0.0f) {
		num = 0.0f;
	}
	return num / fixedDeltaTime;
}

void Olmod36RC2::Finish()
{
	Original::Finish();
	DumpInstrumentationPoints(instrp, INSTR_COUNT);
}

} // namespace Simulator;
} // namespace OlmodPlayerDumpState 
