#!/bin/bash

if [ "$1" = "-h" ] || [ "$1" = "--help" ]; then
    echo "Usage: $0 [-c|--copy]"
    echo "    -c|--copy    Copies the generated GameMod.dll to your Overload directory"
    exit
fi

if ! command -v mcs &> /dev/null; then
    echo "ERROR: You need the mono 'mcs' compiler (e.g. package 'mono-dev' on Ubuntu)."
    exit 1
fi

if test -z "${OLPATH}"; then
    echo "ERROR: Please specify your Overload installation directory in your environment.\n"
    echo
    echo "Examples:"
    echo "        Steam: export OLPATH=~/.steam/steam/steamapps/common/Overload"
    echo "   Steam alt.: export OLPATH=~/.local/share/Steam/steamapps/common/Overload"
    echo "          GOG: export OLPATH=~/GOG\\ Games/Overload/game"
    exit 1
fi

echo "$OLPATH/Overload_Data"
if ! test -d "${OLPATH}/Overload_Data"; then
    echo "ERROR: Could not locate your Overload_Data directory under the given path."
    exit 1
fi

echo ../GameMod/*.cs

mcs \
    -r:"${OLPATH}"/Overload_Data/Managed/Assembly-CSharp.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/Assembly-CSharp-firstpass.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/DotNetZip.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/Newtonsoft.Json.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/Rewired_Core.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.AudioModule.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.CoreModule.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.IMGUIModule.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.Networking.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.ParticleSystemModule.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.PhysicsModule.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.UnityWebRequestModule.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.UnityWebRequestWWWModule.dll \
    -r:"${OLPATH}"/Overload_Data/Managed/UnityEngine.UNETModule.dll \
    -r:../GameMod/0Harmony.dll \
    -target:library \
    -sdk:2 \
    -resource:../GameMod/Resources/meshes,GameMod.Resources.meshes \
	-resource:../GameMod/Resources/audio,GameMod.Resources.audio \
    -out:GameMod.dll \
    ../GameMod/*.cs \
    ../GameMod/*/*.cs

if [ $? -ne 0 ]; then
    exit 1
fi

if [ "$1" = '-c' ] || [ "$1" = '--copy' ]; then
    cp -v GameMod.dll ${OLPATH}
fi
