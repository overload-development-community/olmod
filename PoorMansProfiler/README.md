# Poor Man's Profiler

A simple method-level profiler for olmod and Overload's game script functions. This collects timing statistics for a set of
C# functions one might be interested in. The data collection is separated in intervals (measured by Unity's `FixedUpdate` ticks, the
default is 60, which equals second in Overload Multiplayer). During each interval, for each functions, the following data is collected:
* the call count
* the minimal time for a single call
* the maximal time for a single call
* the total time of all call
* the average time of a call

Internally, the profiler will collect the data of many intervals and will dump them to CSV files at specific points in time,
especially at a multiplayer match begin and end.

## Working Principle

The profiler works by adding a Harmony `Prefix` and `Postfix` functions to each method you want to trace, and it adds them as outmermost pair
after all other prefixes / postfixes are applied. This means that it captures the time of the whole modified funtcion including
all prefixes and postfixes, and it also sees a call if a nother prefix skips the execution of the function. If you are interested 
in the costs of other patches' prefixes or postfixes, you can additionally also trace the `Prefix` or `Postfix` function of other
patches!

### A Word of WARNING

This code is a highly experimental hack. As with all such things, it works "well enough" for the cases it was intented for,
so I'm officially publishing it just in case someone else might make some use of it.

Note that you might as well consider using the Unity Profiler if that is available to you.

## Command-Line Options

To use the profiler, you have to enable it via the `olmod` command line. The following options are available:
* `-poor-mans-profiler`: Enable the profiler. If this option is not present, the profiler is completely disabled and has **no** runtime overhead whatsoever.
* `-pmp-interval <n>`: Set the measurement interval to `n` ticks
* `-pmp-output-path <path>`: Set the path for the output files. This can be absolute or relative, relative paths are considered relative to the
  persistent data dir of Overload (where the pilot files and savegames are stored)
* `-pmp-filter <file1>[:<file2>][:...]`: Load function name [filter](#selecting-the-methods-to-trace) files. Relative paths are searched against the olmod dir
   (where the `GameMod.dll` is located) and the persistent data dir of Overload (where the pilot files and savegames are stored). Multiple files can 
   be specified, separated by a colon, and will be processed in order.
* `-pmp-lazy`: Activate experimental lazy mode. The profiler is not activated at startup, but can later be activated by the `pmpinit` console command.
* `-pmp-locking <n>`: Set use of locking for thread-safety to off (`0`), on (`1`) or auto-detect (`-1`, the default). 
  It seems that servers crash without locking, but clients don't, so the auto-detect enables it just for servers.

## In-Game Console Commands

* `pmpcycle`: Start an new measurement cycle: dump the current cycle with all intervals as a `manual` cycle.
* `pmpinterval <n>`: Set the measurement interval to `n` ticks.
* `pmpinit`: Only if `-pmp-lazy` was used: Start the profiler now.

## Selecting the Methods to Trace

If no `-pmp-filter` command line option is specified, the profiler will look for a `pmp-filters.txt` file (in the same paths trhe `-pmp-filter` 
option looks). If no such file is found, the profiler will default to trace any function which was previously patched by olmod.

If a filter file is present, the profiler will walk through all methods of all types of a set of pre-defined dotnet assebmlies
(olmod's, the game's and Unity's) and apply the filters in-order. As soons as a match filter line (excluding or including this function)
is found, it is applied, and the search continues with the next method.

The filter file syntax is one entry per line, lines beginning with `#` are ignored as comments.
For each line, the fields _options_, _type_ and _method_ must be present, spearated by tabs.

The _options_ include a set of single-character flags:
* `+`: include this method if it matches
* `-`: exclude this methid if it matches
* `N`: no-op, this means this filter line will never match
* `=`: _type_ and _method_ names must **exactly** match
* `C`: _type_ and _method_ names must be contained in the function to match
* `R`: _type_ and _method_ are regular expressions (this is really useful!)
* `*`: matches every function (useful in combination with `p)
* `p`: match only previously patched functions
* `a`: match any function (as opposed to `p`)
Default is `+Ca`.

An example [`pmp-filters.txt`](/PoorMansProfiler/pmp-filters.txt) file is provided here.

## Output Format

The profiler will write a CSV file (with tab as the separator) for each of the statistic channels.
The data is organized such that the columns are the intervals, and the rows represent the functions.
The first column is the method hash (a fairly random number), and the last column the function name.

There is also an `info.csv` file generated which just lists the mapping between index,
hash and method name.

Additionally to the method data, two synthetic entries are added:
* `-7777` `+++PMP-Frametime` represents the `Time.unscaledDeltaTime` at each Unity `Update` step
* `-7778` `+++PMP-Interval` represents a single profiler measurement interval

### Post-Processing 

The data format of the CSV tables can be quite unhandy. Provided in this distribution is the
`pmp_heatmap.c` utility (Makefile for Linux and porject file for VisualStudio on Windows
is provided) which can generate heatmap PNG visualizations of the CSVs, as well as
"transpose" the CSVs so that the functions are the columns and the intervals are the rows,
which makes it easier to process the data in spreadsheet-like applications...

The syntax is: `pmp_heat [options] file1.csv file2.csv ...`

Options are:
* `-scale <s>`: apply factor `s` to the automatically determined scale factor
* `-force <s>`: force the scale factor `s`, no auto-detection
* `-max <m>`: use this as maximum for the scale (after applying potential offset)
* `-offset <o>`: subtract `o` from the values before appliing max/scale
* `-start <n>`: ignore the first `n` intervals in automatic max finding (default: 1)
* `-end <n>`: ignore the last `n` intervals in automatic max finding (default: 1)
* `-second <n>`: if `n` is 0, use the maximum found, if `n` is 1, use the second-max
* `-show <th>`: Show only functions above (or below, if negative) the given threshold `sh`. No other output in this mode.
  
By default, some `fileN.csvXY.png` files, where `X` is
* `0`: find max for all functions, except `-7778 +++PMP-Interval`
* `1`: find max for all functions, except `-7778 +++PMP-Interval` and `-7777 +++PMP-Frametime`

and `Y` is

* `0`: find max over all functions and intervals
* `1`: find max for each function separately, over all intervals
* `2`: find max for each interval separately, over all functions

Furthermore `file1.csv__.ctr` is generated, which is the transposed CSV file.

If `-force` or `-max` is specified, automatic maximum calculation is disabled, and only one output file
is generated, with the scale as specified.
  
Example: suppose you captured intervals of 12 ticks (worth 200ms of game time each). You could use

```
pmp_heat -offset 2 -max 48 ..._TotalTime.csv
```

to generate a single heatmap where everything below 2 ms (1%)
is black, everything above 50ms is white, and the color gradient green-cyan-blue-violet-red to show
the costs in-between.
  
# Potential Improvements

These are mainly a reminder for myself for stuff to change, should the need ever arise...
* Switch to a transpiler instead of using predix-suffix-pairs. This allows to do the `MethodBase` lookup at patch 
  time, instead having to do it at runtime for each call. I could then use a simple array instead of a dirctionary.
  Since the prefixes and postfixes of other patches can be transpiled too, the total cost including the 
  pacthes can still be captured.
* Don't accumulate the data into intervals. Use a single indexed buffer. At each begin and end of a traced function,
  just do an atomic increment on the array index. The array would conatin etires wth the function index, some flags, and the
  ticks. We could have some really big (talking gigabyte range) array pre-allocated, and could dump the binary data
  instead of the CSVs.  This approach would also nicely work with different threads, and recursive function calls.
  
