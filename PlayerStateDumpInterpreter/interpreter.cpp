#include "interpreter.h"

#include <sstream>

namespace OlmodPlayerDumpState {

GameState::GameState() :
	playersCycle(0),
	m_InterpolationStartTime(-1.0f),
	ping(0)
{}

Player& GameState::GetPlayer(uint32_t id)
{
	PlayerMap::iterator it = players.find(id);
	if (it == players.end()) {
		playersCycle++;
		Player p;
		p.id = id;
		players[id] = p;
		return players[id];
	}
	return (it->second);
}

const Player* GameState::FindPlayer(uint32_t id) const
{
	PlayerMap::const_iterator it = players.find(id);
	if (it == players.cend()) {
		return NULL;
	}
	return &(it->second);
}

SimulatorGameState::SimulatorGameState() :
	playerCnt(0),
	playersCycle(0)
{}

Logger::Logger() :
	file(NULL),
	copyWarnings(NULL),
	copyInfos(NULL),
	level(WARN)
{}

Logger::~Logger()
{
	Stop();
}

bool Logger::Start(const char *filename)
{
	file = std::fopen(filename, "wt");
	return (file != NULL);
}

void Logger::Stop()
{
	if (file) {
		std::fclose(file);
		file=NULL;
	}
}

bool Logger::SetLogFile(const char *filename, const char *dir)
{

	Stop();
	if (filename) {
		std::stringstream str;
		if (dir) {
			str << dir << '/';
		}
		str << filename;
		return Start(str.str().c_str());
	}
	return true;
}

void Logger::SetLogLevel(LogLevel l)
{
	level = l;
}

void Logger::SetStdoutStderr(bool enabled)
{
	if (enabled) {
		copyInfos = stdout;
		copyWarnings = stderr;
	} else {
		copyInfos = NULL;
		copyWarnings = NULL;
	}
}

void Logger::Log(LogLevel l, const char *fmt, ...)
{
	if (l > level) {
		return;
	}

	std::va_list args;
	if (file) {
		va_start(args, fmt);
		std::vfprintf(file, fmt, args);
		std::fputc('\n', file);
		va_end(args);
	}
	if (l >= INFO && copyInfos) {
		va_start(args, fmt);
		std::vfprintf(copyInfos, fmt, args);
		std::fputc('\n', copyInfos);
		std::fflush(copyInfos);
		va_end(args);

	} else if (l <= WARN && copyWarnings) {
		va_start(args, fmt);
		std::vfprintf(copyWarnings, fmt, args);
		std::fputc('\n', copyWarnings);
		std::fflush(copyWarnings);
		va_end(args);
	}
}

SimulatorBase::SimulatorBase() :
	registerID(0),
	ip(NULL)
{
	UpdateName();
}

void SimulatorBase::SyncGamestate(const GameState& gs)
{
	if (gameState.playersCycle == gs.playersCycle && gameState.playerCnt == gs.players.size()) {
		return;
	}

	log.Log(Logger::DEBUG, "syncing player count from %u to %u, cycle %u to %u", (unsigned)gameState.playerCnt, (unsigned)gs.players.size(), (unsigned)gameState.playersCycle, (unsigned)gs.playersCycle);

	PlayerMap::const_iterator it;
	for (it=gs.players.cbegin(); it != gs.players.cend(); it++) {
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

void SimulatorBase::DoBufferEnqueue(const PlayerSnapshotMessage& msg)
{
	log.Log(Logger::INFO, "BufferEnqueue for %u players", (unsigned)msg.snapshot.size());
}

void SimulatorBase::DoBufferUpdate(const UpdateCycle& updateInfo)
{

	log.Log(Logger::INFO, "BufferUpdate at %fs %fs", updateInfo.timestamp, updateInfo.m_InterpolationStartTime_before);
}

bool SimulatorBase::DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results)
{
	log.Log(Logger::INFO, "Interpolation at %fs original ping %d", interpolationInfo.timestamp, interpolationInfo.ping);
	return false;
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

Interpreter::Interpreter() :
	file(NULL),
	fileName(NULL),
	process(false)
{
}

Interpreter::~Interpreter()
{
	CloseFile();
}

void Interpreter::AddSimulator(SimulatorBase& simulator)
{
	unsigned int id = (unsigned)simulators.size() + 1;
	simulators.push_back(&simulator);
	simulator.ip=this;
	simulator.registerID = id;
	simulator.UpdateName();
	log.Log(Logger::DEBUG, "added simulator '%s'", simulator.GetName());
}

void Interpreter::DropSimulators()
{
	log.Log(Logger::DEBUG, "dropping simulator");
	simulators.clear();
}

bool Interpreter::OpenFile(const char *filename)
{
	CloseFile();

	if (!filename) {
		log.Log(Logger::ERROR, "open: no file given");
		return false;
	}

	file = std::fopen(filename, "rb");
	if (!file) {
		log.Log(Logger::ERROR, "open: failed to open '%s'",filename);
		fileName = NULL;
		return false;
	}
	fileName = filename;
	log.Log(Logger::DEBUG, "open: opened '%s'",filename);

	uint32_t version = ReadUint();
	uint32_t hdrExtraSize = ReadUint();
	if (!file) {
		log.Log(Logger::ERROR, "open: failed to read header of '%s'",fileName);
		return false;
	}
	log.Log(Logger::DEBUG, "version: %u",(unsigned)version);

	if (version != 1) {
		log.Log(Logger::ERROR, "open: version of '%s' not supported: %u",fileName,(unsigned)version);
		return false;
	}
	if (hdrExtraSize) {
		if (!std::fseek(file, (long)hdrExtraSize, SEEK_CUR)) {
			log.Log(Logger::ERROR, "open: ifailed to skip extra header of '%s'",fileName);
			CloseFile();
			return false;
		}
	}

	log.Log(Logger::DEBUG, "open: successfully opened '%s'",fileName);
	return true;
}

int32_t Interpreter::ReadInt()
{
	int32_t value = 0;
	if (file) {
		if (std::fread(&value, sizeof(value), 1, file) != 1) {
			log.Log(Logger::WARN, "failed to read int");
			value = 0;
			CloseFile();
		}
	}
	return value;
}

uint32_t Interpreter::ReadUint()
{
	uint32_t value = 0;
	if (file) {
		if (std::fread(&value, sizeof(value), 1, file) != 1) {
			log.Log(Logger::WARN, "failed to read uint");
			value = 0;
			CloseFile();
		}
	}
	return value;
}

float Interpreter::ReadFloat()
{
	float value = 0.0f;
	if (file) {
		if (std::fread(&value, sizeof(value), 1, file) != 1) {
			log.Log(Logger::WARN, "failed to read float");
			value = 0.0f;
			CloseFile();
		}
	}
	return value;
}

void Interpreter::ReadPlayerSnapshot(PlayerSnapshot& s)
{
	s.id = ReadUint();
	s.state.pos[0] = ReadFloat();
	s.state.pos[1] = ReadFloat();
	s.state.pos[2] = ReadFloat();
	s.state.rot[0] = ReadFloat();
	s.state.rot[1] = ReadFloat();
	s.state.rot[2] = ReadFloat();
	s.state.rot[3] = ReadFloat();
	if (!file) {
		log.Log(Logger::WARN, "failed to read PlayerSnapshot");
	}
}

void Interpreter::SimulateBufferEnqueue()
{
	log.Log(Logger::INFO, "ENQUEUE: for %u players", (unsigned)currentSnapshots.snapshot.size());
	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		SimulatorBase *sim = (*it);
		sim->SyncGamestate(gameState);
		sim->DoBufferEnqueue(currentSnapshots);
	}
}

void Interpreter::SimulateBufferUpdate()
{
	log.Log(Logger::INFO,"UPDATE");
	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		SimulatorBase *sim = (*it);
		sim->SyncGamestate(gameState);
		sim->DoBufferUpdate(update);
	}
}

void Interpreter::SimulateInterpolation()
{
	log.Log(Logger::INFO, "INTERPOLATE");
	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		SimulatorBase *sim = (*it);
		sim->SyncGamestate(gameState);
		sim->UpdateWaitForRespawn(gameState);
		InterpolationResults results;
		results.playerCnt = 0;
		bool res = sim->DoInterpolation(interpolation, results);
		if (res && results.playerCnt) {
			// take results and add them to the per-player log
		}
	}
	size_t i;
	for (i=0;i<interpolation.lerps.size();i++) {
		const LerpCycle& l=interpolation.lerps[i];
		Player& p=gameState.GetPlayer(l.A.id);
		p.waitForRespawn = l.waitForRespawn_after;
		p.waitForRespawnReset = 0;
	}
}

void Interpreter::ProcessEnqueue()
{
	float ts = ReadFloat();
	uint32_t i, num = ReadUint();
	log.Log(Logger::DEBUG, "got ENQUEUE at %fs for %u players", ts, (unsigned)num);
	currentSnapshots.snapshot.resize(num);
	for (i=0; i<num; i++) {
		ReadPlayerSnapshot(currentSnapshots.snapshot[i]);
		currentSnapshots.snapshot[i].state.timestamp = ts;
		if (file) {
			Player& p = gameState.GetPlayer(currentSnapshots.snapshot[i].id);
			p.mostRecentState = currentSnapshots.snapshot[i].state;
			if (p.firstSeen < 0.0f) {
				p.firstSeen = ts;
			}
			p.lastSeen = ts;
		}
	}
	SimulateBufferEnqueue();
}

void Interpreter::ProcessUpdateBegin() 
{
	update.valid = true;
	update.timestamp =  ReadFloat();
	update.m_InterpolationStartTime_before = ReadFloat();
	log.Log(Logger::DEBUG, "got UPDATE_BEGIN at %fs %fs", update.timestamp, update.m_InterpolationStartTime_before);
}

void Interpreter::ProcessUpdateEnd()
{
	update.m_InterpolationStartTime_after = ReadFloat();

	log.Log(Logger::DEBUG, "got UPDATE_END %fs", update.m_InterpolationStartTime_after);
	if (update.valid) {
		SimulateBufferUpdate();
	}
	update.valid = false;
}

void Interpreter::ProcessInterpolateBegin()
{
	interpolation.valid=true;
	interpolation.timestamp = ReadFloat();
	interpolation.ping = ReadInt();
	gameState.ping = interpolation.ping;
	log.Log(Logger::DEBUG, "got INTERPOLATE_BEGIN at %fs ping %d", interpolation.timestamp, interpolation.ping);
	interpolation.lerps.clear();
}

void Interpreter::ProcessInterpolateEnd()
{
	log.Log(Logger::DEBUG, "got INTERPOLATE_END");
	if (interpolation.valid) {
		SimulateInterpolation();
	}
	interpolation.valid=false;
}

void Interpreter::ProcessLerpBegin()
{
	LerpCycle c;
	c.waitForRespawn_before = ReadUint();
	ReadPlayerSnapshot(c.A);
	ReadPlayerSnapshot(c.B);
	c.A.state.timestamp = -1.0f; /// we do not know
	c.B.state.timestamp = -1.0f; /// we do not know
	c.t=ReadFloat();
	log.Log(Logger::DEBUG, "got LERP_BEGIN for player %u waitForRespwan=%u t=%f",c.A.id,c.waitForRespawn_before,c.t);
	interpolation.lerps.push_back(c);
	Player &p=gameState.GetPlayer(c.A.id);
	if (!p.waitForRespawn && c.waitForRespawn_before) {
		// was enabled outside of the stuff we were inspecting...
		log.Log(Logger::DEBUG,"player %u: waitForRespawn war reset to 1",(unsigned)p.id);
		p.waitForRespawnReset = 1;
	} else {
		p.waitForRespawnReset = 0;
	}
	p.waitForRespawn=c.waitForRespawn_before;
}

void Interpreter::ProcessLerpEnd()
{
	uint32_t waitForRespawn = ReadUint();
	if (interpolation.lerps.size() < 1) {
		log.Log(Logger::WARN, "LERP_END without lerp begin???");
		return;
	}
	LerpCycle &c = interpolation.lerps[interpolation.lerps.size()-1];
	c.waitForRespawn_after = waitForRespawn;
	log.Log(Logger::DEBUG, "got LERP_END for player %u waitForRespwan=%u",c.A.id,c.waitForRespawn_after);
	Player &p=gameState.GetPlayer(c.A.id);

}

bool Interpreter::ProcessCommand()
{
	if (!file || feof(file) || ferror(file)) {
		return false;
	}

	Command cmd = (Command)ReadUint();
	log.Log(Logger::DEBUG, "got command 0x%x", (unsigned)cmd);
	switch(cmd) {
		case ENQUEUE:
			ProcessEnqueue();
			break;
		case UPDATE_BEGIN:
			ProcessUpdateBegin();
			break;
		case UPDATE_END:
			ProcessUpdateEnd();
			break;
		case INTERPOLATE_BEGIN:
			ProcessInterpolateBegin();
			break;
		case INTERPOLATE_END:
			ProcessInterpolateEnd();
			break;
		case LERP_BEGIN:
			ProcessLerpBegin();
			break;
		case LERP_END:
			ProcessLerpEnd();
			break;
		case FINISH:
			log.Log(Logger::DEBUG, "got FINISH");
			process = false;
			return true;
		default:
			if (file) {
				log.Log(Logger::ERROR, "INVALID COMMAND 0x%x", (unsigned)cmd);
			}
			CloseFile(); 
	}

	return (file != NULL);
}

void Interpreter::CloseFile()
{
	if (file) {
		if (process) {
			log.Log(Logger::WARN, "read error or premature end of file");
		}
		std::fclose(file);
		file = NULL;
		log.Log(Logger::INFO, "closed '%f'", fileName);
	}
	fileName = NULL;
}

bool Interpreter::ProcessFile(const char *filename)
{

	if (!OpenFile(filename)) {
		return false;
	}

	process = true;
	// process the file until end or error is reached
	while (process && ProcessCommand());

	CloseFile();
	return true;
}


}; // namespace OlmodPlayerDumpState 


int main(int argc, char **argv)
{
	const char *dir = (argc>2)?argv[2]:".";

	OlmodPlayerDumpState::SimulatorBase s1;
	OlmodPlayerDumpState::Interpreter interpreter;

	s1.SetSuffix("test1");

	interpreter.GetLogger().SetLogFile("interpreter.log",dir);
	interpreter.GetLogger().SetLogLevel(OlmodPlayerDumpState::Logger::DEBUG);
	interpreter.GetLogger().SetStdoutStderr(false);
	interpreter.AddSimulator(s1);

	s1.SetLogging(OlmodPlayerDumpState::Logger::DEBUG,  dir);
	int exit_code = !interpreter.ProcessFile((argc > 1)?argv[1]:"playerstatedump0.olmd");
	return exit_code;
}
