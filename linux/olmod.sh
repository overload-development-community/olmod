#!/bin/sh
export OLMODDIR=$(dirname "$0")
OLDIR=.
for arg; do
  if [ "$next_dir" = "0" ]; then OLDIR="$arg"; fi
  [ "$arg" = "-gamedir" ]; next_dir="$?"
done
ISMAC=""
[ -n "$OSTYPE" ] && if [[ "$OSTYPE" == "darwin"* ]]; then ISMAC="1"; fi
if [ -n "$ISMAC" ]; then
DYLD_INSERT_LIBRARIES="${DYLD_INSERT_LIBRARIES:+$DYLD_INSERT_LIBRARIES:}$OLMODDIR/olmod.dylib" exec "$OLDIR/Overload.app/Contents/MacOS/Overload" "$@"
else
LD_PRELOAD="${LD_PRELOAD:+$LD_PRELOAD:}$OLMODDIR/olmod.so" exec "$OLDIR/Overload.x86_64" "$@"
fi
