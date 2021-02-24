#ifndef OLMD_LOGGER_H
#define OLMD_LOGGER_H

#include <cstdio>
#include <cstdarg>

namespace OlmodPlayerDumpState {

class Logger {
	public:
		typedef enum {
			FATAL=0,
			ERROR,
			WARN,
			INFO,
			DEBUG
		} LogLevel;
	protected:
		std::FILE *file;
		std::FILE *copyWarnings;
		std::FILE *copyInfos;

		LogLevel level;

		bool Start(const char *filename);
		void Stop();

	public:
		Logger();
		~Logger();

		bool SetLogFile(const char *filename, const char *dir=".");
		void SetLogLevel(LogLevel l);
		void SetStdoutStderr(bool enabled=true);
		void Log(LogLevel l, const char *fmt, ...);
};

}; // namespace OlmodPlayerDumpState 


#endif // !OLMD_LOGGER_H
