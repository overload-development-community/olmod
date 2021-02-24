#ifndef OLMD_RESULT_PROCESSOR_H
#define OLMD_RESULT_PROCESSOR_H

#include "config.h"
#include "logger.h"
#include "player_types.h"

#include <string>
#include <map>
#include <vector>
#include <cstdio>
#include <cstdint>

namespace OlmodPlayerDumpState {

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

}; // namespace OlmodPlayerDumpState 

#endif // !OLMD_RESULT_PROCESSOR_H
