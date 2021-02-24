#include "simulator_original.h"
#include "math_helper.h"

namespace OlmodPlayerDumpState {
namespace Simulator {

Original::Original(ResultProcessor& rp) :
	SimulatorBase(rp),
	fixedDeltaTime(1.0f/60.0f)
{
	instrp[INSTR_UPDATE_BUFFERS_SKIPPED].name = "ORIGINAL_BUFFERS_SKIPPED";
	instrp[INSTR_UPDATE_PATH_INIT_3].name = "ORIGINAL_BUFFER_INIT_3";
	instrp[INSTR_UPDATE_PATH_TAKE_1_1].name = "ORIGINAL_BUFFER_UPDATE_TAKE_1_NUM<=0.5";
	instrp[INSTR_UPDATE_PATH_TAKE_1_2].name = "ORIGINAL_BUFFER_UPDATE_TAKE_1_NUM>0.5";
	instrp[INSTR_UPDATE_PATH_TAKE_2].name = "ORIGINAL_BUFFER_UPDATE_TAKE_2";
	instrp[INSTR_INTERPOLATE_FRAME_01].name = "ORIGINAL_INTERPOLATE_01";
	instrp[INSTR_INTERPOLATE_FRAME_12].name = "ORIGINAL_INTERPOLATE_12";
}

Original::~Original()
{
}

const char *Original::GetBaseName() const
{
	return "original";
}

void Original::SetInterpolationBuffer(int idx, const PlayerSnapshotMessage& msg)
{
	m_InterpolationBuffer_contents[idx]=msg;
	m_InterpolationBuffer[idx] = &m_InterpolationBuffer_contents[idx];
}

void Original::ResetInterpolationBuffer(int idx)
{
	m_InterpolationBuffer[idx] = NULL;
}

PlayerSnapshot* Original::GetPlayerSnapshotFromInterpolationBuffer(uint32_t playerId, PlayerSnapshotMessage* msg)
{
	if (!msg) {
		return NULL;
	}
	for (size_t i=0; i< msg->snapshot.size(); i++) {
		if (msg->snapshot[i].id == playerId) {
			return &msg->snapshot[i];
		}
	}
	return NULL;
}

PlayerSnapshotMessage Original::PendingSnapshotDequeue()
{
	PlayerSnapshotMessage msg;

	if (m_PendingPlayerSnapshotMesages.size() > 0) {
		msg = m_PendingPlayerSnapshotMesages.front();
		m_PendingPlayerSnapshotMesages.pop();
	} else {
		log.Log(Logger::WARN, "attempt to dequeue from empyty m_PendingPlayerSnapshotMesages queue!");
	}

	return msg;
}

void Original::DoBufferEnqueue(const PlayerSnapshotMessage& msg)
{
	SimulatorBase::DoBufferEnqueue(msg);
	m_PendingPlayerSnapshotMesages.push(msg);
}

void Original::DoBufferUpdate(const UpdateCycle& updateInfo)
{
	SimulatorBase::DoBufferUpdate(updateInfo);
	if (m_InterpolationBuffer[0] == NULL) {
		if (m_PendingPlayerSnapshotMesages.size() >= 3) {
			SetInterpolationBuffer(0, PendingSnapshotDequeue());
			SetInterpolationBuffer(1, PendingSnapshotDequeue());
			SetInterpolationBuffer(2, PendingSnapshotDequeue());
			m_InterpolationStartTime = updateInfo.timestamp;
			instrp[INSTR_UPDATE_PATH_INIT_3].count++;
		}
	} else {
		if (m_PendingPlayerSnapshotMesages.size() == 0) {
			return;
		}
		float num = CalculateLerpParameter(updateInfo.timestamp);
		int num2=((!(num >= 1.0f)) ? 1 : 2);
		if ( num2 == 2 && m_PendingPlayerSnapshotMesages.size() < 2) {
			return;
		}
		while (m_PendingPlayerSnapshotMesages.size() > 3 + (size_t)num2) {
			instrp[INSTR_UPDATE_BUFFERS_SKIPPED].count++;
			m_PendingPlayerSnapshotMesages.pop();
		}
		if (num2 == 1) {
			SetInterpolationBuffer(0, m_InterpolationBuffer_contents[1]);
			SetInterpolationBuffer(1, m_InterpolationBuffer_contents[2]);
			SetInterpolationBuffer(2, PendingSnapshotDequeue());
			if (num <= 0.5f) {
				m_InterpolationStartTime = updateInfo.timestamp;
				instrp[INSTR_UPDATE_PATH_TAKE_1_1].count++;
			} else {
				m_InterpolationStartTime += fixedDeltaTime;
				instrp[INSTR_UPDATE_PATH_TAKE_1_2].count++;
			}
		} else {
			SetInterpolationBuffer(0, m_InterpolationBuffer_contents[1]);
			SetInterpolationBuffer(1, PendingSnapshotDequeue());
			SetInterpolationBuffer(2, PendingSnapshotDequeue());
			m_InterpolationStartTime = updateInfo.timestamp;
			instrp[INSTR_UPDATE_PATH_TAKE_2].count++;
		}
	}
}

bool Original::DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results)
{
	SimulatorBase::DoInterpolation(interpolationInfo, results);
	if (m_InterpolationBuffer[0] == NULL || m_InterpolationBuffer[1] == NULL || m_InterpolationBuffer[2] == NULL) {
		return false;
	}
	float num = CalculateLerpParameter(interpolationInfo.timestamp);
	PlayerSnapshotMessage *playerSnapshotToClientMessage = NULL;
	PlayerSnapshotMessage *playerSnapshotToClientMessage2 = NULL;
	if (num <= 0.5f) {
		playerSnapshotToClientMessage = m_InterpolationBuffer[0];
		playerSnapshotToClientMessage2 = m_InterpolationBuffer[1];
		num = clamp(num/0.5f, 0.0f, 1.0f);
		instrp[INSTR_INTERPOLATE_FRAME_01].count++;
	} else {
		playerSnapshotToClientMessage = m_InterpolationBuffer[1];
		playerSnapshotToClientMessage2 = m_InterpolationBuffer[2];
		num = clamp((num-0.5f)/0.5f, 0.0f, 1.0f);
		instrp[INSTR_INTERPOLATE_FRAME_12].count++;
	}

	for (size_t i=0; i<gameState.playerCnt; i++) {
		const Player& cp=gameState.player[i];
		PlayerSnapshot& p=results.player[results.playerCnt];
		p.id=cp.id;
		PlayerSnapshot* playerSnapshotFromInterpolationBuffer = GetPlayerSnapshotFromInterpolationBuffer(p.id, playerSnapshotToClientMessage);
		PlayerSnapshot* playerSnapshotFromInterpolationBuffer2 = GetPlayerSnapshotFromInterpolationBuffer(p.id, playerSnapshotToClientMessage2);
		if (playerSnapshotFromInterpolationBuffer != NULL && playerSnapshotFromInterpolationBuffer2 != NULL) {
			if (LerpRemotePlayer(p, i, interpolationInfo, *playerSnapshotFromInterpolationBuffer, *playerSnapshotFromInterpolationBuffer2, num)) {
				results.playerCnt++;
			}
		}
	}

	return true;
}

bool Original::LerpRemotePlayer(PlayerSnapshot& p, size_t idx, const InterpolationCycle& interpolationInfo, const PlayerSnapshot&A, const PlayerSnapshot& B, float t)
{
	Player& gp=gameState.player[idx];

	// we do not track respawn and death position, but we have captured the
	// resulting decision
	const LerpCycle *lc = interpolationInfo.FindLerp(p.id);
	if (gp.waitForRespawn) {
		if (lc) {
			if (lc->waitForRespawn_after) {
				return false;
			}
			gp.waitForRespawn = 0;

		} else {
			log.Log(Logger::WARN,"ignoring missing Lerp data for player");
			gp.waitForRespawn = 0;
		}
	} 

	t = clamp(t,0.0f, 1.0f);
	lerp(A.state.pos,B.state.pos,p.state.pos,t);
	slerp(A.state.rot, B.state.rot,p.state.rot,t);
	p.state.timestamp = interpolationInfo.timestamp;

	return true;	
}

float Original::CalculateLerpParameter(float timestamp)
{
	float num = timestamp - m_InterpolationStartTime;
	if (num < 0.0f) {
		num = 0.0f;
	}
	return clamp(num / (2.0f * fixedDeltaTime), 0.0f, 1.0f);
}

void Original::Start()
{
	SimulatorBase::Start();
	ClearInstrumentationPoints(instrp, INSTR_COUNT);

	m_InterpolationBuffer[0] = NULL;
	m_InterpolationBuffer[1] = NULL;
	m_InterpolationBuffer[2] = NULL;
	m_InterpolationStartTime = 0.0f;
}

void Original::Finish()
{
	SimulatorBase::Finish();
	DumpInstrumentationPoints(instrp, INSTR_COUNT);
}

} // namespace Simulator;
} // namespace OlmodPlayerDumpState 
