using System.Text.Json;
using System.Text.Json.Nodes;

namespace VideoLab;

internal static class EditingProof
{
    internal static async Task Run()
    {
        var checks = new List<string>();
        void Check(bool value, string name) { if (!value) throw new InvalidOperationException("FAIL: " + name); checks.Add(name); }
        void Reject(Action action, string name) { try { action(); } catch (ArgumentException) { checks.Add(name); return; } throw new InvalidOperationException("FAIL: " + name); }
        var root = Path.GetFullPath("artifacts/editing-proof"); Directory.CreateDirectory(root);
        string source = """
        {"width":160,"height":100,"fps":30,"frames":8,
         "parameters":{"visible":{"default":1,"min":0,"max":1},"alpha":{"default":1,"min":0,"max":1}},
         "videos":[],"audio":[],"shapes":[],
         "texts":[{"text":"HI","at":1,"frames":6,"width":128,"height":32,"fontSize":16,
           "when":"visible > 0.5","transform":{"x":8,"opacity":"alpha"}}],
         "callouts":[{"label":"Check","at":1,"frames":6,"width":128,"height":60,"fontSize":12,
           "targetX":100,"targetY":54,"transform":{"x":8,"y":36}}]}
        """;
        Composition Compile(string text) => Composition.Compile(text, root);
        string Change(Action<JsonNode> change) { var n = JsonNode.Parse(source)!; change(n); return n.ToJsonString(); }
        var composition = Compile(source); var runtime = composition.CreateRuntime(); var a = runtime.Bind();
        Check(a.SceneAt(0).Layers.Length == 0 && a.SceneAt(1).Layers.Length == 2 && a.SceneAt(7).Layers.Length == 0, "Caption half-open lifetimes");
        Check(a.SceneAt(2).Layers.Select(l => l.Kind).SequenceEqual(["text", "callout"]), "Text and callout stable layer order");
        Check(a.SceneAt(2).Layers[0].Caption?.FontSha256 == Captions.FontHash, "Controlled font identity exposed in scene");
        runtime.SetMany(new Dictionary<string, decimal> { ["visible"] = 0, ["alpha"] = 0.5m }); var b = runtime.Bind();
        Check(ReferenceEquals(a.Composition,b.Composition) && a.SceneAt(2).Layers[0].Included && !b.SceneAt(2).Layers[0].Included && b.SceneAt(2).Layers[0].Transform.Opacity == 0.5m, "Caption conditions and numeric bindings preserve snapshots");
        Check(JsonSerializer.Serialize(a.SceneAt(5)) == JsonSerializer.Serialize(a.SceneAt(5)) && a.SceneAt(1).Layers[0].Caption!.Text == "HI", "Caption frame inspection is random access");
        var moving = Compile(Change(n => n["texts"]![0]!["transform"]!["x"] = JsonNode.Parse("{\"keys\":[{\"frame\":0,\"value\":8},{\"frame\":5,\"value\":28}]}"))).CreateRuntime().Bind();
        Check(moving.SceneAt(1).Layers[0].Transform.X == 8 && moving.SceneAt(6).Layers[0].Transform.X == 28, "Caption animation uses local endpoints");
        var transformed = Compile(Change(n => { n["callouts"]![0]!["transform"]!["scale"] = 0.8; n["callouts"]![0]!["transform"]!["rotation"] = 15; })).CreateRuntime().Bind();
        Check(transformed.SceneAt(2).Layers[1].Transform.ScaleX == 0.8m && transformed.SceneAt(2).Layers[1].Transform.Rotation == 15, "Callout uses shared scale and rotation semantics");
        var fontCopies = Ffmpeg.Plan(a,"static.mp4").FilterGraph.Split("drawtext=").Length - 1;
        Check(fontCopies == 2, "Each static caption plane is constructed once before per-frame transforms");
        foreach (var (name, change) in new (string, Action<JsonNode>)[] {
            ("font lower bound",n=>n["texts"]![0]!["fontSize"]=7), ("font upper bound",n=>n["texts"]![0]!["fontSize"]=97),
            ("odd plane",n=>n["texts"]![0]!["width"]=127), ("plane bound",n=>n["texts"]![0]!["width"]=4098),
            ("font fits plane",n=>n["texts"]![0]!["fontSize"]=24), ("empty text",n=>n["texts"]![0]!["text"]=""),
            ("text length",n=>n["texts"]![0]!["text"]=new string('A',161)), ("unsupported glyph",n=>n["texts"]![0]!["text"]="🙂"),
            ("control characters",n=>n["texts"]![0]!["text"]="A\nB"), ("alignment",n=>n["texts"]![0]!["align"]="justify"),
            ("color injection",n=>n["texts"]![0]!["color"]="FFFFFF,negate"), ("background injection",n=>n["callouts"]![0]!["background"]="red"),
            ("target outside",n=>n["callouts"]![0]!["targetX"]=128), ("target behind label",n=>n["callouts"]![0]!["targetY"]=1),
            ("caption span",n=>n["texts"]![0]!["frames"]=9), ("caption unknown field",n=>n["texts"]![0]!["fontfile"]="host.ttf"),
            ("null caption",n=>n["texts"]![0]=null), ("caption numeric condition",n=>n["texts"]![0]!["when"]="1") })
            Reject(()=>Compile(Change(change)), "Reject " + name);
        Reject(()=>Compile(Change(n=>{var list=new JsonArray();for(int i=0;i<65;i++)list.Add(n["texts"]![0]!.DeepClone());n["texts"]=list;})), "Captions count toward element cap");
        var plan = Ffmpeg.Plan(a, Path.Combine(root,"pure.mp4"));
        Check(plan.GeneratedText?.Length == 2 && plan.FilterGraph.Contains("expansion=none") && !Directory.EnumerateFileSystemEntries(root,"*.assets").Any(), "Pure plan declares generated text without file I/O");
        foreach (var format in new[] { "mp4", "webm" })
        {
            string first=Path.Combine(root,"caption."+format),repeat=Path.Combine(root,"repeat."+format);
            await Ffmpeg.Render(a,first);await Ffmpeg.Render(a,repeat);
            Check(Ffmpeg.Hash(first)==Ffmpeg.Hash(repeat), "Caption " + format + " repeated bytes");
            var raw=Path.Combine(root,format+".rgb");await Ffmpeg.Run("ffmpeg",["-v","error","-xerror","-y","-i",first,"-f","rawvideo","-pix_fmt","rgb24",raw]);
            var bytes=File.ReadAllBytes(raw);Check(bytes.Length==160*100*3*8, "Caption " + format + " decoded frame count");
            int At(int frame,int x,int y,int channel=0)=>bytes[((frame*100+y)*160+x)*3+channel];
            Check(At(0,20,12)<20 && At(7,20,12)<20 && Enumerable.Range(16,25).Any(x=>At(2,x,12)>180), "Rendered glyphs obey lifetime in " + format);
            Check(At(2,108,90)>170 && At(2,100,90)<60 && At(2,9,38,2)>25, "Rendered callout background and pointer in " + format);
        }
        int[] bounds = new int[3]; int index=0;
        foreach (var align in new[]{"left","center","right"})
        {
            var aligned=Compile(Change(n=>n["texts"]![0]!["align"]=align)).CreateRuntime().Bind();var file=Path.Combine(root,align+".mp4");
            await Ffmpeg.Render(aligned,file);var raw=Path.Combine(root,align+".rgb");
            await Ffmpeg.Run("ffmpeg",["-v","error","-y","-i",file,"-vf","select=eq(n\\,2)","-frames:v","1","-f","rawvideo","-pix_fmt","rgb24",raw]);
            var bytes=File.ReadAllBytes(raw);bounds[index++]=Enumerable.Range(0,160).First(x=>Enumerable.Range(8,20).Any(y=>bytes[(y*160+x)*3]>190));
        }
        Check(bounds[0]<bounds[1]-25 && bounds[1]<bounds[2]-25, "Decoded left/center/right glyph alignment");
        var hidden=Path.Combine(root,"hidden.mp4");await Ffmpeg.Render(b,hidden);var hiddenRaw=Path.Combine(root,"hidden.rgb");
        await Ffmpeg.Run("ffmpeg",["-v","error","-y","-i",hidden,"-vf","select=eq(n\\,2)","-frames:v","1","-f","rawvideo","-pix_fmt","rgb24",hiddenRaw]);
        var hiddenBytes=File.ReadAllBytes(hiddenRaw);
        Check(Enumerable.Range(0,160*32*3).All(i=>hiddenBytes[i]<25), "Condition removes title pixels while retaining callout");
        runtime.SetMany(new Dictionary<string,decimal>{{"visible",1},{"alpha",0}});
        var transparent=Path.Combine(root,"transparent.mp4");await Ffmpeg.Render(runtime.Bind(),transparent);
        var transparentRaw=Path.Combine(root,"transparent.rgb");await Ffmpeg.Run("ffmpeg",["-v","error","-y","-i",transparent,"-vf","select=eq(n\\,2)","-frames:v","1","-f","rawvideo","-pix_fmt","rgb24",transparentRaw]);
        Check(File.ReadAllBytes(transparentRaw).Take(160*32*3).All(v=>v<25), "Runtime opacity removes title pixels");
        const string literal="%{eif:1/0:d} ':,;[]\\";
        var injected=Compile(Change(n=>n["texts"]![0]!["text"]=literal)).CreateRuntime().Bind();
        Check(!Ffmpeg.Plan(injected,"literal.mp4").FilterGraph.Contains(literal), "Authored punctuation never enters filter syntax");
        await Ffmpeg.Render(injected,Path.Combine(root,"literal.mp4"));checks.Add("Filter-like text renders literally without expansion");
        var unusual=Path.Combine(root,"spaces ' and ; punctuation");Directory.CreateDirectory(unusual);
        await Ffmpeg.Render(a,Path.Combine(unusual,"caption.mp4"));
        Check(Ffmpeg.Hash(Path.Combine(unusual,"caption.mp4"))==Ffmpeg.Hash(Path.Combine(root,"caption.mp4")), "Generated resource paths are safe with output-directory punctuation");
        var preserved=Path.Combine(root,"cancelled.mp4");File.WriteAllText(preserved,"keep-existing");using var cancelled=new CancellationTokenSource();cancelled.Cancel();
        try { await Ffmpeg.Render(a,preserved,cancelled.Token); throw new InvalidOperationException("Cancellation was ignored"); } catch(OperationCanceledException) { }
        Check(File.ReadAllText(preserved)=="keep-existing" && !Directory.EnumerateFileSystemEntries(root,".videolab-*").Any(), "Cancellation protects destination and cleans caption/font resources");
        await File.WriteAllTextAsync(Path.Combine(root,"report.json"),JsonSerializer.Serialize(new{checks=checks.Count,passed=checks}));
        Console.WriteLine($"PASS: {checks.Count} Phase 1 editing checks.");
    }
}
