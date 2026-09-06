namespace SoundScript.Playground;

public sealed record VisualPreset(string Key, string Title, string Description, string ExampleFile)
{
    public string Category { get; init; } = "Getting started";
    public string Source
    {
        get
        {
            using var stream = typeof(VisualPresetCatalog).Assembly.GetManifestResourceStream($"VisualExamples.{ExampleFile}")
                ?? throw new InvalidOperationException($"Missing visual example: {ExampleFile}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }
    }
}

public static class VisualPresetCatalog
{
    public static IReadOnlyList<VisualPreset> All { get; } =
    [
        new("visual-temporal", "Audio/Visual showcase", "A 12-second piano score with sequential cues, overlap, and fades.", "visual-temporal.ssv"),
        new("visual-motion", "Moving orb", "Position and radius animation with a rotating sparkle over piano.", "visual-motion.ssv"),
        new("visual-story", "Ready, Go, Done", "Named cards, a deliberate pause, resizing, and fades over a seven-second score.", "visual-story.ssv"),
        new("visual-overlays", "Layered product cue", "Overlapping card, orb, and star with millisecond placement and size animation.", "visual-overlays.ssv"),
        new("visual-progress", "Progress indicator", "A growing bar, outlined rounded panel, and portable text label over piano.", "visual-progress.ssv"),
        new("visual-process", "Process diagram", "Two labelled panels connected by an animated arrow, with a line divider.", "visual-process.ssv"),
        new("visual-instructions", "Instruction sequence", "A rotating triangle and three timed text instructions, synchronized with piano.", "visual-instructions.ssv"),
        new("visual-status", "Status display", "A ring and circle indicator beside a rotating ellipse and text label.", "visual-status.ssv"),
        new("visual-org-chart", "Team responsibilities", "A light organization chart with staged teams and musical entry cues.", "visual-org-chart.ssv") { Category = "Diagrams" },
        new("visual-delivery-flow", "Request to delivery", "Three process stages with captions and seeded synthetic speech cues.", "visual-delivery-flow.ssv") { Category = "Diagrams" },
        new("visual-sequence-diagram", "Cache request sequence", "Actors, lifelines, request and return arrows with musical cues.", "visual-sequence-diagram.ssv") { Category = "Diagrams" },
        new("visual-architecture", "Service boundaries", "Nested containers, connectors and timed synthetic speech cues.", "visual-architecture.ssv") { Category = "Diagrams" },
        new("visual-block-diagram", "Signal conditioning", "A moving signal through sensor, filter and output blocks.", "visual-block-diagram.ssv") { Category = "Diagrams" },
        new("visual-workflow", "Review and revision", "Approval flow with a visible feedback path.", "visual-workflow.ssv") { Category = "Diagrams" },
        new("visual-network", "Dependency map", "Shared dependencies and an outline highlight without obscuring labels.", "visual-network.ssv") { Category = "Diagrams" },
        new("visual-release-timeline", "Release milestones", "Design, beta and release milestones introduced over music.", "visual-release-timeline.ssv") { Category = "Business and dashboards" },
        new("visual-status-dashboard", "Service health", "A compact status snapshot with an alert cue for a degraded worker.", "visual-status-dashboard.ssv") { Category = "Business and dashboards" },
        new("visual-progress-dashboard", "Deployment progress", "Three left-anchored progress bars completing in order.", "visual-progress-dashboard.ssv") { Category = "Business and dashboards" },
        new("visual-information-cards", "Support handoff", "Owner, priority and next action in a reusable light card composition.", "visual-information-cards.ssv") { Category = "Business and dashboards" },
        new("visual-comparison", "Latency comparison", "Before and after bars on the same scale.", "visual-comparison.ssv") { Category = "Business and dashboards" },
        new("visual-kpi", "Delivery scorecard", "Three authored metrics with targets; no live data connection.", "visual-kpi.ssv") { Category = "Business and dashboards" },
        new("visual-startup-explainer", "Device startup", "Step captions, symbols and synthetic check/connect/start cues.", "visual-startup-explainer.ssv") { Category = "Explainers and presentations" },
        new("visual-title-captions", "Experiment title sequence", "Three full-screen messages with a short musical opening.", "visual-title-captions.ssv") { Category = "Explainers and presentations" },
        new("visual-presentation", "Quarterly review", "Three timed slides with captions and synthetic speech cues.", "visual-presentation.ssv") { Category = "Explainers and presentations" },
        new("visual-step-by-step", "Incident response", "Detect, triage and resolve with coordinated border highlights.", "visual-step-by-step.ssv") { Category = "Explainers and presentations" },
        new("visual-education", "Distance, speed and time", "A moving marker, metre scale and one-second musical ticks.", "visual-education.ssv") { Category = "Explainers and presentations" },
        new("visual-mixed-audio", "Score, Wave and Voice", "Score notes, voice/sing, speak and a Wave effect share one PCM mix. Voice is synthetic.", "visual-mixed-audio.ssv") { Category = "Audio and scale studies" },
        new("visual-scale-study", "48-service rollout", "Repeated cards and six timed batches; a larger composition for inspection and export.", "visual-scale-study.ssv") { Category = "Audio and scale studies" },
    ];
}
