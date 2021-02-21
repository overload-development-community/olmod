#include "interpreter.h"

namespace OlmodPlayerDumpState {

GameState::GameState() :
	m_InterpolationStartTime(-1.0f)
{}

Player& GameState::GetPlayer(uint32_t id)
{
	PlayerMap::iterator it = players.find(id);
	if (it == players.end()) {
		Player p;
		p.id = id;
		players[id] = p;
		return players[id];
	}
	return (it->second);
}

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
	char buf[16384];
	Stop();
	if (buf) {
		snprintf(buf, sizeof(buf), "%s/%s", dir, filename);
		return Start(buf);
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

SimulatorBase::SimulatorBase()
{}

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

const char * SimulatorBase::GetName() const
{
	return "base";
}

bool SimulatorBase::SetLogging(Logger::LogLevel l, const char *id, const char *dir, bool enableStd)
{
	char buf[16384];

	snprintf(buf, sizeof(buf), "sim_%s_%s.log", GetName(),id);
	log.SetLogLevel(l);
	log.SetStdoutStderr(enableStd);
	return log.SetLogFile(buf, dir);
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
	log.Log(Logger::DEBUG, "adding simulator");
	simulators.push_back(&simulator);
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
		(*it)->DoBufferEnqueue(currentSnapshots);
	}
}

void Interpreter::SimulateBufferUpdate()
{
	log.Log(Logger::INFO,"UPDATE");
	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		(*it)->DoBufferUpdate(update);
	}
}

void Interpreter::SimulateInterpolation()
{
	log.Log(Logger::INFO, "INTERPOLATE");
	for (SimulatorSet::iterator it=simulators.begin(); it!=simulators.end(); it++) {
		InterpolationResults results;
		bool res= (*it)->DoInterpolation(interpolation, results);
		if (res) {
			// take results and add them to the per-player log
		}
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
	if (!p.waitForRespawn && c.waitForRespawn_after) {
		// was enabled outside of the stuff we were inspecting...
		p.waitForRespawn = 1;
	}

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
	interpreter.GetLogger().SetLogFile("interpreter.log",dir);
	interpreter.GetLogger().SetLogLevel(OlmodPlayerDumpState::Logger::DEBUG);
	interpreter.GetLogger().SetStdoutStderr(false);
	interpreter.AddSimulator(s1);

	s1.SetLogging(OlmodPlayerDumpState::Logger::DEBUG, "s1", dir);
	int exit_code = !interpreter.ProcessFile((argc > 1)?argv[1]:"playerstatedump0.olmd");
	return exit_code;
}
