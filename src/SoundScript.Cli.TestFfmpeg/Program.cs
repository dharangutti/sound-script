// Test-only process double. Never referenced or packaged by the CLI.
var mode = Environment.GetEnvironmentVariable("SS_TEST_FFMPEG_MODE");
if (args.Contains("-version")) { Console.WriteLine("FFmpeg test double"); return 0; }
if (args.Contains("-encoders"))
{
    if (mode != "missing-codec") Console.WriteLine(" V..... libvpx-vp9 test\n A..... libopus test");
    return 0;
}
if (args.Contains("-muxers")) { Console.WriteLine(" E webm test"); return 0; }
if (args.Contains("-c:v"))
{
    File.WriteAllText(args[^1], "encoded test container");
    return mode == "encode-failure" ? 8 : 0;
}
return mode == "decode-failure" ? 9 : 0;
