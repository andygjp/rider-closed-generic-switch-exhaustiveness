namespace ClosedExhaustiveness.Flagged;

// Rider reports CS8509 on both switches below. The compiler builds them without a warning,
// even though this project treats CS8509 as an error.
//
// All three are needed:
// - the closed class is generic,
// - its subclasses are nested in it,
// - the switch is inside the closed class, over the closed class's own type.

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

    // Rider: "The 'switch' expression does not handle all possible inputs (it is not exhaustive).
    // For example, the pattern 'Either<TLeft,TRight>' is not covered."
    public static explicit operator TLeft(Either<TLeft, TRight> either) => either switch
    {
        Left(var left) => left,
        Right => throw new InvalidCastException("It is a Right.")
    };

    // Rider: the same, with the subclasses written fully qualified.
    public string Describe() => this switch
    {
        Either<TLeft, TRight>.Left => "left",
        Either<TLeft, TRight>.Right => "right"
    };
}
