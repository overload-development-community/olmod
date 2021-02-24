#ifndef OLMD_INSTRUMENTATION_H
#define OLMD_INSTRUMENTATION_H

#include <string>
#include <cstdint>

namespace OlmodPlayerDumpState {

class Logger;

struct InstrumentationPoint {
	uint64_t count;
	std::string name;

	InstrumentationPoint();
	void ToLog(Logger& log) const;
};

} // namespace OlmodPlayerDumpState

#endif // !OLMD_INSTRUMENTATION_H
