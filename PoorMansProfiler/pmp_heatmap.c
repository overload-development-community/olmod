#include <stdio.h>
#include <stdlib.h>


#define STB_IMAGE_WRITE_IMPLEMENTATION
#include "stb_image_write.h"

static size_t startInterval = 1;
static size_t endInterval = 1;
static double extraScale = 1.0;
static double forceScale = -1.0;
static double forceMax = -1.0;
static double offset = 0.0;
static int secondToMax = 1;

static size_t
findEndLine(const char *data, size_t start, size_t end)
{
	size_t pos;
	for (pos = start; pos < end; pos++) {
		if (data[pos] == '\n' || data[pos] == '\r') {
			break;
		}
	}
	return pos;
}

static size_t
findStartLine(const char *data, size_t start, size_t end)
{
	size_t pos;
	for (pos = start; pos < end; pos++) {
		if (data[pos] != 0 && data[pos] != '\n' && data[pos] != '\r') {
			break;
		}
	}
	return pos;
}

static size_t
findEndField(const char *data, size_t start, size_t end)
{
	size_t pos;
	for (pos = start; pos < end; pos++) {
		if (data[pos] == '\t') {
			break;
		}
	}
	return pos;
}

static double
scan_matrix(const double *matrix, const long *funcs, size_t functions, size_t intervals, int mode)
{
	double max = -1.0;
	double max2 = -1.0;
	size_t x,y;
	for (y=0; y<functions; y++) {
		if (mode == 1) {
			if (funcs[y] == -7777) {
				continue;
			}
		}
		for (x=startInterval; x + endInterval <intervals; x++) {
			double val = matrix[y*intervals + x];
			if (offset != 0.0) {
				val -= offset;
			}
			if (val > max) {
				max2 = max;
				max = val;
			} else if (val > max2) {
				max2 = val;
			}
		}
	}

	return (secondToMax)?max2:max;
}

static void
to_heat(const double val, unsigned char* rgba)
{
	if (val <= 0.0001) {
		rgba[0]= 0;
		rgba[1]= 0;
		rgba[2]= 0;
		rgba[3]= 255;
	} else if (val <= 0.25) {
		double s = val * 4*255;
		rgba[0]= 0;
		rgba[1]= 255;
		rgba[2]= (unsigned char)s;
		rgba[3]= 255;
	} else if (val <= 0.5) {
		double s = (val - 0.25) * 4*255;
		rgba[0]= 0;
		rgba[1]= 255 - (unsigned char)s;
		rgba[2]= 255;
		rgba[3]= 255;
	} else if (val <= 0.75) {
		double s = (val - 0.5) * 4*255;
		rgba[0]= (unsigned char)s;
		rgba[1]= 0;
		rgba[2]= 255;
		rgba[3]= 255;
	} else if (val <= 1.0) {
		double s = (val - 0.75) * 4*255;
		rgba[0]= 255;
		rgba[1]= 0;
		rgba[2]= 255-(unsigned char)s;
		rgba[3]= 255;
	} else {
		rgba[0]= 255;
		rgba[1]= 255;
		rgba[2]= 255;
		rgba[3]= 255;
	}
}

static void
process_matrix(const char *fname, double *matrix, long *funcs, size_t functions, size_t intervals, int mode)
{
	unsigned char *rgba = calloc(4, functions*intervals);
	double max = scan_matrix(matrix, funcs, functions, intervals, mode);
	double scale;

	if (forceMax > 0.0) {
		max = forceMax;
	}
       
	scale = (max > 0.0)?1.0/max:1.0;
	if (forceScale > 0.0) {
		scale = forceScale;
	}
	scale = scale * extraScale;
	if (rgba) {
		size_t x,y;
		unsigned char *pixel = rgba;
		for (y=0; y<functions; y++) {
			for (x=0; x<intervals; x++) {
				double val = matrix[y*intervals + x];
				if (offset != 0.0) {
					val -= offset;
				}
				to_heat(val*scale, pixel);
			 	pixel+=4;
			}
		}
		printf("%s: scale=%f, max=%f\n", fname, scale, max);
     		stbi_write_png(fname, (int)intervals, (int)functions, 4, rgba, (int)(4*intervals));
		free(rgba);
        }
}


static void
process_data(const char *fname, char *data, size_t size)
{
	size_t pos=0;
	size_t line = findEndLine(data, 0, size);
	size_t intervals = 0;
	size_t functions = 0;
	double *matrix = NULL;
	char *fnameOut = NULL;
	size_t fnameLen = strlen(fname);
	long *funcs = NULL;
	int haveForced = (forceScale > 0.0 || forceMax > 0.0);

	while(pos < line) {
		intervals ++;
		pos = findEndField(data, pos, line)+1;
	}
	intervals-=2; // first and last column isn't an interval

	pos = 0;
	while(pos < size) {
		functions++;
		pos = findStartLine(data,findEndLine(data,pos,size),size);
	}

	fnameOut = calloc(1,fnameLen+5);
	if (!fnameOut) {
		return;
	}
	memcpy(fnameOut,fname, fnameLen);
	fnameOut[fnameLen]='x';
	memcpy(fnameOut + fnameLen+1, ".png", 5);
	funcs = calloc(sizeof(long),functions);
	if (!funcs) {
		return;
	}
	matrix = calloc(sizeof(double),intervals * functions);
	if (matrix) {
		size_t function = 0;
		int mode;
		pos = 0;
		while (pos < size) {
			size_t interval = 0;
			long funcId = 0;
			line = findEndLine(data,pos,size);
			while (pos < line) {
				size_t field = findEndField(data,pos,line);
				data[field] = 0;
				if (!interval) {
					funcId = strtol(data+pos, NULL, 10);
					funcs[function] = funcId;
					if (funcId == -7778) {
						// ignore per-Interval data as this will screw up the scale
						break;
					}
				} else {
					if (interval-1 < intervals && function < functions) {
						matrix[function*intervals + interval-1] = strtod(data+pos, NULL);
					}
				}
				interval++;
				pos=field+1;
			}
			function++;
			pos = findStartLine(data,line,size);
		}
		for (mode = 0; mode < ((haveForced)?1:2); mode++) {
			fnameOut[fnameLen] = '0'+mode;
			process_matrix(fnameOut,matrix,funcs, functions, intervals, mode);
		}
		free(matrix);
		free(funcs);
	}
}

static void
process(const char *fname)
{
	off_t size;
	char *data = NULL;
	FILE *f = fopen(fname,"rb");

	if (f) {
		fseeko(f, 0, SEEK_END);
		size = ftello(f);
		fseeko(f, 0, SEEK_SET);
		data = malloc(size+1);
		if (data) {
			if (fread(data, size, 1, f) == 1) {
				data[size]=0;
				process_data(fname, data, size);
			}
		}
		fclose(f);
	}
	if (data) {
		free(data);
	}
}

int
main(int argc, char **argv)
{
	int i;

	for (i=1; i<argc; i++) {
		if (i+1 < argc && argv[i][0] == '-') {
			if (!strcmp(argv[i],"-scale")) {
				extraScale = strtod(argv[i+1],NULL);
			} else if (!strcmp(argv[i],"-force")) {
				forceScale = strtod(argv[i+1],NULL);
			} else if (!strcmp(argv[i],"-max")) {
				forceMax = strtod(argv[i+1],NULL);
			} else if (!strcmp(argv[i],"-offset")) {
				offset = strtod(argv[i+1],NULL);
			} else if (!strcmp(argv[i],"-start")) {
				startInterval = strtoul(argv[i+1],NULL,0);
			} else if (!strcmp(argv[i],"-end")) {
				endInterval = strtoul(argv[i+1],NULL,0);
			} else if (!strcmp(argv[i],"-second")) {
				secondToMax = (int)strtol(argv[i+1],NULL,0);
			}
		}
	}
	for (i=1; i<argc; i++) {
		if (i+1 >= argc || argv[i][0] != '-') {
			process(argv[1]);
		}
	}
	return 0;
}
