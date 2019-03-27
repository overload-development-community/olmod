#!/bin/sh
LD_PRELOAD=./olmod.so exec ./Overload.x86_64 "$@"
