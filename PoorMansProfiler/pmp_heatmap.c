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
static int showMode = 0;
static double showThreshold = 0.0;

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
scan_matrix(const double *matrix, const long *funcs, size_t functions, size_t intervals, int mode, size_t func, size_t interval)
{
	double max = -1.0;
	double max2 = -1.0;
	size_t x,y;
	size_t yStart = 0;
	size_t yEnd = functions;
	size_t xStart = startInterval;
	size_t xEnd = intervals;
	if (endInterval > xEnd) {
		xEnd = 0;
	} else {
		xEnd = xEnd-endInterval;
	}
	if (func != (size_t)-1) {
		yStart = func;
		yEnd = yStart+1;
	}
	if (interval != (size_t)-1) {
		xStart = interval;
		xEnd = interval+1;
	}

	for (y=yStart; y<yEnd; y++) {
		if (funcs[y] == -7778 && func != y) {
			// generally ignore per-Interval data as this will screw up the scale
			continue;
		}
		if (mode == 1) {
			if (func == (size_t)-1 && funcs[y] == -7777) {
				continue;
			}
		}
		for (x=xStart; x < xEnd; x++) {
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

static double
get_scale(double max)
{
	double scale;

	if (forceMax > 0.0) {
		max = forceMax;
	}
       
	scale = (max > 0.0)?1.0/max:1.0;
	if (forceScale > 0.0) {
		scale = forceScale;
	}
	scale = scale * extraScale;
	return scale;
}

static double*
get_scale_matrix(const double *matrix, const long *funcs, const char *fname, size_t functions, size_t intervals, int mode, int norm)
{
	double *scales = calloc(sizeof(double), functions*intervals);
	size_t x,y;
	
	if (!scales) {
		return NULL;
	}

	if (norm == 1) {
		for (y=0; y<functions; y++) {
			double max = scan_matrix(matrix, funcs, functions, intervals, 0, y, (size_t)-1);
			double scale = get_scale(max);
			printf("%s: func %ld: scale=%f, max=%f\n",fname,funcs[y],scale,max);
			for (x=0; x < intervals; x++) {
				scales[y*intervals + x] = scale;
			}
		}	
	} else if (norm == 2) {
		for (x=0; x < intervals; x++) {
			double max = scan_matrix(matrix, funcs, functions, intervals, 0, (size_t)-1, x);
			double scale = get_scale(max);
			printf("%s: interval %d: scale=%f, max=%f\n",fname,(int)x,scale,max);
			for (y=0; y<functions; y++) {
				scales[y*intervals + x] = scale;
			}
		}	
	} else {
		double max = scan_matrix(matrix, funcs, functions, intervals, mode, (size_t)-1, (size_t)-1);
		double scale = get_scale(max);
		printf("%s: scale=%f, max=%f\n",fname,scale,max);
		for (y=0; y<functions; y++) {
			for (x=0; x < intervals; x++) {
				scales[y*intervals + x] = scale;
			}
		}	
	}
	return scales;
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
process_matrix(const char *fname, double *matrix, long *funcs, size_t functions, size_t intervals, int mode,int norm)
{
	unsigned char *rgba;
	double *scales = get_scale_matrix(matrix, funcs, fname, functions, intervals, mode, norm);
       	rgba = calloc(4, functions*intervals);
	if (rgba && scales) {
		size_t x,y;
		unsigned char *pixel = rgba;
		for (y=0; y<functions; y++) {
			for (x=0; x<intervals; x++) {
				double val = matrix[y*intervals + x];
				if (offset != 0.0) {
					val -= offset;
				}
				to_heat(val*scales[y*intervals+x], pixel);
			 	pixel+=4;
			}
		}
     		stbi_write_png(fname, (int)intervals, (int)functions, 4, rgba, (int)(4*intervals));
        } else {
		printf("out of memory\n");
	}
	free(rgba);
	free(scales);
}

static void
process_csv(const char *fname, double *matrix, long *funcs, char** funcNames, size_t functions, size_t intervals)
{
	FILE *f = fopen(fname,"wt");
	if (f) {
		size_t x,y;
		for (x=0; x<functions; x++) {
			if (x > 0) {
				fputc('\t',f);
			}
			fprintf(f,"%ld",funcs[x]);
		}
		fputc('\n',f);
		for (y=0; y<intervals; y++) {
			for (x=0; x<functions; x++) {
				if (x > 0) {
					fputc('\t',f);
				}
				fprintf(f,"%.6f",matrix[x*intervals + y]);
			}
			fputc('\n',f);
		}
		for (x=0; x<functions; x++) {
			if (x > 0) {
				fputc('\t',f);
			}
			if (funcNames[x]) {
				fprintf(f,"%s",funcNames[x]);
			}
		}
		fputc('\n',f);
		fclose(f);
	}
}

static void
show_threshold_only(const char *fname, double *matrix, long *funcs, char** funcNames, size_t functions, size_t intervals)
{
	size_t y;
	double th = fabs(showThreshold);

	for (y=0; y<functions; y++) {
		double max = scan_matrix(matrix, funcs, functions, intervals, 0, y, (size_t)-1);
		int show = (showThreshold<0.0)?(max <= th):(max >= th);
		if (show) {
			printf("%s\t%ld\t%.6f\n",funcNames[y],funcs[y],max);
		}
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
	char **funcNames = NULL;
	int haveForced = (forceScale > 0.0 || forceMax > 0.0);

	while(pos < line) {
		intervals ++;
		pos = findEndField(data, pos, line)+1;
	}
	if (intervals == 1 && line >= 9) {
		if (!memcmp(data,"+++ OLMOD",9)) {
			printf("ignoring info file\n");
			return;
		}
	}
	if (intervals < 3) {
		printf("no data\n");
		return;
	}
	intervals-=2; // first and last column isn't an interval

	pos = 0;
	while(pos < size) {
		functions++;
		pos = findStartLine(data,findEndLine(data,pos,size),size);
	}

	fnameOut = calloc(1,fnameLen+7);
	if (!fnameOut) {
		printf("out of memory\n");
		return;
	}
	memcpy(fnameOut,fname, fnameLen);
	fnameOut[fnameLen]='x';
	fnameOut[fnameLen+1]='x';
	funcs = calloc(sizeof(long),functions);
	if (!funcs) {
		free(fnameOut);
		printf("out of memory\n");
		return;
	}
	funcNames = (char**)calloc(sizeof(char*),functions);
	if (!funcNames) {
		free(fnameOut);
		free(funcs);
		printf("out of memory\n");
		return;
	}
	matrix = calloc(sizeof(double),intervals * functions);
	if (matrix) {
		size_t function = 0;
		int mode;
		int norm;
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
				} else {
					if (interval-1 < intervals && function < functions) {
						matrix[function*intervals + interval-1] = strtod(data+pos, NULL);
					} else {
						funcNames[function] = data+pos;
					}
				}
				interval++;
				pos=field+1;
			}
			function++;
			pos = findStartLine(data,line,size);
		}
		if (showMode > 0) {
			show_threshold_only(fnameOut, matrix, funcs, funcNames, functions, intervals);
		} else {
			memcpy(fnameOut + fnameLen+2, ".png", 5);
			for (mode = 0; mode < ((haveForced)?1:2); mode++) {
				for (norm = 0; norm < ((haveForced)?1:3); norm++) {
					fnameOut[fnameLen] = '0'+mode;
					fnameOut[fnameLen+1] = '0'+norm;
					process_matrix(fnameOut,matrix,funcs, functions, intervals, mode, norm);
				}
			}
			fnameOut[fnameLen] = '_';
			fnameOut[fnameLen+1] = '_';
			memcpy(fnameOut + fnameLen+2, ".ctr", 5);
			process_csv(fnameOut, matrix, funcs, funcNames, functions, intervals);
		}
		free(matrix);
		free(funcs);
	} else {
		printf("out of memory\n");
	}
	free(fnameOut);
	free(funcNames);
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
	} else {
		printf("file %s not found\n",fname);
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
			} else if (!strcmp(argv[i],"-show")) {
				showThreshold = strtod(argv[i+1],NULL);
				showMode = 1;
			}
		}
	}
	for (i=1; i<argc; i++) {
		if (i+1 >= argc || argv[i][0] != '-') {
			process(argv[i]);
		} else {
			i++;
		}
	}
	return 0;
}
