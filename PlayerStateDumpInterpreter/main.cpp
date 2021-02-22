#include "interpreter.h"
#include "simulator_original.h"
#include "simulator_36rc2.h"
#include "simulator_dh1.h"

#include <clocale>

int main(int argc, char **argv)
{
	const char *dir = (argc>2)?argv[2]:".";

	std::setlocale(LC_ALL,"C");
	OlmodPlayerDumpState::ResultProcessor rp;
	OlmodPlayerDumpState::Interpreter interpreter(rp, dir);

	rp.Configure("dumpDeltaPos=1;");

	interpreter.GetLogger().SetLogFile("interpreter.log",dir);
	interpreter.GetLogger().SetLogLevel(OlmodPlayerDumpState::Logger::DEBUG);
	interpreter.GetLogger().SetStdoutStderr(false);


	OlmodPlayerDumpState::Simulator::Original sOVL(rp);
	interpreter.AddSimulator(sOVL);
	sOVL.SetLogging(OlmodPlayerDumpState::Logger::DEBUG,  dir);

	OlmodPlayerDumpState::Simulator::Olmod36RC2 sOlmod36RC2(rp);
	sOlmod36RC2.Configure("max=1000;scale=100;ping=100;");
	interpreter.AddSimulator(sOlmod36RC2);
	sOlmod36RC2.SetLogging(OlmodPlayerDumpState::Logger::DEBUG,  dir);

	OlmodPlayerDumpState::Simulator::Derhass1 sDH1(rp);
	sDH1.Configure("max=0;scale=0;ping=0;");
	interpreter.AddSimulator(sDH1);
	sDH1.SetLogging(OlmodPlayerDumpState::Logger::DEBUG,  dir);

	OlmodPlayerDumpState::Simulator::Derhass1 sDH1b(rp);
	sDH1b.Configure("max=1000;scale=100;ping=133.6;");
	interpreter.AddSimulator(sDH1b);
	sDH1b.SetLogging(OlmodPlayerDumpState::Logger::DEBUG,  dir);

	OlmodPlayerDumpState::Simulator::Derhass1 sDH1c(rp);
	sDH1c.Configure("max=1000;scale=100;ping=34;");
	interpreter.AddSimulator(sDH1c);
	sDH1c.SetLogging(OlmodPlayerDumpState::Logger::DEBUG,  dir);

	int exit_code = !interpreter.ProcessFile((argc > 1)?argv[1]:"playerstatedump0.olmd");
	return exit_code;
}
