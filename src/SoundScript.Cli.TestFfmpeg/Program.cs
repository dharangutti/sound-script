// Test-only process double. Never referenced or packaged by the CLI.
if (args.FirstOrDefault() == "--process-test")
{
    switch (args[1])
    {
        case "echo": Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(args.Skip(2))); return 0;
        case "flood":
            await Task.WhenAll(Console.Out.WriteAsync(new string('o', 262144)), Console.Error.WriteAsync(new string('e', 262144)));
            return 7;
        case "sleep":
            File.WriteAllText(args[2], Environment.ProcessId.ToString());
            await Task.Delay(TimeSpan.FromMinutes(2)); return 0;
        case "tree":
            var start = new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false };
            start.ArgumentList.Add("--process-test"); start.ArgumentList.Add("sleep"); start.ArgumentList.Add(args[2]);
            using (var child = System.Diagnostics.Process.Start(start)!) await child.WaitForExitAsync();
            return 0;
    }
    return 99;
}
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
