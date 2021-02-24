#include "instrumentation.h"
#include "logger.h"

namespace OlmodPlayerDumpState {

InstrumentationPoint::InstrumentationPoint() :
	count(0)
{
}

void InstrumentationPoint::ToLog(Logger& log) const
{
	log.Log(Logger::INFO, "instrumentation point '%s': %llu times", name.c_str(), (unsigned long long)count);
}

void InstrumentationPoint::Clear()
{
	count=0;
}

} // namespace OlmodPlayerDumpState
