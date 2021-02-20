#ifndef OLMD_INTERPRETER_H
#define OLMD_INTERPRETER_H

#include <string>
#include <map>
#include <vector>
#include <cstdio>


namespace OlmodPlayerDumpState {

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
	PlayerState origState;
	PlayerState mostRecentState;
	
	Player() {
		id = (uint32_t)-1;
		firstSeen = lastSeen = -1.0f;
		waitForRespawn=0;
		origState.Invalidate();
		mostRecentState.Invalidate();
	}
};

typedef std::map<uint32_t, Player> PlayerMap;

struct GameState {
	PlayerMap players;
	float m_InterpolationStartTime;

	GameState();
	Player& GetPlayer(uint32_t id);
};

class InterpolationResults {
	std::vector<PlayerSnapshot> players;
};

class SimulatorBase {
	
	public:
		SimulatorBase();

		virtual void DoBufferEnqueue(const PlayerSnapshotMessage& msg, GameState& gameState);
		virtual void DoBufferUpdate(const UpdateCycle& upateInfo, GameState& gameState);
		virtual bool DoInterpolation(const InterpolationCycle& interpolationInfo, GameState& gameState, InterpolationResults& results);
};

typedef std::vector<SimulatorBase*> SimulatorSet;

class Interpreter {
	protected:
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

};	

}; // namespace OlmodPlayerDumpState 


#endif // !OLMD_INTERPRETER_H
