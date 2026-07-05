namespace SoundScript.Prosody;

/// <summary>Coarse sentence type driving the phrase-level pitch contour.</summary>
public enum SentenceType
{
    /// <summary>Declarative — contour trends downward toward the end.</summary>
    Statement,

    /// <summary>Interrogative — contour trends upward toward the end.</summary>
    Question,

    /// <summary>Comma-separated list item — contour steps upward per item.</summary>
    ListItem
}
