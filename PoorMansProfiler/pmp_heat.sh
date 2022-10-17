#!/bin/bash

# HARDCODED FOR MY SYSTEM
DATADIR="${HOME}/.config/unity3d/Revival/Overload"
OUTDIR="${HOME}/tmp/DONTBACKUP/olmod-experiments/pmp/"
OLMODSRC="${HOME}/development/olmod"


mkdir -p "${OUTDIR}"
cd "${OUTDIR}"

for file in "${DATADIR}/olmod_pmp"*.csv; do
	SNAME="$(basename -- ${file})"
	mv "$file" .
	echo "$SNAME"
	"${OLMODSRC}/PoorMansProfiler/pmp_heat" "$@" "$SNAME" > "$SNAME.pmplog"
done


