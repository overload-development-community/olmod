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

	COMMAND_END_MARKER
};

struct PlayerState {
	float pos[3];
	float rot[3];
	float timestamp;
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
	int ping;

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
	std::vector<LerpCycle> lerps;

	InterpolationCycle() :
		valid(false)
	{}
};

class Player {
	protected:
		uint32_t id;
		PlayerState origState;
	
	public:

};

class SimulatorBase {
	
	public:
		virtual void DoBufferEnqueue(const PlayerSnapshotMessage& msg);
		virtual void DoBufferUpdate(const UpdateCycle& upateInfo);
		virtual void DoInterpolation(const InterpolationCycle& interpolationInfo);
};

class Interpreter {
	protected:
		std::map<uint32_t, PlayerState> players;
		std::FILE *file;
		const char *fileName;
		PlayerSnapshotMessage currentSnapshots;
		UpdateCycle update;
		InterpolationCycle interpolation;

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

		bool ProcessFile(const char *filename);

};	

}; // namespace OlmodPlayerDumpState 


#endif // !OLMD_INTERPRETER_H
