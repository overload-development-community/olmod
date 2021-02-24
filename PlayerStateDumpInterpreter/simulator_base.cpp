#include "simulator_base.h"
#include "interpreter.h"

namespace OlmodPlayerDumpState {

SimulatorGameState::SimulatorGameState() :
	playerCnt(0),
	playersCycle(0)
{}

SimulatorBase::SimulatorBase(ResultProcessor& rp) :
	resultProcessor(rp),
	registerID(0),
	ip(NULL)
{
	instrp[INSTR_DMAX_SHENANIGANS].name = "DMAX_SHENANIGANS";
	instrp[INSTR_DMAX_SHENANIGANS].count = (uint64_t)-1;  //we still love you, dmax...

	instrp[INSTR_ENQUEUE].name = "BASE_BUFFER_ENQUEUE";
	instrp[INSTR_UPDATE].name = "BASE_BUFFER_UPDATE";
	instrp[INSTR_INTERPOLATE].name = "BASE_INTERPOLATE";
	UpdateName();
}

SimulatorBase::~SimulatorBase()
{
}

void SimulatorBase::SyncGamestate(const GameState& gs)
{
	if (gameState.playersCycle == gs.playersCycle && gameState.playerCnt == gs.players.size()) {
		return;
	}

	log.Log(Logger::DEBUG, "syncing player count from %u to %u, cycle %u to %u", (unsigned)gameState.playerCnt, (unsigned)gs.players.size(), (unsigned)gameState.playersCycle, (unsigned)gs.playersCycle);

	PlayerMap::const_iterator it;
	for (it=gs.players.begin(); it != gs.players.end(); it++) {
		const Player& cp=it->second;
		size_t i;
		bool found = false;
		for (i=0; i<gameState.playerCnt; i++) {
			if (gameState.player[i].id == cp.id) {
				found = true;
				break;
			}
		}
		if (!found) {
			if (gameState.playerCnt + 1 < MAX_PLAYERS) {
				Player& x=gameState.player[gameState.playerCnt++];
				x.Invalidate();
				x.id = cp.id;
				NewPlayer(x,gameState.playerCnt-1);
			} else {
				log.Log(Logger::WARN, "failed to add new player %u, reached limit", (unsigned)cp.id);
			}
		}
	}

	gameState.playersCycle = gs.playersCycle;
}

void SimulatorBase::UpdateWaitForRespawn(const GameState& gs)
{
	size_t i;
	for (i=0; i<gameState.playerCnt; i++) {
		const Player* master = gs.FindPlayer(gameState.player[i].id);
		if (master) {
			UpdateWaitForRespawn(gameState.player[i].id, master->waitForRespawnReset, i);
		} else {
			log.Log(Logger::WARN, "failed to find player %u in global gamestate (UpdateWaitForRespawn)", (unsigned)gameState.player[i].id);
		}
	}
}

void SimulatorBase::NewPlayer(Player& p, size_t idx)
{
	log.Log(Logger::INFO, "new player %u, total players: %u", (unsigned)p.id,(unsigned)gameState.playerCnt);
}

void SimulatorBase::UpdateWaitForRespawn(uint32_t id, uint32_t doReset, size_t idx)
{
	if (doReset) {
		log.Log(Logger::DEBUG, "player %u: waitForRespwan was reset to true in the original dump", (unsigned)id);
		gameState.player[idx].waitForRespawn = 0;
	}
}

void SimulatorBase::Start()
{
	log.Log(Logger::INFO, "start");
	ClearInstrumentationPoints(instrp, INSTR_COUNT);
}

void SimulatorBase::DoBufferEnqueue(const PlayerSnapshotMessage& msg)
{
	log.Log(Logger::INFO, "BufferEnqueue for %u players", (unsigned)msg.snapshot.size());
	instrp[INSTR_ENQUEUE].count++;
}

void SimulatorBase::DoBufferUpdate(const UpdateCycle& updateInfo)
{

	log.Log(Logger::INFO, "BufferUpdate at %fs %fs", updateInfo.timestamp, updateInfo.m_InterpolationStartTime_before);
	instrp[INSTR_UPDATE].count++;
}

bool SimulatorBase::DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results)
{
	log.Log(Logger::INFO, "Interpolation at %fs original ping %d", interpolationInfo.timestamp, interpolationInfo.ping);
	instrp[INSTR_INTERPOLATE].count++;
	return false;
}

void SimulatorBase::ProcessResults(const InterpolationCycle& interpolationInfo, InterpolationResults& results)
{
	size_t i;
	for (i=0; i<results.playerCnt; i++) {
		bool isNew;
		ResultProcessorChannel *rpc = resultProcessor.GetChannel(results.player[i].id,registerID, isNew);
		if (isNew) {
			rpc->SetLogger(&log);
			rpc->SetName(fullName.c_str());
			rpc->StartStream(ip->GetOutputDir());
			log.Log(Logger::INFO,"created new result process channel '%s'", rpc->GetName());
		}
		rpc->Add(results.player[i]);
	}
}

const char * SimulatorBase::GetBaseName() const
{
	return "base";
}

const char * SimulatorBase::GetName() const
{
	return fullName.c_str();
}

void SimulatorBase::UpdateName()
{
	std::stringstream str;
	str << "sim" << registerID << "_" << GetBaseName();
	if (!nameSuffix.empty()) {
		str << "_" << nameSuffix;
	}
	cfg.GetShortCfg(str,true);
	fullName = str.str();
}

bool SimulatorBase::SetLogging(Logger::LogLevel l, const char *dir, bool enableStd)
{
	std::stringstream str;

	log.SetLogLevel(l);
	log.SetStdoutStderr(enableStd);
	str << fullName << ".log";
	return log.SetLogFile(str.str().c_str(), dir);
}

void SimulatorBase::SetSuffix(const char* suffix)
{
	if (suffix) {
		nameSuffix = std::string(suffix);
	} else {
		nameSuffix.clear();
	}
}

void SimulatorBase::ClearInstrumentationPoints(InstrumentationPoint* insts, size_t count)
{
	for (size_t i=0; i<count; i++) {
		insts[i].Clear();
	}
}

void SimulatorBase::DumpInstrumentationPoints(const InstrumentationPoint* insts, size_t count)
{
	for (size_t i=0; i<count; i++) {
		insts[i].ToLog(log);
	}
}

void SimulatorBase::Finish()
{
	log.Log(Logger::INFO, "finish");
	DumpInstrumentationPoints(instrp, INSTR_COUNT);
}

void SimulatorBase::Configure(const char *options)
{
	if (options) {
		cfg.Parse(options);
		UpdateName();
	}
}

}; // namespace OlmodPlayerDumpState 
