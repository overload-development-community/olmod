#include "interpreter.h"

namespace OlmodPlayerDumpState {

Interpreter::Interpreter() :
	file(NULL),
	fileName(NULL)
{
}

Interpreter::~Interpreter()
{
	CloseFile();
}

bool Interpreter::OpenFile(const char *filename)
{
	CloseFile();

	if (!filename) {
		return false;
	}

	file = std::fopen(filename, "rb");
	if (!file) {
		fileName = NULL;
		return false;
	}
	fileName = filename;

	uint32_t version = ReadUint();
	uint32_t hdrExtraSize = ReadUint();
	if (!file) {
		return false;
	}

	if (version != 1) {
		// TODO:
		return false;
	}
	if (hdrExtraSize) {
		if (!std::fseek(file, (long)hdrExtraSize, SEEK_CUR)) {
			CloseFile();
			return false;
		}
	}

	return true;
}

uint32_t Interpreter::ReadUint()
{
	uint32_t value = 0;
	if (file) {
		if (std::fread(&value, sizeof(value), 1, file) != 1) {
			value = 0;
			CloseFile();
		}
	}
	return value;
}

float Interpreter::ReadFloat()
{
	float value = 0.0f;
	if (file) {
		if (std::fread(&value, sizeof(value), 1, file) != 1) {
			value = 0.0f;
			CloseFile();
		}
	}
	return value;
}

void Interpreter::ReadPlayerSnapshot(PlayerSnapshot& s)
{
	s.id = ReadUint();
	s.state.pos[0] = ReadFloat();
	s.state.pos[1] = ReadFloat();
	s.state.pos[2] = ReadFloat();
	s.state.rot[0] = ReadFloat();
	s.state.rot[1] = ReadFloat();
	s.state.rot[2] = ReadFloat();
	s.state.rot[3] = ReadFloat();
}

void Interpreter::SimulateBufferEnqueue()
{
	std::printf("ENQUEUE: %u\n", (unsigned)currentSnapshots.snapshot.size());
}

void Interpreter::SimulateBufferUpdate()
{
	std::printf("UPDATE\n");
}

void Interpreter::SimulateInterpolation()
{
	std::printf("Interpol: %u\n",(unsigned)interpolation.lerps.size());
}

void Interpreter::ProcessEnqueue()
{
	float ts = ReadFloat();
	uint32_t i, num = ReadUint();
	currentSnapshots.snapshot.resize(num);
	for (i=0; i<num; i++) {
		ReadPlayerSnapshot(currentSnapshots.snapshot[i]);
		currentSnapshots.snapshot[i].state.timestamp = ts;
	}
	SimulateBufferEnqueue();
}

void Interpreter::ProcessUpdateBegin() 
{
	update.valid = true;
	update.timestamp =  ReadFloat();
	update.m_InterpolationStartTime_before = ReadFloat();
}

void Interpreter::ProcessUpdateEnd()
{
	update.m_InterpolationStartTime_after = ReadFloat();

	if (update.valid) {
		SimulateBufferUpdate();
	}
	update.valid = false;
}

void Interpreter::ProcessInterpolateBegin()
{
	interpolation.valid=true;
	interpolation.timestamp = ReadFloat();
	interpolation.lerps.clear();
}

void Interpreter::ProcessInterpolateEnd()
{
	if (interpolation.valid) {
		SimulateInterpolation();
	}
	interpolation.valid=false;
}

void Interpreter::ProcessLerpBegin()
{
	LerpCycle c;
	c.waitForRespawn_before = ReadUint();
	ReadPlayerSnapshot(c.A);
	ReadPlayerSnapshot(c.B);
	c.A.state.timestamp = -1.0f; /// we do not know
	c.B.state.timestamp = -1.0f; /// we do not know
	c.t=ReadFloat();
	interpolation.lerps.push_back(c);
}

void Interpreter::ProcessLerpEnd()
{
	uint32_t waitForRespawn = ReadUint();
	if (interpolation.lerps.size() < 1) {
		return;
	}
	LerpCycle &c = interpolation.lerps[interpolation.lerps.size()-1];
	c.waitForRespawn_after = waitForRespawn;
}

bool Interpreter::ProcessCommand()
{
	if (!file || feof(file) || ferror(file)) {
		return false;
	}

	Command cmd = (Command)ReadUint();
	std::printf("COMMAND is: %u\n",(uint32_t)cmd);
	switch(cmd) {
		case ENQUEUE:
			ProcessEnqueue();
			break;
		case UPDATE_BEGIN:
			ProcessUpdateBegin();
			break;
		case UPDATE_END:
			ProcessUpdateEnd();
			break;
		case INTERPOLATE_BEGIN:
			ProcessInterpolateBegin();
			break;
		case INTERPOLATE_END:
			ProcessInterpolateEnd();
			break;
		case LERP_BEGIN:
			ProcessLerpBegin();
			break;
		case LERP_END:
			ProcessLerpEnd();
			break;
		default:
			// unknown command, can't parse file any further
			CloseFile(); 
	}

	return (file != NULL);
}

void Interpreter::CloseFile()
{
	if (file) {
		std::fclose(file);
		file = NULL;
	}
	fileName = NULL;
}

bool Interpreter::ProcessFile(const char *filename)
{
	if (!OpenFile(filename)) {
		return false;
	}

	// process the file until end or error is reached
	while (ProcessCommand());

	CloseFile();
	return true;
}


}; // namespace OlmodPlayerDumpState 


int main(int argc, char **argv)
{

	OlmodPlayerDumpState::Interpreter interpreter;
	int exit_code = !interpreter.ProcessFile((argc > 1)?argv[1]:"playerstatedump0.olmd");
	return exit_code;
}
