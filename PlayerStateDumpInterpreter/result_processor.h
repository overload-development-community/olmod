#ifndef OLMD_RESULT_PROCESSOR_H
#define OLMD_RESULT_PROCESSOR_H

#include "config.h"
#include "logger.h"
#include "player_types.h"

#include <string>
#include <map>
#include <vector>
#include <sstream>
#include <cstdio>
#include <cstdint>

namespace OlmodPlayerDumpState {

class ResultProcessor;

class ResultProcessorChannelBase {
	protected:
		ResultProcessor& resultProcessor;
		std::FILE *fStream;
		Logger* log;
		std::string name;
		uint32_t objectId;
		uint32_t playerId;
		friend class ResultProcessor;
		friend class Interpreter;

		virtual void Clear();
		virtual void Finish();

		virtual void PrepareName(std::stringstream& str, const char *n);

		ResultProcessorChannelBase(ResultProcessor& rp, uint32_t player, uint32_t object);
		virtual ~ResultProcessorChannelBase();
	
	public:
		virtual void SetName(const char *n);
		virtual bool StartStream(const char *dir);
		virtual void StopStream();
		virtual void SetLogger(Logger* l) {log=l;}

		virtual const char *GetName() const {return name.c_str();}
};

class ResultProcessorChannel : public ResultProcessorChannelBase {
	protected:
		std::vector<PlayerState> data;
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
};

class ResultProcessorAuxChannel : public ResultProcessorChannelBase {
	protected:
		uint32_t auxId;
		std::vector<float> currentData;
		friend class ResultProcessor;
		friend class Interpreter;

		virtual void Clear();
		virtual void Finish();
		virtual void StreamOut();

		ResultProcessorAuxChannel(ResultProcessor& rp, uint32_t player, uint32_t object, uint32_t aux);
		virtual ~ResultProcessorAuxChannel();
	
	public:
		virtual void SetName(const char *n);
		virtual void Add(float value);
		virtual void Add(const float* values, size_t size);
		virtual void Add(const PlayerState& s);
		virtual void FlushCurrent();
};

class ResultProcessor {
	protected:
		typedef std::pair<uint32_t,uint32_t> channelID;
		typedef std::pair<channelID,uint32_t> auxChannelID;
		typedef std::map<channelID,ResultProcessorChannel*> ChannelMap;
		typedef std::map<auxChannelID,ResultProcessorAuxChannel*> AuxChannelMap;
		ChannelMap channels;
		AuxChannelMap auxChannels;
		Config cfg;

		int dumpDeltaPos;

		friend class Interpreter;
		friend class ResultProcessorChannel;

		virtual ResultProcessorChannel* CreateChannel(channelID id);
		virtual ResultProcessorAuxChannel* CreateAuxChannel(auxChannelID id);
		virtual void Clear();
		virtual void Finish();
	public:
		ResultProcessor();
		virtual ~ResultProcessor();

		void Configure(const char *options);
		virtual ResultProcessorChannel* GetChannel(uint32_t playerId, uint32_t objectId, bool& isNew);
		virtual ResultProcessorAuxChannel* GetAuxChannel(uint32_t playerId, uint32_t objectId, uint32_t auxId, bool& isNew);
};

}; // namespace OlmodPlayerDumpState 

#endif // !OLMD_RESULT_PROCESSOR_H
