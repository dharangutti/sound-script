using System.Text;
using System.Text.RegularExpressions;

namespace VideoLab;

// Literal caption data travels separately from FFmpeg syntax. The same transparent
// plane and existing transform pipeline serve titles and text+shape callouts.
public sealed record CaptionStyle(string Text, int FontSize, string Color, string Align,
    string? Background = null, int? TargetX = null, int? TargetY = null)
{
    public string Font => "DejaVu Sans 2.37";
    public string FontSha256 => Captions.FontHash;
}
public sealed record CaptionResource(string File, string Text);

internal static class Captions
{
    internal const string FontHash = "7DA195A74C55BEF988D0D48F9508BD5D849425C1770DBA5D7BFC6CE9ED848954";
    internal static void Color(string color) => Composition.Require(Regex.IsMatch(color ?? "", "^[0-9a-fA-F]{6}$"), "Caption color must be six hex digits.");
    internal static void Validate(string text, int width, int height, int size, string color, string align)
    {
        Composition.Require(!string.IsNullOrWhiteSpace(text) && text.Length <= 160 && text.All(c => c is >= ' ' and <= '~'),
            "Caption needs 1–160 single-line printable ASCII characters (no control characters or font fallback).");
        Composition.Require(width is >= 24 and <= 4096 && height is >= 24 and <= 4096 && width % 2 == 0 && height % 2 == 0,
            "Caption plane must have even dimensions in [24,4096].");
        Composition.Require(size is >= 8 and <= 96 && size + 16 <= height, "Caption fontSize must be 8–96 and fit inside height minus 16.");
        Color(color); Composition.Require(align is "left" or "center" or "right", "Caption align must be left, center or right.");
    }
    internal static string FileName(int z) => $"text-{z}.txt";
    internal static string Source(VisualProgram program, int fps)
    {
        var c = program.Caption!; int w = program.BaseWidth, h = program.BaseHeight;
        string source = $"color=c=black@0:s={w}x{h}:r={fps},trim=end_frame=1,setpts=PTS-STARTPTS,format=rgba";
        if (c.Background != null)
        {
            // A bounded line segment in local plane coordinates. All filter values
            // are generated numeric constants, never authored expression strings.
            int x = w / 2, y = c.FontSize + 16, dx = c.TargetX!.Value - x, dy = c.TargetY!.Value - y;
            string t = $"clip(((X-{x})*{dx}+(Y-{y})*{dy})/{dx * dx + dy * dy},0,1)";
            string distance = $"(X-({x}+({t})*{dx}))^2+(Y-({y}+({t})*{dy}))^2";
            int r = Convert.ToInt32(c.Color[..2], 16), g = Convert.ToInt32(c.Color[2..4], 16), b = Convert.ToInt32(c.Color[4..], 16);
            source += $",geq=r={r}:g={g}:b={b}:a='if(lte({distance},2.25),255,0)',format=rgba";
            source += $",drawbox=x=0:y=0:w={w}:h={y}:color=0x{c.Background}:t=fill:replace=1";
        }
        string left = c.Align switch { "center" => "(w-text_w)/2", "right" => "w-text_w-8", _ => "8" };
        // Relative filenames are safe generated names resolved in Render's private
        // working directory. Literal text (including quotes, %, :, [ ]) is file data.
        return source + $",drawtext=fontfile=font.ttf:textfile={FileName(program.ZOrder)}:expansion=none:text_shaping=0:ft_load_flags=no_hinting+no_bitmap:fontsize={c.FontSize}:fontcolor=0x{c.Color}:x={left}:y=8";
    }
    internal static async Task Materialize(string directory, CaptionResource[] resources)
    {
        Directory.CreateDirectory(directory);
        await using var font = typeof(Captions).Assembly.GetManifestResourceStream("VideoLab.DejaVuSans.ttf")
            ?? throw new InvalidOperationException("Bundled VideoLab font is missing.");
        var file = Path.Combine(directory, "font.ttf");
        await using (var output = File.Create(file)) await font.CopyToAsync(output);
        Composition.Require(Ffmpeg.Hash(file) == FontHash, "Bundled font fingerprint differs from the validated font.");
        foreach (var resource in resources)
            await File.WriteAllTextAsync(Path.Combine(directory, resource.File), resource.Text, new UTF8Encoding(false));
    }
}
