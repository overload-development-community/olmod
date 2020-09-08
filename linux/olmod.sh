#!/bin/bash
args=()
gamedir="$(cd "$(dirname "$0")"; pwd)"
olmoddir="$(cd "$(dirname "$0")"; pwd)"
while [[ $# -gt 0 ]]
do
	key="$1"
	case $key in
		-gamedir)
			gamedir="$2"
			shift
			shift
			;;
		*)
			args+=("$1")
			shift
			;;
	esac
done
olargs=${args[@]}

if [[ -z "$OSTYPE" || "$OSTYPE" != "darwin"* ]];
then
	olmodso="${olmoddir}/olmod.so"
	overload="${gamedir}/Overload.x86_64"
	if [[ -f "${olmodso}" ]];
	then
		if [[ -f "${overload}" ]];
		then
			cd "${olmoddir}"
			OLMODDIR="${olmoddir}" LD_PRELOAD="${LD_PRELOAD:+$LD_PRELOAD:}./olmod.so" "${overload}" ${olargs}
		else
			echo "Error: Overload.x86_64 not found." >&2
			echo "Looked in ${gamedir}" >&2
			echo "Use the -gamedir switch to specify the path that Overload.x86_64 resides in." >&2
			exit 1
		fi
	else
		echo "Error: olmod.so not found." >&2
		echo "Looked in ${olmoddir}" >&2
		echo "You must run olmod.sh while it's in the same directory as olmod.so." >&2
		exit 1
	fi
else
	olmoddylib="${olmoddir}/olmod.dylib"
	overload="${gamedir}/Overload.app/Contents/MacOS/Overload"
	if [[ -f "${olmoddylib}" ]];
	then
		if [[ -f "${overload}" ]];
		then
			cd "${olmoddir}"
			OLMODDIR="${olmoddir}" DYLD_INSERT_LIBRARIES="${DYLD_INSERT_LIBRARIES:+$DYLD_INSERT_LIBRARIES:}./olmod.dylib" "${overload}" ${olargs}
		else
			echo "Error: Overload.app not found." >&2
			echo "Looked in ${gamedir}" >&2
			echo "Use the -gamedir switch to specify the path that Overload.app resides in." >&2
			exit 1
		fi
	else
		echo "Error: olmod.dylib not found." >&2
		echo "Looked in ${olmoddir}" >&2
		echo "You must run olmod.sh while it's in the same directory as olmod.dylib." >&2
		exit 1
	fi
fi
