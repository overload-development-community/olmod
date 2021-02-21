#include "interpreter.h"
#include "simulator_original.h"

#include <clocale>

int main(int argc, char **argv)
{
	const char *dir = (argc>2)?argv[2]:".";

	std::setlocale(LC_ALL,"C");
	OlmodPlayerDumpState::ResultProcessor rp;
	OlmodPlayerDumpState::Simulator::Original s1(rp);
	OlmodPlayerDumpState::Interpreter interpreter(rp, dir);

	s1.SetSuffix("test1");

	interpreter.GetLogger().SetLogFile("interpreter.log",dir);
	interpreter.GetLogger().SetLogLevel(OlmodPlayerDumpState::Logger::DEBUG);
	interpreter.GetLogger().SetStdoutStderr(false);
	interpreter.AddSimulator(s1);

	s1.SetLogging(OlmodPlayerDumpState::Logger::DEBUG,  dir);
	int exit_code = !interpreter.ProcessFile((argc > 1)?argv[1]:"playerstatedump0.olmd");
	return exit_code;
}
