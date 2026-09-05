namespace SoundScript.Core.Ast;

/// <summary>
/// Declares a synchronization marker between the visual narrative cursor and
/// SoundScript's audio clock. It does not play audio or advance either clock.
/// </summary>
public sealed record AudioSyncNode : AstNode;
