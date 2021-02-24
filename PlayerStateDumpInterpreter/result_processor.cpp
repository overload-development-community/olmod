#include "result_processor.h"

#include <sstream>

namespace OlmodPlayerDumpState {

ResultProcessorChannel::ResultProcessorChannel(ResultProcessor& rp, uint32_t player, uint32_t object) :
	resultProcessor(rp),
	fStream(NULL),
	log(NULL),
	objectId(object),
	playerId(player)
{
	SetName("unknown");
}

ResultProcessorChannel::~ResultProcessorChannel()
{
	StopStream();
}

void ResultProcessorChannel::Clear()
{
	if (log) {
		log->Log(Logger::INFO,"rpc: Clear");
	}
	data.clear();
}

void ResultProcessorChannel::Finish()
{
	if (log) {
		log->Log(Logger::INFO,"rpc: Finish");
	}
	StopStream();
}

void ResultProcessorChannel::SetName(const char *n)
{
	std::stringstream str;

	str << "res_o" << objectId << "_p" << playerId;

	if (n) {
		str << "_" << n;
	}
	name=str.str();
	if (log) {
		log->Log(Logger::INFO,"rpc: my name is '%s'", name.c_str());
	}
}

bool ResultProcessorChannel::StartStream(const char *dir)
{
	std::stringstream str;

	if (fStream) {
		StopStream();
	}

	if (dir) {
		str << dir << '/';
	}
	str << name << ".csv";
	fStream = std::fopen(str.str().c_str(), "wt");
	if (log) {
		if (fStream) {
			log->Log(Logger::INFO,"rpc: streaming to '%s'", name.c_str());
		} else {
			log->Log(Logger::WARN,"rpc: failed to stream to '%s'", name.c_str());
		}
	}
	return (fStream != NULL);
}

void ResultProcessorChannel::StopStream()
{
	if (fStream) {
		if (log) {
			log->Log(Logger::INFO, "rpc: stream stopped");
		}
		std::fclose(fStream);
		fStream = NULL;
	}
}

void ResultProcessorChannel::StreamOut(const PlayerState& s, size_t idx)
{
	float yawPitchRoll[3];

	s.rot.ToEuler(yawPitchRoll);
	fprintf(fStream, "%f\t%f\t%f\t%f\t%f\t%f\t%f",
			s.timestamp,
			s.pos[0],
			s.pos[1],
			s.pos[2],
			yawPitchRoll[0],
			yawPitchRoll[1],
			yawPitchRoll[2]);
	if (resultProcessor.dumpDeltaPos && idx != (size_t)-1 && idx > 0) {
		const PlayerState b=data[idx-1];
		fprintf(fStream, "\t%f",s.timestamp-b.timestamp);
		for (int k=0; k<3; k++) {
			fprintf(fStream, "\t%f",s.pos[k]-b.pos[k]);
			
		}
	}
	fputc('\n',fStream);
}

void ResultProcessorChannel::Add(const PlayerState& s)
{
	if (log) {
		log->Log(Logger::DEBUG, "rpc: adding data point at timestamp %fs", s.timestamp);
	}
	data.push_back(s);
	if (fStream) {
		StreamOut(s, data.size()-1);
	}
}

void ResultProcessorChannel::Add(const PlayerSnapshot& s)
{
	if (s.id != playerId) {
		if (log) {
			log->Log(Logger::WARN, "rpc: adding data point for wrong player %u, exptected %u", (unsigned)s.id,(unsigned)playerId);
		}
	}
	Add(s.state);
}

ResultProcessor::ResultProcessor() :
	dumpDeltaPos(0)
{
	cfg.Add(ConfigParam(dumpDeltaPos,"dumpDeltaPos"));
}

ResultProcessor::~ResultProcessor()
{
	for (ChannelMap::iterator it=channels.begin(); it !=channels.end(); it++) {
		if (it->second) {
			delete it->second;
		}
	}
	channels.clear();
}

void ResultProcessor::Configure(const char *options)
{
	if (options) {
		cfg.Parse(options);
	}
}

ResultProcessorChannel* ResultProcessor::CreateChannel(channelID id)
{
	return new ResultProcessorChannel(*this, id.first, id.second);
}

void ResultProcessor::Clear()
{
	for (ChannelMap::iterator it=channels.begin(); it !=channels.end(); it++) {
		if (it->second) {
			it->second->Clear();
		}
	}
}

void ResultProcessor::Finish()
{
	for (ChannelMap::iterator it=channels.begin(); it !=channels.end(); it++) {
		if (it->second) {
			it->second->Finish();
		}
	}
}

ResultProcessorChannel* ResultProcessor::GetChannel(uint32_t playerId, uint32_t objectId, bool& isNew)
{
	channelID id(playerId,objectId);
	ChannelMap::iterator it=channels.find(id);
	if (it == channels.end()) {
		ResultProcessorChannel *ch=CreateChannel(id);
		channels[id]=ch;
		isNew = true;
		return ch;
	}
	isNew = false;
	return it->second;
}

}; // namespace OlmodPlayerDumpState 
