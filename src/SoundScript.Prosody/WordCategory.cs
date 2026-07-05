namespace SoundScript.Prosody;

/// <summary>Coarse lexical category driving a word's base prosodic pitch band.</summary>
public enum WordCategory
{
    /// <summary>Nouns, verbs, adjectives, adverbs — carry the sentence's meaning and stress.</summary>
    Content,

    /// <summary>Articles, prepositions, conjunctions, pronouns, auxiliaries — typically unstressed.</summary>
    Function
}
