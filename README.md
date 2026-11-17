# Rider: false CS8509 on a switch inside a generic closed class

Rider reports CS8509, "The 'switch' expression does not handle all possible inputs (it is not exhaustive)", on a
switch that the C# 15 compiler accepts as exhaustive. It happens when the switch is written inside a generic type, over
a `closed` class that is declared, with its subclasses, inside that generic type. The closed class can be the generic
type itself, as `Either<TLeft, TRight>` is, or a class nested in it that isn't generic itself.

[RSRP-504103](https://youtrack.jetbrains.com/issue/RSRP-504103) reported the same false positive for a non-generic
closed class with top-level subclasses, and is marked fixed. That case is no longer flagged (see
[`NotFlagged.cs`](src/NotFlagged.cs), case 5), but this one still is.

## Versions

- Rider 2026.3 (build 263.7307.258), macOS
- .NET SDK 11.0.100, `LangVersion` 15

## Steps to reproduce

1. Open `ClosedExhaustiveness.csproj` in Rider.
2. Open [`src/Flagged.cs`](src/Flagged.cs).

```csharp
public closed class Either<TLeft, TRight>
{
    public sealed class Left(TLeft value) : Either<TLeft, TRight>
    {
        public void Deconstruct(out TLeft left) => left = value;
    }

    public sealed class Right(TRight value) : Either<TLeft, TRight>
    {
        public void Deconstruct(out TRight right) => right = value;
    }

    public static explicit operator TLeft(Either<TLeft, TRight> either) => either switch
    {
        Left(var left) => left,
        Right => throw new InvalidCastException("It is a Right.")
    };
}
```

The same happens to a closed class nested in a generic class, as in case 2 of `Flagged.cs`, which lets a base class
hand its subclasses a result they can't create themselves:

```csharp
public abstract class Handler<TResult>
{
    protected closed record Result;

    private sealed record Continue : Result;

    private sealed record HaltWith(TResult ReturnThis) : Result;

    protected TResult Run(Result result, TResult current) => result switch
    {
        HaltWith halt => halt.ReturnThis,
        Continue => current
    };
}
```

## Actual result

Rider highlights all six switches in `Flagged.cs`, naming the closed class:

> The 'switch' expression does not handle all possible inputs (it is not exhaustive). For example, the pattern
> 'Either<TLeft,TRight>' is not covered.

> ... For example, the pattern 'Handler<TResult>.Result' is not covered.

## Expected result

No warning. The arms cover every direct subclass of the closed class (`Left` and `Right` of `Either<TLeft, TRight>`,
`Continue` and `HaltWith` of `Handler<TResult>.Result`), so each switch is exhaustive
([closed hierarchies](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/closed-hierarchies.md#exhaustiveness-in-switches)).
The compiler agrees: the project treats CS8509 as an error, and `dotnet build` succeeds.

## What triggers it

Rider flags a switch when both hold:

- the closed class and its subclasses are declared inside a generic type: the closed class is that type, or is nested
  in it,
- the switch is written inside that generic type: in the closed class, in the type that contains it, or in another
  type nested in either.

Where exactly the subclasses sit inside the generic type doesn't matter. [`src/Flagged.cs`](src/Flagged.cs) shows the
shapes Rider flags:

| Case in `Flagged.cs` | Closed class | Subclasses | Switch |
|---|---|---|---|
| 1. `Either<TLeft, TRight>` | the generic class | nested in it | in it (an operator and a method) |
| 2. `Handler<TResult>` | `Result`, nested in the generic class | beside it | in the generic class |
| 3. `Outer<T>` | `Result`, nested in the generic class | nested in `Result` | in the generic class |
| 4. `Container<T>` | `Result`, nested in the generic class | beside it | in `Result` |
| 5. `Choice<T>` | the generic class | nested in it | in a static class nested in it |

Writing the subclasses fully qualified (`Either<TLeft, TRight>.Left`) doesn't change it.
[`src/NotFlagged.cs`](src/NotFlagged.cs) changes one thing at a time, and Rider reports nothing on any of them:

| Case in `NotFlagged.cs` | Differs from `Flagged.cs` in |
|---|---|
| 1. `OutsideOpenGeneric` | case 1, switched outside the class, over `Either<TLeft, TRight>` |
| 2. `OutsideConstructed` | case 1, switched outside the class, over `Either<string, int>` |
| 3. `Result<TError, TValue>` | case 1, with the subclasses top-level |
| 4. `Gate` | case 1, with a closed class that isn't generic |
| 5. `Light` | not generic, top-level subclasses, switch outside (RSRP-504103) |
| 6. `NonGenericHandler` | case 2, with an outer class that isn't generic |
| 7. `OutsideOuter` | case 3, switched outside the outer class, over `Outer<T>.Result` and `Outer<int>.Result` |

## The arms that would silence it don't compile

Adding an arm for the type Rider names, `Either<TLeft, TRight> => …`, satisfies Rider, which then suggests "Use not
null pattern instead of a type check succeeding on any not-null value". The compiler rejects both, because `Left` and
`Right` already cover every non-null value:

| Last arm after `Left` and `Right` | Rider | `dotnet build` |
|---|---|---|
| `Either<TLeft, TRight> => …` | suggests "Use not null pattern" | error CS8510: The pattern is unreachable |
| `not null => …` | CS8655: `null` isn't covered | error CS8510, and warning CS8655 |
| `null => …` | CS8509 | builds |
| `_ => …` | nothing | builds |
| `not null or null => …` | nothing | builds |

Rider never reports the CS8510 errors, the same blind spot from the other side: it doesn't see that `Left` and `Right`
cover every non-null value. The arm that matches what the compiler knows is `null`, which Rider still flags, so the
only arms that satisfy both are catch-alls that the compiler doesn't need.
