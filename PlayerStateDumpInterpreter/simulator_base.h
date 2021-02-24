#ifndef OLMD_SIM_BASE_H
#define OLMD_SIM_BASE_H

#include "config.h"
#include "dump_types.h"
#include "instrumentation.h"
#include "result_processor.h"
#include "player_types.h"

#include <string>
#include <vector>
#include <cstdint>

namespace OlmodPlayerDumpState {

struct GameState;
class Interpreter;

const size_t MAX_PLAYERS = 32;

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
		Logger log;

		friend class Interpreter;

		void SyncGamestate(const GameState& gs);
		void UpdateWaitForRespawn(const GameState& gs);

		virtual void NewPlayer(Player& p, size_t idx);
		virtual void UpdateWaitForRespawn(uint32_t id, uint32_t doReset, size_t idx);

		virtual void Start();
		virtual void DoBufferEnqueue(const PlayerSnapshotMessage& msg);
		virtual void DoBufferUpdate(const UpdateCycle& updateInfo);
		virtual bool DoInterpolation(const InterpolationCycle& interpolationInfo, InterpolationResults& results);
		virtual void ProcessResults(const InterpolationCycle& interpolationInfo, InterpolationResults& results);
		virtual void Finish();

		void ClearInstrumentationPoints(InstrumentationPoint* insts, size_t count);
		void DumpInstrumentationPoints(const InstrumentationPoint* insts, size_t count);

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
} // namespace OlmodPlayerDumpState

#endif // !OLMD_SIM_BASE_H
