#include "logger.h"

#include <string>
#include <sstream>

namespace OlmodPlayerDumpState {

Logger::Logger() :
	file(NULL),
	copyWarnings(NULL),
	copyInfos(NULL),
	level(WARN)
{}

Logger::~Logger()
{
	Stop();
}

bool Logger::Start(const char *filename)
{
	file = std::fopen(filename, "wt");
	return (file != NULL);
}

void Logger::Stop()
{
	if (file) {
		std::fclose(file);
		file=NULL;
	}
}

bool Logger::SetLogFile(const char *filename, const char *dir)
{

	Stop();
	if (filename) {
		std::stringstream str;
		if (dir) {
			str << dir << '/';
		}
		str << filename;
		return Start(str.str().c_str());
	}
	return true;
}

void Logger::SetLogLevel(LogLevel l)
{
	level = l;
}

void Logger::SetStdoutStderr(bool enabled)
{
	if (enabled) {
		copyInfos = stdout;
		copyWarnings = stderr;
	} else {
		copyInfos = NULL;
		copyWarnings = NULL;
	}
}

void Logger::Log(LogLevel l, const char *fmt, ...)
{
	if (l > level) {
		return;
	}

	std::va_list args;
	if (file) {
		va_start(args, fmt);
		std::vfprintf(file, fmt, args);
		std::fputc('\n', file);
		std::fflush(file);
		va_end(args);
	}
	if (l >= INFO && copyInfos) {
		va_start(args, fmt);
		std::vfprintf(copyInfos, fmt, args);
		std::fputc('\n', copyInfos);
		std::fflush(copyInfos);
		va_end(args);

	} else if (l <= WARN && copyWarnings) {
		va_start(args, fmt);
		std::vfprintf(copyWarnings, fmt, args);
		std::fputc('\n', copyWarnings);
		std::fflush(copyWarnings);
		va_end(args);
	}
}

}; // namespace OlmodPlayerDumpState 
