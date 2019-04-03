#define _GNU_SOURCE
#include <stdlib.h>
#include <stdint.h>
#include <unistd.h>
#include <fcntl.h>
#include <dlfcn.h>
#include <string.h>
#include <sys/stat.h>

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

typedef MonoImage *(*mono_image_open_from_data_with_name_t)(char *, int, int, MonoImageOpenStatus*, int, const char *);
typedef void(*mono_image_close_t)(MonoImage *image);
typedef MonoAssembly* (*mono_assembly_load_from_full_t)(MonoImage *image, const char *fname,
	MonoImageOpenStatus *status, mono_bool refonly);
typedef MonoClass* (*mono_class_from_name_t)(MonoImage *image, const char* name_space, const char *name);
typedef MonoMethod* (*mono_class_get_method_from_name_t)(MonoClass *klass, const char *name, int param_count);
typedef MonoObject* (*mono_runtime_invoke_t)(MonoMethod *method, MonoObject *obj, void **params, MonoObject **exc);
typedef MonoClass* (*mono_get_byte_class_t)();
typedef MonoDomain* (*mono_domain_get_t)();
typedef char* (*mono_array_addr_with_size_t)(MonoArray *array, int size, uintptr_t idx);
typedef MonoArray *(*mono_array_new_t)(MonoDomain *domain, MonoClass *eclass, uintptr_t n);
typedef char *(*mono_class_get_name_t)(MonoClass *);
typedef char *(*mono_class_get_namespace_t)(MonoClass *);
typedef char *(*mono_method_get_name_t)(MonoMethod *);
typedef MonoClass *(*mono_method_get_class_t)(MonoMethod *);

static mono_image_close_t org_mono_image_close;
static mono_assembly_load_from_full_t org_mono_assembly_load_from_full;
static mono_class_from_name_t org_mono_class_from_name;
static mono_class_get_method_from_name_t org_mono_class_get_method_from_name;
static mono_get_byte_class_t org_mono_get_byte_class;
static mono_domain_get_t org_mono_domain_get;
static mono_array_addr_with_size_t org_mono_array_addr_with_size;
static mono_array_new_t org_mono_array_new;
static mono_class_get_name_t org_mono_class_get_name;
static mono_class_get_namespace_t org_mono_class_get_namespace;
static mono_method_get_name_t org_mono_method_get_name;
static mono_method_get_class_t org_mono_method_get_class;
static mono_image_open_from_data_with_name_t org_mono_image_open_from_data_with_name;
static mono_runtime_invoke_t org_mono_runtime_invoke;

static MonoImage *gamemod_img = NULL;

void print(const char *msg) {
	if (!msg)
		msg = "(null)";
	if (write(1, msg, strlen(msg)));
}

static int load_image(const char *filename, MonoImage **pimage, MonoAssembly **passem) {
	MonoImage *image = NULL;
	MonoAssembly *assem = NULL;
	int fd;
	char *data = NULL;
	int size;
	struct stat st;

	if ((fd = open(filename, O_RDONLY)) == -1) {
		print("olmod load_image: cannot open ");
		print(filename);
		print("\n");
		goto err;
	}
	if (fstat(fd, &st) == -1) {
		print("cannot size\n");
		goto err;
	}
	size = st.st_size;
	if (!(data = (char *)malloc(size))) {
		print("cannot alloc\n");
		goto err;
	}
	if (read(fd, data, size) != size) {
		print("cannot read\n");
		goto err;
	}
	close(fd);
	fd = -1;
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
		free(data);
	if (fd != -1)
		close(fd);
	return -1;
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
		print("olmod: invoke exception caught\n");
		void (*org_mono_print_unhandled_exception)(MonoObject*) = 
			dlsym(dlopen("./Overload_Data/Mono/x86_64/libmono.so",1), "mono_print_unhandled_exception");
		if (org_mono_print_unhandled_exception)
			org_mono_print_unhandled_exception(exc);
		else
			print("olmod: exception print failed\n");
		return -1;
	}
	//print("invoke done\n");
	return 0;
}


static MonoObject* my_mono_runtime_invoke(MonoMethod *method, MonoObject *obj, void **params, MonoObject **exc) {
	if (gamemod_img) {
		run_void_method(gamemod_img, "GameMod.Core", "GameMod", "Initialize");
		gamemod_img = NULL;
	}
	return org_mono_runtime_invoke(method, obj, params, exc);
}

// strlcat: Russ Allbery <rra@stanford.edu>, PD
static size_t strlcat(char *dst, const char *src, size_t size)
{
    size_t used, length, copy;

    used = strlen(dst);
    length = strlen(src);
    if (size > 0 && used < size - 1) {
        copy = (length >= size - used) ? size - used - 1 : length;
        memcpy(dst + used, src, copy);
        dst[used + copy] = '\0';
    }
    return used + length;
}

static void *my_mono_image_open_from_data_with_name(char *data, int data_len, int copy,
	MonoImageOpenStatus *st, int ref, const char*name)
{
	int name_len;
	if (name && (name_len = strlen(name)) > 19 && strcmp(name + name_len - 19, "Assembly-CSharp.dll") == 0) {
		/*
		load_image_data(&_binary_0Harmony_dll_start, &_binary_0Harmony_dll_end - &_binary_0Harmony_dll_start,
			"0Harmony.dll");
		gamemod_img = load_image_data(&_binary_GameMod_dll_start, &_binary_GameMod_dll_end - &_binary_GameMod_dll_start,
			"GameMod.dll");
		*/
		MonoImage *harmony_img;
		char buf[256];
		if (!getcwd(buf, sizeof(buf) - 16)) {
			print("olmod getcwd failed");
			abort();
		}
		//int len = GetModuleFileNameA(NULL, buf, sizeof(buf));
		//while (len && buf[len - 1] != '\\')
		//	len--;
		//buf[len] = 0;
		int len = strlen(buf);
		strlcat(buf, "/0Harmony.dll", sizeof(buf));
		load_image(buf, &harmony_img, NULL);
		buf[len] = 0;
		strlcat(buf, "/GameMod.dll", sizeof(buf));
		load_image(buf, &gamemod_img, NULL);
	}
	return org_mono_image_open_from_data_with_name(data, data_len, copy, st, ref, name);
}

static void *(*org_dlsym)(void*,const char*);

extern void *_dl_sym(void *, const char *, void *);

void *dlsym(void *lib, const char *sym) {
	void *ret = org_dlsym(lib, sym);
	if (sym[0] != 'm' || !ret)
		return ret;
	if (strcmp(sym, "mono_image_close") == 0)
		org_mono_image_close = (mono_image_close_t)ret;
	if (strcmp(sym, "mono_runtime_invoke") == 0) {
		org_mono_runtime_invoke = (mono_runtime_invoke_t)ret;
		return my_mono_runtime_invoke;
	}
	if (strcmp(sym, "mono_assembly_load_from_full") == 0)
		org_mono_assembly_load_from_full = (mono_assembly_load_from_full_t)ret;
	if (strcmp(sym, "mono_class_from_name") == 0)
		org_mono_class_from_name = (mono_class_from_name_t)ret;
	if (strcmp(sym, "mono_class_get_method_from_name") == 0)
		org_mono_class_get_method_from_name = (mono_class_get_method_from_name_t)ret;
	if (strcmp(sym, "mono_get_byte_class") == 0)
		org_mono_get_byte_class = (mono_get_byte_class_t)ret;
	if (strcmp(sym, "mono_domain_get") == 0)
		org_mono_domain_get = (mono_domain_get_t)ret;
	if (strcmp(sym, "mono_array_addr_with_size") == 0)
		org_mono_array_addr_with_size = (mono_array_addr_with_size_t)ret;
	if (strcmp(sym, "mono_array_new") == 0)
		org_mono_array_new = (mono_array_new_t)ret;
	if (strcmp(sym, "mono_method_get_class") == 0)
		org_mono_method_get_class = (mono_method_get_class_t)ret;
	if (strcmp(sym, "mono_class_get_name") == 0)
		org_mono_class_get_name = (mono_class_get_name_t)ret;
	if (strcmp(sym, "mono_class_get_namespace") == 0)
		org_mono_class_get_namespace = (mono_class_get_namespace_t)ret;
	if (strcmp(sym, "mono_method_get_name") == 0)
		org_mono_method_get_name = (mono_method_get_name_t)ret;
	if (strcmp(sym, "mono_image_open_from_data_with_name") == 0) {
		org_mono_image_open_from_data_with_name = ret;
		return my_mono_image_open_from_data_with_name;
	}
	return ret;
}

//static void olmod_init(void) __attribute__((constructor));

__attribute__((constructor)) static void olmod_init(void) 
{
	char buf[256], *p;
	if (readlink("/proc/self/exe", buf, sizeof(buf)) > 0 && (p = strrchr(buf, '/'))) {
		*p = 0;
		setenv("OLMODDIR", p, 1);
	}
	if (!(org_dlsym = _dl_sym(RTLD_NEXT, "dlsym", olmod_init))) {
		print("olmod failed dlsym lookup\n");
		abort();
	}
}
