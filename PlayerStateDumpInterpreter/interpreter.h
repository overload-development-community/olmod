#ifndef OLMD_INTERPRETER_H
#define OLMD_INTERPRETER_H

#include "config.h"
#include "dump_types.h"
#include "logger.h"
#include "math_helper.h"
#include "player_types.h"
#include "simulator_base.h"

#include <string>
#include <map>
#include <vector>
#include <cstdio>
#include <cstdint>

namespace OlmodPlayerDumpState {

class ResultProcessor;

struct GameState {
	PlayerMap players;
	uint32_t playersCycle;
	float m_InterpolationStartTime;
	int ping;

	GameState();
	Player& GetPlayer(uint32_t id);
	const Player* FindPlayer(uint32_t id) const;
};

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
