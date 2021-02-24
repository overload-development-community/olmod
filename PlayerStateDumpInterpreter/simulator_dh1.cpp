#include "simulator_dh1.h"

#include "interpreter.h"
#include "math_helper.h"

#include <sstream>

namespace OlmodPlayerDumpState {
namespace Simulator {

PlayerExtraDH1::PlayerExtraDH1()
{
	Init();
}

PlayerExtraDH1::~PlayerExtraDH1()
{
	Clear();
}

void PlayerExtraDH1::Init()
{
	Clear();
}

void PlayerExtraDH1::Clear()
{
	size_t i;
	for (i=0; i<AUX_PLAYER_CHANNELS_COUNT; i++) {
		rpcAux[i]=NULL;
	}
}

Derhass1::Derhass1(ResultProcessor& rp) :
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

Derhass1::~Derhass1()
{
}

const char *Derhass1::GetBaseName() const
{
	return "derhass1";
}

void Derhass1::NewPlayer(Player& p, size_t idx)
{
	size_t i;
	for (i=0; i<PlayerExtraDH1::AUX_PLAYER_CHANNELS_COUNT; i++) {
		bool isNew;
		playerExtra[idx].rpcAux[i]=resultProcessor.GetAuxChannel(p.id, registerID, i, isNew);
		if (isNew) {
			playerExtra[idx].rpcAux[i]->SetLogger(&log);
			playerExtra[idx].rpcAux[i]->SetName(fullName.c_str());
			playerExtra[idx].rpcAux[i]->StartStream(ip->GetOutputDir());
			log.Log(Logger::INFO,"created new aux result process channel '%s'", playerExtra[idx].rpcAux[i]->GetName());
		}
	}
}

int Derhass1::GetPing()
{
	if (ping <= -1000) {
		return ip->GetGameState().ping;
	}
	return ping;
}

// How far ahead to advance ships, in seconds.
float Derhass1::GetShipExtrapolationTime()
{
	float time_ms = (float)GetPing();
	if (time_ms > mms_ship_lag_compensation_max) {
		time_ms = mms_ship_lag_compensation_max;
	}
	return (mms_ship_lag_compensation_scale / 100.0f) * time_ms / 1000.0f;
}

void Derhass1::DoBufferUpdate(const UpdateCycle& updateInfo)
{
	rpcAux[AUX_BUFFER_UPDATE]->Add(updateInfo.timestamp);
	rpcAux[AUX_BUFFER_UPDATE]->Add(updateInfo.m_InterpolationStartTime_before);
	rpcAux[AUX_BUFFER_UPDATE]->Add(m_InterpolationStartTime);
	// not bypass it
	DoBufferUpdateX(updateInfo);
	rpcAux[AUX_BUFFER_UPDATE]->Add(updateInfo.m_InterpolationStartTime_after);
	rpcAux[AUX_BUFFER_UPDATE]->Add(m_InterpolationStartTime);
	rpcAux[AUX_BUFFER_UPDATE]->FlushCurrent();
}

void Derhass1::DoBufferUpdateX(const UpdateCycle& updateInfo)
{
	size_t size = m_PendingPlayerSnapshotMesages.size();
	SimulatorBase::DoBufferUpdate(updateInfo);
	if (m_InterpolationBuffer[0] == NULL) {
		while(m_PendingPlayerSnapshotMesages.size() > 3) {
			m_PendingPlayerSnapshotMesages.pop();
		}
		SetInterpolationBuffer(0, PendingSnapshotDequeue());
		SetInterpolationBuffer(1, PendingSnapshotDequeue());
		SetInterpolationBuffer(2, PendingSnapshotDequeue());
		m_InterpolationStartTime = updateInfo.timestamp;
		return;
	}

	if (size < 1) {
		instrp[INSTR_UPDATE_NO_BUFFER].count++;
		return;
	}

	if (size == 1) {
		SetInterpolationBuffer(0, m_InterpolationBuffer_contents[1]);
		SetInterpolationBuffer(1, m_InterpolationBuffer_contents[2]);
		SetInterpolationBuffer(2, PendingSnapshotDequeue());
		m_InterpolationStartTime += fixedDeltaTime;
	} else if (size == 2) {
		SetInterpolationBuffer(0, m_InterpolationBuffer_contents[2]);
		SetInterpolationBuffer(1, PendingSnapshotDequeue());
		SetInterpolationBuffer(2, PendingSnapshotDequeue());
		m_InterpolationStartTime += 2.0f * fixedDeltaTime;
	} else {
		while(m_PendingPlayerSnapshotMesages.size() > 3) {
			m_PendingPlayerSnapshotMesages.pop();
			m_InterpolationStartTime += fixedDeltaTime;
			instrp[INSTR_UPDATE_BUFFERS_SKIPPED].count++;
		}
		SetInterpolationBuffer(0, PendingSnapshotDequeue());
		SetInterpolationBuffer(1, PendingSnapshotDequeue());
		SetInterpolationBuffer(2, PendingSnapshotDequeue());
		m_InterpolationStartTime += 3.0f * fixedDeltaTime;
	}

	while (m_InterpolationStartTime + 1.5f * fixedDeltaTime < updateInfo.timestamp) {
		m_InterpolationStartTime += fixedDeltaTime;
	}
	while (m_InterpolationStartTime >  updateInfo.timestamp) {
	//	m_InterpolationStartTime -= fixedDeltaTime;
		m_InterpolationStartTime = updateInfo.timestamp;
	}
}

bool Derhass1::DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results)
{
	//if (interpolationInfo.timestamp >= 153.4f && interpolationInfo.timestamp <= 153.44f) {
	if (interpolationInfo.timestamp >= 152.88f && interpolationInfo.timestamp <= 152.9f) {
		log.Log(Logger::INFO,"XXX");
	}
	UpdateCycle u;
	u.timestamp = interpolationInfo.timestamp;
//	DoBufferUpdateX(u);

	if (m_InterpolationBuffer[0] == NULL || m_InterpolationBuffer[1] == NULL || m_InterpolationBuffer[2] == NULL) {
		return Original::DoInterpolation(interpolationInfo, results);
	}

	SimulatorBase::DoInterpolation(interpolationInfo, results);
	float num = CalculateLerpParameter(interpolationInfo.timestamp);

	rpcAux[AUX_INTERPOLATE]->Add(interpolationInfo.timestamp);
	rpcAux[AUX_INTERPOLATE]->Add((float)interpolationInfo.ping);
	rpcAux[AUX_INTERPOLATE]->Add(num);
	rpcAux[AUX_INTERPOLATE]->FlushCurrent();

	for (size_t i=0; i<gameState.playerCnt; i++) {
		const Player& cp=gameState.player[i];
		PlayerSnapshot& p=results.player[results.playerCnt];
		PlayerSnapshot A,B;
		p.id=cp.id;
		PlayerSnapshot *sn[3];
		int k;
		float delta[3][3];
		for (k=0; k<3; k++) {
			sn[k] = GetPlayerSnapshotFromInterpolationBuffer(p.id, m_InterpolationBuffer[k]);
		}
		//if (interpolationInfo.timestamp >= 153.4f && interpolationInfo.timestamp <= 153.44f && p.id==3114) {
		if (interpolationInfo.timestamp >= 152.88f && interpolationInfo.timestamp <= 152.9f && p.id==3114) {
			log.Log(Logger::INFO,"YYY");
		}
		if (sn[0] && sn[1] && sn[2]) {
			A = *sn[1];
			B = *sn[2];
			for (k=0; k<3; k++) {
				delta[0][k] = sn[1]->state.pos[k] - sn[0]->state.pos[k];
				delta[1][k] = sn[2]->state.pos[k] - sn[1]->state.pos[k];
				delta[2][k] = 0.5f* (delta[0][k] + delta[1][k]);
				B.state.pos[k] = sn[2]->state.pos[k] + delta[2][k];
			}

			if (num < 1.0f) {
				if (LerpRemotePlayer(p, i, interpolationInfo, *sn[0], *sn[1], num)) {
					results.playerCnt++;
				}
			} else {
				if (LerpRemotePlayer(p, i, interpolationInfo, *sn[1], *sn[2], num-1.0f)) {
					results.playerCnt++;
				}
		/*
			} else if (num < 2.0f) {
				if (LerpRemotePlayer(p, i, interpolationInfo, *sn[1], *sn[2], num-1.0f)) {
					results.playerCnt++;
				}
			} else {
				if (LerpRemotePlayer(p, i, interpolationInfo, *sn[2], B, num-2.0f)) {
					results.playerCnt++;
				}
		*/	}
		}
	}

	return true;
}

bool Derhass1::LerpRemotePlayer(PlayerSnapshot& p, size_t idx, const InterpolationCycle& interpolationInfo, const PlayerSnapshot&A, const PlayerSnapshot& B, float t)
{
	Player& gp=gameState.player[idx];
	PlayerExtraDH1& ep=playerExtra[idx];
	ResultProcessorAuxChannel *rpc=ep.rpcAux[PlayerExtraDH1::AUX_PLAYER_LERP];

	if (gp.waitForRespawn) {
		return Original::LerpRemotePlayer(p, idx, interpolationInfo, A, B, t);
	} 

	// Lookahead in frames
	float lookahead = (GetShipExtrapolationTime() / fixedDeltaTime);
	
       	// reduce oversteer by extrapolating less for rotation
	float rot_lookahead = lookahead * .5f;

	lerp(A.state.pos,B.state.pos,p.state.pos,t + lookahead);
	slerp(A.state.rot, B.state.rot,p.state.rot,t + rot_lookahead);
	p.state.timestamp = interpolationInfo.timestamp;

	const LerpCycle *lc = interpolationInfo.FindLerp(p.id);
	if (lc) {

		rpc->Add(interpolationInfo.timestamp);
		rpc->Add(lc->t);
		rpc->Add(t);
		rpc->Add(lc->A.state);
		rpc->Add(lc->B.state);
		rpc->Add(A.state);
		rpc->Add(B.state);
		rpc->FlushCurrent();
	} else {
		log.Log(Logger::WARN, "failed to find lerp cycle for player %u", (unsigned)p.id);
	}

	return true;	
}

// Not the same as vanilla
float Derhass1::CalculateLerpParameter(float timestamp)
{
	float num = timestamp - m_InterpolationStartTime;
	if (num < 0.0f) {
		num = 0.0f;
	}
	return num / fixedDeltaTime;
}

void Derhass1::Start()
{
	size_t i;

	Original::Start();
	ClearInstrumentationPoints(instrp, INSTR_COUNT);
	for (i=0; i<AUX_CHANNELS_COUNT; i++) {
		bool isNew;
		rpcAux[i]=resultProcessor.GetAuxChannel(0, registerID, i, isNew);
		if (isNew) {
			rpcAux[i]->SetLogger(&log);
			rpcAux[i]->SetName(fullName.c_str());
			rpcAux[i]->StartStream(ip->GetOutputDir());
			log.Log(Logger::INFO,"created new aux result process channel '%s'", rpcAux[i]->GetName());
		}
	}

	for (i=0; i<MAX_PLAYERS; i++) {
		playerExtra[i].Init();
	}
}

void Derhass1::Finish()
{
	size_t i;
	DumpInstrumentationPoints(instrp, INSTR_COUNT);

	for (i=0; i<MAX_PLAYERS; i++) {
		playerExtra[i].Clear();
	}
	for (i=0; i<AUX_CHANNELS_COUNT; i++) {
		rpcAux[i]=NULL;
	}
	Original::Finish();
}

} // namespace Simulator;
} // namespace OlmodPlayerDumpState 
