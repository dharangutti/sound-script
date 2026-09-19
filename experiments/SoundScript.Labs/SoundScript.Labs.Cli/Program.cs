using System.Text;
using SoundScript.Labs;
using SoundScript.Labs.Output;
using SoundScript.Labs.Syntax;

return LabsCommand.Run(args, Console.Out, Console.Error);

public static class LabsCommand
{
    public static int Run(string[] args, TextWriter output, TextWriter error)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            output.WriteLine("SoundScript.Labs - EXPERIMENTAL, SIMULATOR ONLY\nUsage: <source.sslabs> --out <new-directory>");
            return 0;
        }
        if (args.Length != 3 || args[1] != "--out")
        {
            error.WriteLine("Usage: <source.sslabs> --out <new-directory>");
            return 2;
        }
        string? staging = null;
        try
        {
            var sourceInfo = new FileInfo(args[0]);
            if (sourceInfo.Length > LabsParser.MaxSourceLength * 4L)
                throw new LabsException("Source file exceeds the Labs size limit.");
            var source = File.ReadAllText(args[0], new UTF8Encoding(false, true));
            var files = ResultExporter.Export(ExperimentRunner.Run(source));
            string destination = Path.GetFullPath(args[2]);
            if (Directory.Exists(destination) || File.Exists(destination))
                throw new IOException("Output already exists. Choose a new directory; Labs does not overwrite results.");
            string parent = Path.GetDirectoryName(destination) ?? throw new IOException("Invalid output directory.");
            Directory.CreateDirectory(parent);
            staging = Path.Combine(parent, ".labs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            foreach (var (name, bytes) in files) File.WriteAllBytes(Path.Combine(staging, name), bytes);
            Directory.Move(staging, destination);
            staging = null;
            output.WriteLine($"EXPERIMENTAL simulation complete: {destination}");
            return 0;
        }
        catch (Exception ex) when (ex is LabsException or ArgumentException or IOException or UnauthorizedAccessException)
        {
            error.WriteLine($"SoundScript.Labs: {ex.Message}");
            return 1;
        }
        finally
        {
            // Only the uniquely named directory created by this invocation is eligible for cleanup.
            if (staging is not null && Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
        }
    }
}
