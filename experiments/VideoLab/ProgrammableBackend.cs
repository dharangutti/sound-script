namespace VideoLab;

// A bounded reference lowering: evaluate first, then emit numeric operations. Optimizations
// may group equal states later, but must preserve this SceneAt-based meaning exactly.
internal static class ProgrammableBackend
{
    internal static RenderPlan Plan(Snapshot snapshot, string output)
    {
        var c = snapshot.Composition; var s = c.Script;
        var scenes = Enumerable.Range(0, s.Frames).Select(snapshot.SceneAt).ToArray();
        var inputs = c.Clips.Select(v => Path.GetFullPath(v.Asset, c.AssetDirectory))
            .Concat(s.Audio.Select(a => Path.GetFullPath(a.Asset, c.AssetDirectory))).ToArray();
        var graph = new List<string>();
        string N(decimal n) => Ffmpeg.Number(n);
        for (int i = 0; i < c.Clips.Length; i++)
        {
            var frames = scenes.SelectMany((scene, frame) => scene.Layers.Where(l => l.Id == $"video[{i}]" && l.Included).Select(_ => frame)).ToArray();
            string normalize = $"[{i}:v:0]setpts=PTS-STARTPTS,fps={s.Fps},scale={s.Width}:{s.Height}:force_original_aspect_ratio=decrease,pad={s.Width}:{s.Height}:(ow-iw)/2:(oh-ih)/2,setsar=1,format=rgba";
            graph.Add(frames.Length == 0 ? normalize + ",nullsink" : normalize + $",split={frames.Length}" + string.Concat(frames.Select(f => $"[source{i}_{f}]")));
        }
        for (int frame = 0; frame < s.Frames; frame++)
        {
            string current = $"base{frame}";
            graph.Add($"color=c=black:s={s.Width}x{s.Height}:r={s.Fps},trim=end_frame=1,setpts=PTS-STARTPTS,format=rgba[{current}]");
            foreach (var layer in scenes[frame].Layers.Where(l => l.Included))
            {
                var program = c.Visuals[layer.ZOrder]; var t = layer.Transform; var r = t.Raster();
                string id = $"f{frame}l{layer.ZOrder}";
                string source = layer.Kind == "video"
                    ? $"[source{layer.ZOrder}_{frame}]trim=start_frame={layer.SourceFrame}:end_frame={layer.SourceFrame + 1},setpts=PTS-STARTPTS"
                    : $"color=c=0x{layer.Source}:s={program.BaseWidth}x{program.BaseHeight}:r={s.Fps},trim=end_frame=1,setpts=PTS-STARTPTS,format=rgba";
                // Normalized crop refers to the untransformed layer plane. Floor the edges,
                // then resize, rotate, and place the chosen anchor at the evaluated x/y.
                int cx = (int)(t.Crop.X * program.BaseWidth), cy = (int)(t.Crop.Y * program.BaseHeight);
                int cw = Math.Max(1, (int)(t.Crop.Width * program.BaseWidth)), ch = Math.Max(1, (int)(t.Crop.Height * program.BaseHeight));
                graph.Add(source + $",crop={cw}:{ch}:{cx}:{cy}:exact=1,scale={r.Width}:{r.Height}:flags=neighbor,format=rgba,rotate={N(t.Rotation * (decimal)Math.PI / 180)}:ow={r.RotatedWidth}:oh={r.RotatedHeight}:c=none:bilinear=0,colorchannelmixer=aa={N(t.Opacity)}[{id}]");
                graph.Add($"[{current}][{id}]overlay=x={r.Left}:y={r.Top}:format=rgb:shortest=1[{id}out]");
                current = id + "out";
            }
            graph.Add($"[{current}]format=yuv420p[frame{frame}]");
        }
        graph.Add(string.Concat(Enumerable.Range(0, s.Frames).Select(f => $"[frame{f}]")) + $"concat=n={s.Frames}:v=1:a=0,setpts=N/({s.Fps}*TB),fps={s.Fps}[vout]");
        long totalSamples = (long)s.Frames * 48000 / s.Fps; int block = 48000 / s.Fps;
        graph.Add($"anullsrc=r=48000:cl=stereo,atrim=end_sample={totalSamples}[silence]");
        string mixed = "[silence]";
        for (int i = 0; i < s.Audio.Length; i++)
        {
            var audio = s.Audio[i]; var program = c.AudioPrograms[i];
            graph.Add($"[{c.Clips.Length + i}:a:0]asetpts=PTS-STARTPTS,aresample=48000,aformat=sample_fmts=fltp:channel_layouts=stereo,asplit={audio.Frames}" + string.Concat(Enumerable.Range(0, audio.Frames).Select(f => $"[as{i}_{f}]")));
            for (int f = 0; f < audio.Frames; f++)
            {
                var state = scenes[audio.At + f].Audio.Single(a => a.Id == program.Id);
                graph.Add($"[as{i}_{f}]atrim=start_sample={((long)audio.Trim + f) * block}:end_sample={((long)audio.Trim + f + 1) * block},asetpts=PTS-STARTPTS,volume={N(state.Included ? state.Gain : 0)}[ab{i}_{f}]");
            }
            graph.Add(string.Concat(Enumerable.Range(0, audio.Frames).Select(f => $"[ab{i}_{f}]")) + $"concat=n={audio.Frames}:v=0:a=1,adelay={(long)audio.At * block}S:all=1[audio{i}]");
            mixed += $"[audio{i}]";
        }
        graph.Add(mixed + $"amix=inputs={s.Audio.Length + 1}:duration=first:normalize=0:dropout_transition=0,alimiter=limit=0.95:level=0:latency=1,atrim=end_sample={totalSamples}[aout]");
        return Ffmpeg.Assemble(s, output, inputs, graph);
    }
}
