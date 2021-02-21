#ifndef OLMD_INTERPRETER_H
#define OLMD_INTERPRETER_H

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
	float rot[3];
	float timestamp;

	void Invalidate()
	{
		pos[0] = 0.0f;
		pos[1] = 0.0f;
		pos[2] = 0.0f;

		rot[0] = 0.0f;
		rot[1] = 0.0f;
		rot[2] = 0.0f;
		rot[3] = 0.0f;

		timestamp = -1;
	}
};

struct PlayerSnapshot {
	uint32_t id;
	PlayerState state;
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
class SimulatorBase {

	protected:
		std::string fullName;
		std::string nameSuffix;
		unsigned int registerID;

		const Interpreter* ip;
		SimulatorGameState gameState;
		Logger log;

		friend class Interpreter;

		void SyncGamestate(const GameState& gs);
		void UpdateWaitForRespawn(const GameState& gs);

		virtual void NewPlayer(Player& p, size_t idx);
		virtual void UpdateWaitForRespawn(uint32_t id, uint32_t doReset, size_t idx);

		virtual void DoBufferEnqueue(const PlayerSnapshotMessage& msg);
		virtual void DoBufferUpdate(const UpdateCycle& updateInfo);
		virtual bool DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results);

		virtual void UpdateName();
		virtual const char *GetName() const;
		virtual const char *GetBaseName() const;
	public:
		SimulatorBase();

		Logger& GetLogger() {return log;}
		bool SetLogging(Logger::LogLevel l=Logger::WARN, const char *dir=".", bool enableStd=false);
		void SetSuffix(const char* suffix = NULL);
};

typedef std::vector<SimulatorBase*> SimulatorSet;

class Interpreter {
	protected:
		Logger log;
		GameState gameState;
		std::FILE *file;
		const char *fileName;
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
		Interpreter();
		~Interpreter();

		void AddSimulator(SimulatorBase& simulator);
		void DropSimulators();
		bool ProcessFile(const char *filename);

		Logger& GetLogger() {return log;};
		const GameState& GetGameState() const {return gameState;}

};	

}; // namespace OlmodPlayerDumpState 


#endif // !OLMD_INTERPRETER_H
