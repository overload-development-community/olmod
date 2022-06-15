// olmod.cpp : Defines the entry point for the application.
//

#include "stdafx.h"
#include "olmod.h"

// Hint that the discrete gpu should be enabled on optimus/enduro systems
// NVIDIA docs: http://developer.download.nvidia.com/devzone/devcenter/gamegraphics/files/OptimusRenderingPolicies.pdf
// AMD forum post: http://devgurus.amd.com/thread/169965
extern "C"
{
	__declspec(dllexport) DWORD NvOptimusEnablement = 0x00000001;
	__declspec(dllexport) int AmdPowerXpressRequestHighPerformance = 1;
}

typedef struct _MonoImage MonoImage;
typedef struct _MonoAssembly MonoAssembly;
typedef struct _MonoMethod MonoMethod;
typedef struct _MonoObject MonoObject;
typedef struct _MonoClass MonoClass;
typedef struct _MonoDomain MonoDomain;
typedef struct _MonoArray MonoArray;
typedef int32_t mono_bool;
typedef enum {
	MONO_IMAGE_OK,
	MONO_IMAGE_ERROR_ERRNO,
	MONO_IMAGE_MISSING_ASSEMBLYREF,
	MONO_IMAGE_IMAGE_INVALID
} MonoImageOpenStatus;
static HMODULE mono_lib;
typedef MonoImage *(*mono_image_open_from_data_with_name)(char *, int, int, MonoImageOpenStatus*, int, const char *);
typedef void(*mono_image_close)(MonoImage *image);
typedef MonoAssembly* (*mono_assembly_load_from_full)  (MonoImage *image, const char *fname,
	MonoImageOpenStatus *status, mono_bool refonly);
typedef MonoClass* (*mono_class_from_name) (MonoImage *image, const char* name_space, const char *name);
typedef MonoMethod* (*mono_class_get_method_from_name) (MonoClass *klass, const char *name, int param_count);
typedef MonoObject* (*mono_runtime_invoke) (MonoMethod *method, MonoObject *obj, void **params, MonoObject **exc);
typedef MonoClass* (*mono_get_byte_class) ();
typedef MonoDomain* (*mono_domain_get) ();
typedef char* (*mono_array_addr_with_size) (MonoArray *array, int size, uintptr_t idx);
typedef MonoArray *(*mono_array_new) (MonoDomain *domain, MonoClass *eclass, uintptr_t n);
typedef char *(*mono_class_get_name)(MonoClass *);
typedef char *(*mono_class_get_namespace)(MonoClass *);
typedef char *(*mono_method_get_name)(MonoMethod *);
//static MonoClass *(*mono_object_get_class)(MonoObject *);
typedef MonoClass *(*mono_method_get_class)(MonoMethod *);

static mono_image_open_from_data_with_name org_mono_image_open_from_data_with_name;
static mono_image_close org_mono_image_close;
static mono_assembly_load_from_full org_mono_assembly_load_from_full;
static mono_class_from_name org_mono_class_from_name;
static mono_class_get_method_from_name org_mono_class_get_method_from_name;
static mono_runtime_invoke org_mono_runtime_invoke;
static mono_get_byte_class org_mono_get_byte_class;
static mono_domain_get org_mono_domain_get;
static mono_array_addr_with_size org_mono_array_addr_with_size;
static mono_array_new org_mono_array_new;
static mono_class_get_name org_mono_class_get_name;
static mono_class_get_namespace org_mono_class_get_namespace;
static mono_method_get_name org_mono_method_get_name;
static mono_method_get_class org_mono_method_get_class;

static MonoImage *gamemod_img = NULL;
static WCHAR game_dir[256];

static HANDLE console = NULL;
void print(const char *message) {
	if (!message)
		message = "(null)";
	DWORD written = 0;
	if (console && console != INVALID_HANDLE_VALUE) {
		DWORD len = lstrlenA(message);
		WriteFile(console, message, len, &written, NULL);
		if (len && message[len - 1] == '\n')
			FlushFileBuffers(console);
	}
}

void printw(LPCWSTR message) {
	DWORD written = 0;
	if (console && console != INVALID_HANDLE_VALUE)
		for (const wchar_t *p = message; *p; p++)
			WriteFile(console, p, 1, &written, NULL);
}

static MonoImage *load_image_data(char *data, uint32_t size, const char *filename) {
	MonoImageOpenStatus status;
	MonoImage *image;
	if (!(image = org_mono_image_open_from_data_with_name(data, size, 1, &status, 0, filename)) ||
		status != MONO_IMAGE_OK)
		return NULL;
	MonoAssembly *assem;
	if (!(assem = org_mono_assembly_load_from_full(image, filename, &status, 0)) || status != MONO_IMAGE_OK) {
		org_mono_image_close(image);
		return NULL;
	}
	return image;
}

static int run_void_method(MonoImage *image,
	const char *name_space, const char *cls_name, const char *method_name) {
	MonoClass *cls;
	if (!(cls = org_mono_class_from_name(image, name_space, cls_name))) {
		print("cannot find class\n");
		return -1;
	}
	MonoMethod* method;
	if (!(method = org_mono_class_get_method_from_name(cls, method_name, 0))) {
		print("cannot find method ");
		print(method_name);
		print("\n");
		return -1;
	}

	//MonoObject *result;
	MonoObject *exc;
	org_mono_runtime_invoke(method, NULL, NULL, &exc);
	if (exc) {
		print("invoke exception caught\n");
		return -1;
	}
	//print("invoke done\n");
	return 0;
}


static MonoObject* my_mono_runtime_invoke(MonoMethod *method, MonoObject *obj, void **params, MonoObject **exc)
{
	//MonoClass *cls = org_mono_method_get_class(method);print("runtime invoke ");print(org_mono_class_get_namespace(cls));print(" ");print(org_mono_class_get_name(cls));print(" ");print(org_mono_method_get_name(method));print("\n");
	if (gamemod_img) {
		run_void_method(gamemod_img, "GameMod.Core", "GameMod", "Initialize");
		gamemod_img = NULL;
	}
	return org_mono_runtime_invoke(method, obj, params, exc);
}

static int load_image(const char *filename, MonoImage **pimage, MonoAssembly **passem) {
	MonoImage *image = NULL;
	MonoAssembly *assem = NULL;
	HANDLE h = NULL;
	char *data = NULL;
	DWORD size, rd_size;
	if ((h = CreateFileA(filename, GENERIC_READ, FILE_SHARE_WRITE | FILE_SHARE_READ, NULL, OPEN_EXISTING, 0, NULL)) == INVALID_HANDLE_VALUE) {
		print("cannot open ");
		print(filename);
		print("\n");
		goto err;
	}
	if ((size = GetFileSize(h, NULL)) == INVALID_FILE_SIZE) {
		print("cannot size\n");
		goto err;
	}
	if (!(data = (char *)HeapAlloc(GetProcessHeap(), 0, size))) {
		print("cannot alloc\n");
		goto err;
	}
	if (!ReadFile(h, data, size, &rd_size, NULL) || rd_size != size) {
		print("cannot read\n");
		goto err;
	}
	CloseHandle(h);
	h = NULL;
	MonoImageOpenStatus status;
	if (!(image = org_mono_image_open_from_data_with_name(data, size, 1, &status, 0, filename)) ||
		status != MONO_IMAGE_OK) {
		print("cannot open image\n");
		goto err;
	}
	if (!(assem = org_mono_assembly_load_from_full(image, filename, &status, 0)) || status != MONO_IMAGE_OK) {
		print("cannot load assem\n");
		goto err;
	}
	if (pimage)
		*pimage = image;
	if (passem)
		*passem = assem;
	return 0;
err:
	if (image)
		org_mono_image_close(image);
	if (data)
		HeapFree(GetProcessHeap(), 0, data);
	if (h)
		CloseHandle(h);
	return -1;
}

/*
extern char _binary_0Harmony_dll_start;
extern char _binary_0Harmony_dll_end;
extern int _binary_0Harmony_dll_size;
extern char _binary_GameMod_dll_start;
extern char _binary_GameMod_dll_end;
*/

static void *my_mono_image_open_from_data_with_name(char *data, int data_len, int copy,
	MonoImageOpenStatus *st, int ref, const char*name)
{
	//print(name);print("\n");
	int name_len;
	if ((name_len = lstrlenA(name)) > 19 && lstrcmpA(name + name_len - 19, "Assembly-CSharp.dll") == 0) {
		/*
		load_image_data(&_binary_0Harmony_dll_start, &_binary_0Harmony_dll_end - &_binary_0Harmony_dll_start,
			"0Harmony.dll");
		gamemod_img = load_image_data(&_binary_GameMod_dll_start, &_binary_GameMod_dll_end - &_binary_GameMod_dll_start,
			"GameMod.dll");
		*/
		MonoImage *harmony_img;
		char buf[256];
		int len = GetModuleFileNameA(NULL, buf, sizeof(buf));
		while (len && buf[len - 1] != '\\')
			len--;
		buf[len] = 0;
		StringCbCatA(buf, sizeof(buf), "0Harmony.dll");
		load_image(buf, &harmony_img, NULL);
		buf[len] = 0;
		StringCbCatA(buf, sizeof(buf), "GameMod.dll");
		load_image(buf, &gamemod_img, NULL);
	}
	return org_mono_image_open_from_data_with_name(data, data_len, copy, st, ref, name);
}

static FARPROC MyGetProcAddress(HMODULE hModule, LPCSTR lpProcName) {
	FARPROC ret = GetProcAddress(hModule, lpProcName);
	if (lpProcName[0] != 'm')
		return ret;
	//print(lpProcName);print("\n");
	if (lstrcmpA(lpProcName, "mono_image_close") == 0)
		org_mono_image_close = (mono_image_close)ret;
	if (lstrcmpA(lpProcName, "mono_runtime_invoke") == 0) {
		org_mono_runtime_invoke = (mono_runtime_invoke)ret;
		return (PROC)my_mono_runtime_invoke;
	}
	if (lstrcmpA(lpProcName, "mono_assembly_load_from_full") == 0)
		org_mono_assembly_load_from_full = (mono_assembly_load_from_full)ret;
	if (lstrcmpA(lpProcName, "mono_class_from_name") == 0)
		org_mono_class_from_name = (mono_class_from_name)ret;
	if (lstrcmpA(lpProcName, "mono_class_get_method_from_name") == 0)
		org_mono_class_get_method_from_name = (mono_class_get_method_from_name)ret;
	if (lstrcmpA(lpProcName, "mono_get_byte_class") == 0)
		org_mono_get_byte_class = (mono_get_byte_class)ret;
	if (lstrcmpA(lpProcName, "mono_domain_get") == 0)
		org_mono_domain_get = (mono_domain_get)ret;
	if (lstrcmpA(lpProcName, "mono_array_addr_with_size") == 0)
		org_mono_array_addr_with_size = (mono_array_addr_with_size)ret;
	if (lstrcmpA(lpProcName, "mono_array_new") == 0)
		org_mono_array_new = (mono_array_new)ret;
	if (lstrcmpA(lpProcName, "mono_method_get_class") == 0)
		org_mono_method_get_class = (mono_method_get_class)ret;
	if (lstrcmpA(lpProcName, "mono_class_get_name") == 0)
		org_mono_class_get_name = (mono_class_get_name)ret;
	if (lstrcmpA(lpProcName, "mono_class_get_namespace") == 0)
		org_mono_class_get_namespace = (mono_class_get_namespace)ret;
	if (lstrcmpA(lpProcName, "mono_method_get_name") == 0)
		org_mono_method_get_name = (mono_method_get_name)ret;
	/*
	if (lstrcmpA(lpProcName, "mono_domain_assembly_open") == 0 ||
		lstrcmpA(lpProcName, "mono_debug_open_image_from_memory") == 0 ||
		lstrcmpA(lpProcName, "mono_image_open_from_data_full") == 0 ||
		lstrcmpA(lpProcName, "mono_assembly_open") == 0 ||
		lstrcmpA(lpProcName, "mono_assembly_load_from") == 0)
		return (PROC)abort;
	*/
	if (lstrcmpA(lpProcName, "mono_image_open_from_data_with_name") == 0) {
		mono_lib = hModule;
		org_mono_image_open_from_data_with_name = (mono_image_open_from_data_with_name)ret;
		return (PROC)my_mono_image_open_from_data_with_name;
	}
	return ret;
}

typedef void * (__stdcall *idetd) (HMODULE module, BOOL, int, ULONG *);

// from http://blog.neteril.org/blog/2016/12/23/diverting-functions-windows-iat-patching/
// Apache 2.0 license
static BOOL patch_func(HMODULE module, const char *name, PROC func)
{
	// We use this value as a comparison
	PROC baseGetProcAddress = (PROC)GetProcAddress(GetModuleHandleA("KERNEL32.dll"), name);

	// Get a reference to the import table to locate the kernel32 entry
	//ULONG size;
	//idetd myImageDirectoryEntryToData = (idetd)GetProcAddress(LoadLibrary(L"dbghelp.dll"), "ImageDirectoryEntryToData");
	//PIMAGE_IMPORT_DESCRIPTOR importDescriptor = (PIMAGE_IMPORT_DESCRIPTOR)myImageDirectoryEntryToData(module, TRUE, IMAGE_DIRECTORY_ENTRY_IMPORT, &size);

	BYTE *imageBuf = (BYTE *)module;
	PIMAGE_DOS_HEADER pDOSHeader = (PIMAGE_DOS_HEADER)imageBuf;
	LOADED_IMAGE image;
	image.FileHeader = 	(PIMAGE_NT_HEADERS64)(imageBuf + pDOSHeader->e_lfanew);
	image.NumberOfSections = image.FileHeader->FileHeader.NumberOfSections;
	image.Sections = (PIMAGE_SECTION_HEADER)(imageBuf + pDOSHeader->e_lfanew + sizeof(IMAGE_NT_HEADERS64));
	PIMAGE_IMPORT_DESCRIPTOR importDescriptor = (PIMAGE_IMPORT_DESCRIPTOR)(imageBuf +
		image.FileHeader->OptionalHeader.DataDirectory[IMAGE_DIRECTORY_ENTRY_IMPORT].VirtualAddress);

	// In the import table find the entry that corresponds to kernel32
	BOOL found = FALSE;
	while (importDescriptor->Characteristics && importDescriptor->Name) {
		PSTR importName = (PSTR)((PBYTE)module + importDescriptor->Name);
		if (lstrcmpA(importName, "KERNEL32.dll") == 0) {
			found = TRUE;
			break;
		}
		importDescriptor++;
	}
	if (!found)
		return FALSE;

	// From the kernel32 import descriptor, go over its IAT thunks to
	// find the one used by the rest of the code to call GetProcAddress
	PIMAGE_THUNK_DATA thunk = (PIMAGE_THUNK_DATA)((PBYTE)module + importDescriptor->FirstThunk);
	while (thunk->u1.Function) {
		PROC* funcStorage = (PROC*)&thunk->u1.Function;
		// Found it, now let's patch it
		if (*funcStorage == baseGetProcAddress) {
			// Get the memory page where the info is stored
			MEMORY_BASIC_INFORMATION mbi;
			VirtualQuery(funcStorage, &mbi, sizeof(MEMORY_BASIC_INFORMATION));

			// Try to change the page to be writable if it's not already
			if (!VirtualProtect(mbi.BaseAddress, mbi.RegionSize, PAGE_READWRITE, &mbi.Protect))
				return FALSE;

			// Store our hook
			*funcStorage = func;

			// Restore the old flag on the page
			DWORD dwOldProtect;
			VirtualProtect(mbi.BaseAddress, mbi.RegionSize, mbi.Protect, &dwOldProtect);

			// Profit
			return TRUE;
		}

		thunk++;
	}

	return FALSE;
}

static DWORD MyGetModuleFileNameW(HMODULE hModule, LPWSTR lpFilename, DWORD nSize) {
	if (!hModule) {
		LPCWSTR suffix = L"\\Overload.exe";
		StringCchCopyW(lpFilename, nSize, game_dir);
		StringCchCatW(lpFilename, nSize, suffix);
		DWORD ret = lstrlenW(game_dir) + lstrlenW(suffix);
		if (ret >= nSize) {
			SetLastError(ERROR_INSUFFICIENT_BUFFER);
			ret = nSize;
		}
		return ret;
	}
	return GetModuleFileNameW(hModule, lpFilename, nSize);
}

static BOOL patch_functions() {
	HMODULE module = GetModuleHandle(L"UnityPlayer.dll");
	if (module == NULL) {
		print("Module not found\n");
		return FALSE;
	}
	if (!patch_func(module, "GetProcAddress", (PROC)MyGetProcAddress))
		return FALSE;
	if (!patch_func(module, "GetModuleFileNameW", (PROC)MyGetModuleFileNameW))
		return FALSE;
	return TRUE;
}

static void show_msg(const char *msg) {
	MessageBoxA(0, msg, "olmod", MB_OK);
}

static void show_wmsg(WCHAR *msg) {
	MessageBoxW(0, msg, L"olmod", MB_OK);
}

static int is_game_dir_ok() {
	static WCHAR buf[MAX_PATH];
	DWORD attr;

	if (StringCbCopyW(buf, sizeof(buf), game_dir) ||
		StringCbCatW(buf, sizeof(buf), L"\\Overload_Data"))
		return 0;
	attr = GetFileAttributes(buf);
	if (attr == INVALID_FILE_ATTRIBUTES || !(attr & FILE_ATTRIBUTE_DIRECTORY))
		return 0;
	if (StringCbCopyW(buf, sizeof(buf), game_dir) ||
		StringCbCatW(buf, sizeof(buf), L"\\UnityPlayer.dll"))
		return 0;
	attr = GetFileAttributes(buf);
	if (attr == INVALID_FILE_ATTRIBUTES || (attr & FILE_ATTRIBUTE_DIRECTORY))
		return 0;
	return 1;
}

static int set_game_dir_from_args() {
	LPWSTR *szArglist;
	int nArgs, i;

	szArglist = CommandLineToArgvW(GetCommandLineW(), &nArgs);
	if (szArglist) {
		for (i = 1; i < nArgs - 1; i++)
			if (lstrcmpiW(szArglist[i], L"-gamedir") == 0 &&
				!StringCbCopyW(game_dir, sizeof(game_dir), szArglist[i + 1])) {
				LocalFree(szArglist);
				return 1;
			}
		LocalFree(szArglist);
	}
	return 0;
}

static int find_game_dir() {
	HKEY hKey;

	if (GetCurrentDirectory(sizeof(game_dir) / sizeof(game_dir[0]), game_dir) != 0) {
		WCHAR *p;
		if (is_game_dir_ok())
			return 1;
		// check parent directory
		p = game_dir + lstrlenW(game_dir);
		while (p > game_dir && *--p != '\\')
			;
		if (p > game_dir) {
			*p = 0;
			if (is_game_dir_ok())
				return 1;
		}
	}
	if (RegOpenKey(HKEY_LOCAL_MACHINE, L"SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Uninstall\\Steam App 448850",
		&hKey) == ERROR_SUCCESS) {
		DWORD val_len = sizeof(game_dir);
		if (RegQueryValueEx(hKey, L"InstallLocation",
			NULL, NULL, (LPBYTE)game_dir, &val_len) == ERROR_SUCCESS && is_game_dir_ok())
			return 1;
	}
	if (RegOpenKey(HKEY_LOCAL_MACHINE, L"SOFTWARE\\WOW6432Node\\GOG.com\\Games\\1309632191",
		&hKey) == ERROR_SUCCESS) {
		DWORD val_len = sizeof(game_dir);
		if (RegQueryValueEx(hKey, L"path",
			NULL, NULL, (LPBYTE)game_dir, &val_len) == ERROR_SUCCESS && is_game_dir_ok())
			return 1;
	}
	return 0;
}

typedef int(*wWinMain_type)(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR lpCmdLine, int nShowCmd);

int WINAPI wWinMain(HINSTANCE hInstance, HINSTANCE hPrevInstance, LPWSTR lpCmdLine, int nShowCmd) {
	console = GetStdHandle(STD_OUTPUT_HANDLE);
	//console = CreateFile(L"c:\\temp\\olmod.log", GENERIC_WRITE, FILE_SHARE_READ, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
	//DuplicateHandle(GetCurrentProcess(), GetStdHandle(STD_OUTPUT_HANDLE), GetCurrentProcess(), &console, 0, FALSE, DUPLICATE_SAME_ACCESS);

	static WCHAR buf[256 + 64], *p;

	// workaround for openssl 1.0.2j bug with newer intel CPUs, see github issue #182
	// disable the broken code path for SHAEXT via OPENSSL_ia32cap environment variable,
	// but only if the user has not set that variable already...
	if ((GetEnvironmentVariable(L"OPENSSL_ia32cap", buf, sizeof(buf) / sizeof(buf[0])) < 1) && (GetLastError() == ERROR_ENVVAR_NOT_FOUND)) {
		if (!SetEnvironmentVariable(L"OPENSSL_ia32cap", L":~0x20000000")) {
			show_msg("failed to set OPENSSL_ia32cap environment variable for OpenSSL 1.0.2j SHAEXT bug workaround");
		}
	}

	if (set_game_dir_from_args()) {
		if (!is_game_dir_ok()) {
			show_msg("Cannot find game in directory specified with -gamedir!");
			return 1;
		}
	} else if (!find_game_dir()) {
		show_msg("Game directory not found! Specify manually with -gamedir or copy olmod to the game directory.");
		return 1;
	}

	//print("gamedir ");printw(game_dir);print("\n");

	if (!GetModuleFileName(NULL, buf, sizeof(buf) / sizeof(buf[0]))) {
		show_msg("Cannot get filename");
		return 1;
	}
	p = buf + lstrlenW(buf) - 1;
	while (p >= buf && *p != '\\')
		p--;
	if (p >= buf)
		*p = 0;
	if (!SetEnvironmentVariable(L"OLMODDIR", buf)) {
		show_msg("Cannot set environment");
		return 1;
	}

	if (!SetCurrentDirectory(game_dir)) {
		show_msg("Cannot change directory");
		return 1;
	}
	StringCbCopyW(buf, sizeof(buf), game_dir);
	StringCbCatW(buf, sizeof(buf), L"\\UnityPlayer.dll");
	HMODULE lib = LoadLibrary(buf);
	if (!lib) {
		StringCbCatW(buf, sizeof(buf), L" not found");
		show_wmsg(buf);
		return 1;
	}
	wWinMain_type UnityMain = (wWinMain_type)GetProcAddress(lib, "UnityMain");
	if (!UnityMain) {
		show_msg("Player lib Main not found\n");
		return 1;
	}

	if (!patch_functions()) {
		show_msg("GetProcAddress hook failed\n");
		return 1;
	}
	return UnityMain(hInstance, hPrevInstance, lpCmdLine, nShowCmd);
}

// From LIBCTINY - Matt Pietrek 2001, MSDN Magazine, January 2001, CRT0TWIN.CPP
int WINAPI wWinMainCRTStartup(void)
{
	STARTUPINFO StartupInfo = { sizeof(STARTUPINFO),0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0 };
	int mainret;
	TCHAR *lpszCommandLine = GetCommandLine();

	// skip past program name (first token in command line).
	if (*lpszCommandLine == '"')  // check for and handle quoted program name
	{
		// scan, and skip over, subsequent characters until  another
		// double-quote or a null is encountered
		while (*lpszCommandLine && (*lpszCommandLine != '"'))
			lpszCommandLine++;

		// if we stopped on a double-quote (usual case), skip over it.
		if (*lpszCommandLine == '"')
			lpszCommandLine++;
	}
	else
	{
		// first token wasn't a quote
		while (*lpszCommandLine > ' ')
			lpszCommandLine++;
	}

	// skip past any white space preceeding the second token.
	while (*lpszCommandLine && (*lpszCommandLine <= ' '))
		lpszCommandLine++;

	GetStartupInfo(&StartupInfo);

	mainret = wWinMain(GetModuleHandle(NULL),
		NULL,
		lpszCommandLine,
		StartupInfo.dwFlags & STARTF_USESHOWWINDOW
		? StartupInfo.wShowWindow : SW_SHOWDEFAULT);

	ExitProcess(mainret);
}
