namespace ClosedExhaustiveness.NotFlagged;

using Flagged;

// Rider correctly reports nothing on any of these switches. Each differs from Flagged.cs in one way.

// 1. The same Either, switched outside the class, over an open generic.
public static class OutsideOpenGeneric
{
    public static string Describe<TLeft, TRight>(Either<TLeft, TRight> either) => either switch
    {
        Either<TLeft, TRight>.Left => "left",
        Either<TLeft, TRight>.Right => "right"
    };
}

// 2. The same Either, switched outside the class, over a constructed type.
public static class OutsideConstructed
{
    public static string Describe(Either<string, int> either) => either switch
    {
        Either<string, int>.Left(var error) => error,
        Either<string, int>.Right(var value) => value.ToString()
    };
}

// 3. A generic closed class switched inside the class, but with top-level subclasses.
public closed class Result<TError, TValue>
{
    public string Describe() => this switch
    {
        Failure<TError, TValue> => "failure",
        Success<TError, TValue> => "success"
    };
}

public sealed class Failure<TError, TValue> : Result<TError, TValue>;

public sealed class Success<TError, TValue> : Result<TError, TValue>;

// 4. Nested subclasses switched inside the class, but the closed class isn't generic.
public closed class Gate
{
    public sealed class Open : Gate;

    public sealed class Shut : Gate;

    public string Describe() => this switch
    {
        Open => "open",
        Shut => "shut"
    };
}

// 5. A non-generic closed class with top-level subclasses: the case in RSRP-504103.
public closed class Light;

public sealed class On : Light;

public sealed class Off : Light;

public static class LightDescription
{
    public static string Of(Light light) => light switch
    {
        On => "on",
        Off => "off"
    };
}
