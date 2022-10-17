# Makefile for unix systems
# this requires GNU make

APPNAME=pmp_heat

# enable all warnings in general
WARNFLAGS= -Wall -Wextra -Wno-unused-parameter

# optimize flags, only used for RELEASE=1 builds
OPTIMIZEFLAGS = -mtune=native -march=native -flto -O4 -DNDEBUG

ifeq ($(RELEASE), 1)
CFLAGS =   $(BASECFLAGS) $(WARNFLAGS) $(OPTIMIZEFLAGS) -s
else
CFLAGS =   $(BASECFLAGS) $(WARNFLAGS) -Werror -g
endif

# all needed libraries
LINK = -lm

# Files

# "shared" files are all files needed both for the uebung and the project
CFILES=$(wildcard *.c)
INCFILES=$(wildcard *.h)
PRJFILES=Makefile $(wildcard *.sln)

# build rules
.PHONY: all
all:	$(APPNAME)

# rules to build applications
$(APPNAME): $(CFILES) $(INCFILES)
	$(CC) $(CFLAGS) $(CFILES) $(LDFLAGS) $(LINK) -o$(APPNAME)

# remove all unneeded files
.PHONY: clean
clean:
	@echo removing binaries: $(APPNAME)
	@rm -f $(APPNAME)

