CFLAGS=-Wall -O2 -g -fPIC

all: olmod.so

olmod.so: olmod.o
	$(CC) -shared -o $@ $^ -ldl

clean:
	rm -f olmod.so

dist: olmod.so
	cp -p olmod.so olmod.sh olmodserver.sh ../x64/Release
