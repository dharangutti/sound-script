# After the discoverability workbench

The public gallery and optional private local media/parameter workbench are
implemented. Both reuse the native compiler, immutable snapshots, frame inspector
and FFmpeg renderer. See [usage and limits](README.md).

Next, try small real compositions and measure latency and source compatibility.
Use those results to decide whether a separate repository and optimized backend
are justified. Retain the reference backend and repeat/decode tests as the oracle.

Script editing, independent multi-file clip slots, browser-native rendering,
persistent projects and editable timelines remain outside this milestone. The
current local UI selects approved examples and one video/music substitution.
