namespace ClosedExhaustiveness.Flagged;

// Rider reports CS8509 on every switch below. The compiler builds them without a warning,
// even though this project treats CS8509 as an error.
//
// In each one:
// - the closed type and its subclasses are declared inside a generic type (the closed type may be that type),
// - the switch is written inside that generic type.

// 1. A generic closed class with nested subclasses, switched inside the class.
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

// 2. A closed record that isn't generic itself, nested in a generic class with its subclasses beside it,
// switched in the outer class. Lets a base class hand its subclasses a result they can't create themselves.
public abstract class Handler<TResult>
{
    protected closed record Result;

    private sealed record Continue : Result;

    private sealed record HaltWith(TResult ReturnThis) : Result;

    protected Result Halt(TResult returnThis) => new HaltWith(returnThis);

    protected Result Carry() => new Continue();

    // Rider: "... For example, the pattern 'Handler<TResult>.Result' is not covered."
    protected TResult Run(Result result, TResult current) => result switch
    {
        HaltWith halt => halt.ReturnThis,
        Continue => current
    };
}

// 3. As 2, with the subclasses nested in the closed class.
public abstract class Outer<T>
{
    public closed class Result
    {
        public sealed class A : Result;

        public sealed class B : Result;
    }

    // Rider: "... For example, the pattern 'Outer<T>.Result' is not covered."
    public string Describe(Result result) => result switch
    {
        Result.A => "a",
        Result.B => "b"
    };
}

// 4. As 2, switched inside the closed class.
public abstract class Container<T>
{
    public closed class Result
    {
        // Rider: "... For example, the pattern 'Container<T>.Result' is not covered."
        public string Describe() => this switch
        {
            A => "a",
            B => "b"
        };
    }

    public sealed class A : Result;

    public sealed class B : Result;
}

// 5. As 1, switched in another class nested in the closed class.
public closed class Choice<T>
{
    public sealed class A : Choice<T>;

    public sealed class B : Choice<T>;

    public static class Describer
    {
        // Rider: "... For example, the pattern 'Choice<T>' is not covered."
        public static string Describe(Choice<T> choice) => choice switch
        {
            A => "a",
            B => "b"
        };
    }
}
