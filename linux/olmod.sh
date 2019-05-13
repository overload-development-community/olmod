#!/bin/sh
export OLMODDIR=$(dirname "$0")
OLDIR=.
for arg; do
  if [ "$next_dir" = "0" ]; then OLDIR="$arg"; fi
  [ "$arg" = "-gamedir" ]; next_dir="$?"
done
LD_PRELOAD="$OLMODDIR/olmod.so" exec "$OLDIR/Overload.x86_64" "$@"
