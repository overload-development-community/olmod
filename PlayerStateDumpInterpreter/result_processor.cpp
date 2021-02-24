#include "result_processor.h"

#include <sstream>

namespace OlmodPlayerDumpState {

ResultProcessorChannelBase::ResultProcessorChannelBase(ResultProcessor& rp, uint32_t player, uint32_t object) :
	resultProcessor(rp),
	fStream(NULL),
	log(NULL),
	objectId(object),
	playerId(player)
{
	SetName("unknown");
}

ResultProcessorChannelBase::~ResultProcessorChannelBase()
{
	StopStream();
}

void ResultProcessorChannelBase::Clear()
{
	if (log) {
		log->Log(Logger::INFO,"rpc %s: Clear", name.c_str());
	}
}

void ResultProcessorChannelBase::Finish()
{
	if (log) {
		log->Log(Logger::INFO,"rpc %s: Finish", name.c_str());
	}
	StopStream();
}

void ResultProcessorChannelBase::PrepareName(std::stringstream& str, const char *n)
{
	str << "res_o" << objectId << "_p" << playerId;

	if (n) {
		str << "_" << n;
	}
}

void ResultProcessorChannelBase::SetName(const char *n)
{
	std::stringstream str;
	PrepareName(str, n);
	name=str.str();
	if (log) {
		log->Log(Logger::INFO,"rpc: my name is '%s'", name.c_str());
	}
}

bool ResultProcessorChannelBase::StartStream(const char *dir)
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
			log->Log(Logger::INFO,"rpc %s: streaming to '%s'", name.c_str(), str.str().c_str());
		} else {
			log->Log(Logger::WARN,"rpc $s: failed to stream to '%s'", name.c_str(),str.str().c_str());
		}
	}
	return (fStream != NULL);
}

void ResultProcessorChannelBase::StopStream()
{
	if (fStream) {
		if (log) {
			log->Log(Logger::INFO, "rpc %s: stream stopped",name.c_str());
		}
		std::fclose(fStream);
		fStream = NULL;
	}
}

ResultProcessorChannel::ResultProcessorChannel(ResultProcessor& rp, uint32_t player, uint32_t object) :
	ResultProcessorChannelBase(rp, player, object)
{
}

ResultProcessorChannel::~ResultProcessorChannel()
{
}

void ResultProcessorChannel::Clear()
{
	ResultProcessorChannelBase::Clear();
	data.clear();
}

void ResultProcessorChannel::Finish()
{
	ResultProcessorChannelBase::Finish();
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
	/*
	if (log) {
		log->Log(Logger::DEBUG, "rpc %u %u: adding data point at timestamp %fs", (unsigned)objectId, (unsigned)playerId, s.timestamp);
	}
	*/
	data.push_back(s);
	if (fStream) {
		StreamOut(s, data.size()-1);
	}
}

void ResultProcessorChannel::Add(const PlayerSnapshot& s)
{
	if (s.id != playerId) {
		if (log) {
			log->Log(Logger::WARN, "rpc %u %u: adding data point for wrong player %u", (unsigned)objectId, (unsigned)playerId, (unsigned)s.id);
		}
	}
	Add(s.state);
}

ResultProcessorAuxChannel::ResultProcessorAuxChannel(ResultProcessor& rp, uint32_t player, uint32_t object, uint32_t aux) :
	ResultProcessorChannelBase(rp, player, object),
	auxId(aux)
{
}

ResultProcessorAuxChannel::~ResultProcessorAuxChannel()
{
}

void ResultProcessorAuxChannel::Clear()
{
	ResultProcessorChannelBase::Clear();
	currentData.clear();
}

void ResultProcessorAuxChannel::Finish()
{
	FlushCurrent();
	ResultProcessorChannelBase::Finish();
}

void ResultProcessorAuxChannel::StreamOut()
{
	for (size_t i=0; i<currentData.size(); i++) {
		fprintf(fStream, (i>0)?"\t%f":"%f", currentData[i]);
	}
	fputc('\n',fStream);
}

void ResultProcessorAuxChannel::SetName(const char *n)
{
	std::stringstream str;
	PrepareName(str, n);
	str << "_aux" << auxId;
	name=str.str();
	if (log) {
		log->Log(Logger::INFO,"rpcAux %u %u %u: my name is '%s'", (unsigned)objectId, (unsigned)playerId, (unsigned)auxId, name.c_str());
	}
}

void ResultProcessorAuxChannel::Add(float value)
{
	currentData.push_back(value);
}

void ResultProcessorAuxChannel::Add(const float* values, size_t size)
{
	for (size_t i=0; i<size; i++) {
		Add(values[i]);
	}
}

void ResultProcessorAuxChannel::Add(const PlayerState& s)
{
	Add(s.timestamp);
	Add(s.pos, 3);
	Add(s.rot.v,4);
}

void ResultProcessorAuxChannel::FlushCurrent()
{
	if (currentData.size() > 0) {
		StreamOut();
		currentData.clear();
	}
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

	for (AuxChannelMap::iterator it=auxChannels.begin(); it !=auxChannels.end(); it++) {
		if (it->second) {
			delete it->second;
		}
	}
	auxChannels.clear();
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

ResultProcessorAuxChannel* ResultProcessor::CreateAuxChannel(auxChannelID id)
{
	return new ResultProcessorAuxChannel(*this, id.first.first, id.first.second, id.second);
}

void ResultProcessor::Clear()
{
	for (ChannelMap::iterator it=channels.begin(); it !=channels.end(); it++) {
		if (it->second) {
			it->second->Clear();
		}
	}

	for (AuxChannelMap::iterator it=auxChannels.begin(); it !=auxChannels.end(); it++) {
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

	for (AuxChannelMap::iterator it=auxChannels.begin(); it !=auxChannels.end(); it++) {
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

ResultProcessorAuxChannel* ResultProcessor::GetAuxChannel(uint32_t playerId, uint32_t objectId, uint32_t auxId, bool& isNew)
{
	auxChannelID id(std::make_pair(playerId,objectId),auxId);
	AuxChannelMap::iterator it=auxChannels.find(id);
	if (it == auxChannels.end()) {
		ResultProcessorAuxChannel *ch=CreateAuxChannel(id);
		auxChannels[id]=ch;
		isNew = true;
		return ch;
	}
	isNew = false;
	return it->second;
}


}; // namespace OlmodPlayerDumpState 
