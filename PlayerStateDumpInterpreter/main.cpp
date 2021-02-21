#include "interpreter.h"
#include "simulator_original.h"
#include "simulator_36rc2.h"

#include <clocale>

int main(int argc, char **argv)
{
	const char *dir = (argc>2)?argv[2]:".";

	std::setlocale(LC_ALL,"C");
	OlmodPlayerDumpState::ResultProcessor rp;
	OlmodPlayerDumpState::Simulator::Original sOVL(rp);
	OlmodPlayerDumpState::Simulator::Olmod36RC2 sOlmod36RC2(rp);
	OlmodPlayerDumpState::Interpreter interpreter(rp, dir);

	sOVL.SetSuffix("vanilla");

	interpreter.GetLogger().SetLogFile("interpreter.log",dir);
	interpreter.GetLogger().SetLogLevel(OlmodPlayerDumpState::Logger::DEBUG);
	interpreter.GetLogger().SetStdoutStderr(false);
	interpreter.AddSimulator(sOVL);
	interpreter.AddSimulator(sOlmod36RC2);

	sOVL.SetLogging(OlmodPlayerDumpState::Logger::DEBUG,  dir);
	sOlmod36RC2.SetLogging(OlmodPlayerDumpState::Logger::DEBUG,  dir);
	int exit_code = !interpreter.ProcessFile((argc > 1)?argv[1]:"playerstatedump0.olmd");
	return exit_code;
}
