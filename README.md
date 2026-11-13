# Rider: false CS8509 on a switch inside a generic closed class

Rider reports CS8509, "The 'switch' expression does not handle all possible inputs (it is not exhaustive)", on a
switch that the C# 15 compiler accepts as exhaustive. It happens only when the switch is inside a generic `closed`
class whose subclasses are nested in it.

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

## Actual result

Rider highlights both switches in `Flagged.cs`:

> The 'switch' expression does not handle all possible inputs (it is not exhaustive). For example, the pattern
> 'Either<TLeft,TRight>' is not covered.

## Expected result

No warning. `Left` and `Right` are the only direct subclasses of the closed class `Either<TLeft, TRight>`, so the
switch is exhaustive ([closed hierarchies](https://github.com/dotnet/csharplang/blob/main/proposals/csharp-15.0/closed-hierarchies.md#exhaustiveness-in-switches)).
The compiler agrees: the project treats CS8509 as an error, and `dotnet build` succeeds.

## What triggers it

Rider flags the switch only when all three hold:

- the closed class is generic,
- its subclasses are nested in it,
- the switch is inside the closed class, over the closed class's own type.

Writing the subclasses fully qualified (`Either<TLeft, TRight>.Left`) doesn't change it.
[`src/NotFlagged.cs`](src/NotFlagged.cs) changes one of the three at a time, and Rider reports nothing on any of them:

| Case in `NotFlagged.cs` | Differs from `Flagged.cs` in |
|---|---|
| 1. `OutsideOpenGeneric` | the switch is outside the class, over `Either<TLeft, TRight>` |
| 2. `OutsideConstructed` | the switch is outside the class, over `Either<string, int>` |
| 3. `Result<TError, TValue>` | the subclasses are top-level |
| 4. `Gate` | the closed class isn't generic |
| 5. `Light` | not generic, top-level subclasses, switch outside (RSRP-504103) |

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
