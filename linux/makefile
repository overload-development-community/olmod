CFLAGS=-Wall -O2 -g -fPIC

all: olmod.so

mac: olmod.dylib

olmod.so: olmod.o
	$(CC) -shared -o $@ $^ -ldl

olmod.dylib: olmod.o
	$(CC) -dynamiclib -o $@ $^

GameMod.dll: ../GameMod/*.cs
	./mono_build.sh

clean:
	rm -f olmod.so

dist: olmod.so
	cp -p olmod.so olmod.sh olmodserver.sh ../x64/Release
