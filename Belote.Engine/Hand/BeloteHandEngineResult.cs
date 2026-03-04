namespace Belote.Engine.Hand;

public sealed record EngineEvent(string Code, IReadOnlyDictionary<string, string> Parameters);

public sealed record BeloteHandEngineResult(BeloteHandState State, IReadOnlyList<EngineEvent> Events);

public sealed record EngineRejection(string Code, string Message);

