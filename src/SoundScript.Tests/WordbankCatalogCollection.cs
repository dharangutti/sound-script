using Xunit;

namespace SoundScript.Tests;

// The catalog tests replace process-wide locale packs. Serializing just those
// classes with each other still races with ordinary prosody/timbre readers.
// Run this mutating collection apart from all other collections so synthesis
// keeps the same assets and locale for an entire render.
[CollectionDefinition("WordbankCatalog", DisableParallelization = true)]
public sealed class WordbankCatalogCollection;
