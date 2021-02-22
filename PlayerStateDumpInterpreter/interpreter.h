#ifndef OLMD_INTERPRETER_H
#define OLMD_INTERPRETER_H

#include "config.h"
#include "math_helper.h"

#include <string>
#include <map>
#include <vector>
#include <cstdio>
#include <cstdarg>

namespace OlmodPlayerDumpState {

const int MAX_PLAYERS = 32;

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

struct PlayerState {
	float pos[3];
	CQuaternion rot;
	float timestamp;

	void Invalidate()
	{
		pos[0] = 0.0f;
		pos[1] = 0.0f;
		pos[2] = 0.0f;

		rot.v[0] = 0.0f;
		rot.v[1] = 0.0f;
		rot.v[2] = 0.0f;
		rot.v[3] = 1.0f;

		timestamp = -1;
	}
};

struct PlayerSnapshot {
	uint32_t id;
	PlayerState state;

	void Copy(uint32_t pId, const PlayerState& pState) {
		id = pId;
		state = pState;
	}
};

struct PlayerSnapshotMessage {
	std::vector<PlayerSnapshot> snapshot;
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

struct Player {
	uint32_t id;
	float firstSeen;
	float lastSeen;
	uint32_t waitForRespawn;
	uint32_t waitForRespawnReset;
	PlayerState origState;
	PlayerState mostRecentState;
	void *data;
	
	Player() {
		Invalidate();
	}

	void Invalidate()
	{
		data=NULL;
		id = (uint32_t)-1;
		firstSeen = lastSeen = -1.0f;
		waitForRespawn=0;
		waitForRespawnReset=0;
		origState.Invalidate();
		mostRecentState.Invalidate();
	}
};

typedef std::map<uint32_t, Player> PlayerMap;

class Logger;
struct SimulatorGameState;
struct GameState {
	PlayerMap players;
	uint32_t playersCycle;
	float m_InterpolationStartTime;
	int ping;

	GameState();
	Player& GetPlayer(uint32_t id);
	const Player* FindPlayer(uint32_t id) const;
};

struct SimulatorGameState {
	Player player[MAX_PLAYERS];
	size_t playerCnt;
	uint32_t playersCycle;
	float m_InterpolationStartTime;

	SimulatorGameState();
};

struct InterpolationResults {
	PlayerSnapshot player[MAX_PLAYERS];
	size_t playerCnt;
};

class Logger {
	public:
		typedef enum {
			FATAL=0,
			ERROR,
			WARN,
			INFO,
			DEBUG
		} LogLevel;
	protected:
		std::FILE *file;
		std::FILE *copyWarnings;
		std::FILE *copyInfos;

		LogLevel level;

		bool Start(const char *filename);
		void Stop();

	public:
		Logger();
		~Logger();

		bool SetLogFile(const char *filename, const char *dir=".");
		void SetLogLevel(LogLevel l);
		void SetStdoutStderr(bool enabled=true);
		void Log(LogLevel l, const char *fmt, ...);
};

class Interpreter;
class ResultProcessor;

class ResultProcessorChannel {
	protected:
		ResultProcessor& resultProcessor;
		std::FILE *fStream;
		Logger* log;
		std::vector<PlayerState> data;
		std::string name;
		uint32_t objectId;
		uint32_t playerId;
		friend class ResultProcessor;
		friend class Interpreter;

		virtual void Clear();
		virtual void Finish();
		virtual void StreamOut(const PlayerState& s, size_t idx);

		ResultProcessorChannel(ResultProcessor& rp, uint32_t player, uint32_t object);
		virtual ~ResultProcessorChannel();
	
	public:
		virtual void Add(const PlayerState& s);
		void Add(const PlayerSnapshot& s);

		virtual void SetName(const char *n);
		virtual bool StartStream(const char *dir);
		virtual void StopStream();
		virtual void SetLogger(Logger* l) {log=l;}

		virtual const char *GetName() const {return name.c_str();}
};

class ResultProcessor {
	protected:
		typedef std::pair<uint32_t,uint32_t> channelID;
		typedef std::map<channelID,ResultProcessorChannel*> ChannelMap;
		ChannelMap channels;
		Config cfg;

		int dumpDeltaPos;

		friend class Interpreter;
		friend class ResultProcessorChannel;

		virtual ResultProcessorChannel* CreateChannel(channelID id);
		virtual void Clear();
		virtual void Finish();
	public:
		ResultProcessor();
		virtual ~ResultProcessor();

		void Configure(const char *options);
		virtual ResultProcessorChannel* GetChannel(uint32_t playerId, uint32_t objectId, bool& isNew);
};

struct InstrumentationPoint {
	uint64_t count;
	std::string name;

	InstrumentationPoint();
	void ToLog(Logger& log) const;
};

class SimulatorBase {
	private:
		typedef enum {
			INSTR_DMAX_SHENANIGANS=0,
			INSTR_ENQUEUE,
			INSTR_UPDATE,
			INSTR_INTERPOLATE,

			INSTR_COUNT
		} InstrumentationPointEnums;

		InstrumentationPoint instrp[INSTR_COUNT];

	protected:
		ResultProcessor& resultProcessor;
		Config cfg;

		std::string fullName;
		std::string nameSuffix;
		unsigned int registerID;

		const Interpreter* ip;
		SimulatorGameState gameState;
		std::vector<ResultProcessorChannel*> resultProcessors;
		Logger log;

		friend class Interpreter;

		void SyncGamestate(const GameState& gs);
		void UpdateWaitForRespawn(const GameState& gs);

		virtual void NewPlayer(Player& p, size_t idx);
		virtual void UpdateWaitForRespawn(uint32_t id, uint32_t doReset, size_t idx);

		virtual void DoBufferEnqueue(const PlayerSnapshotMessage& msg);
		virtual void DoBufferUpdate(const UpdateCycle& updateInfo);
		virtual bool DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results);
		virtual void ProcessResults(const InterpolationCycle& interpolationInfo, InterpolationResults& results);
		void DumpInstrumentationPoints(const InstrumentationPoint* insts, size_t count);
		virtual void Finish();

		virtual void UpdateName();
		virtual const char *GetName() const;
		virtual const char *GetBaseName() const;

	public:
		SimulatorBase(ResultProcessor& rp);
		virtual ~SimulatorBase();

		Logger& GetLogger() {return log;}
		bool SetLogging(Logger::LogLevel l=Logger::WARN, const char *dir=".", bool enableStd=false);
		void SetSuffix(const char* suffix = NULL);

		void Configure(const char *options);
};

typedef std::vector<SimulatorBase*> SimulatorSet;

class Interpreter {
	protected:
		Logger log;
		ResultProcessor& resultProcessor;
		GameState gameState;
		std::FILE *file;
		const char *fileName;
		const char *outputDir;
		bool process;
		PlayerSnapshotMessage currentSnapshots;
		UpdateCycle update;
		InterpolationCycle interpolation;
		SimulatorSet simulators;

		bool OpenFile(const char *filename);
		void CloseFile();

		int32_t ReadInt();
		uint32_t ReadUint();
		float ReadFloat();
		void ReadPlayerSnapshot(PlayerSnapshot& s);

	        void SimulateBufferEnqueue();
		void SimulateBufferUpdate();
		void SimulateInterpolation();

		void ProcessEnqueue();
		void ProcessUpdateBegin();
		void ProcessUpdateEnd();
		void ProcessInterpolateBegin();
		void ProcessInterpolateEnd();
		void ProcessLerpBegin();
		void ProcessLerpEnd();

		bool ProcessCommand();

	public:
		Interpreter(ResultProcessor& rp, const char *outputPath=".");
		~Interpreter();

		void AddSimulator(SimulatorBase& simulator);
		void DropSimulators();
		bool ProcessFile(const char *filename);

		Logger& GetLogger() {return log;};
		const GameState& GetGameState() const {return gameState;}
		const char *GetOutputDir() const {return outputDir;}

};	

}; // namespace OlmodPlayerDumpState 


#endif // !OLMD_INTERPRETER_H
