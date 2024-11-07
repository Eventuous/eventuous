namespace Eventuous.Sut.Domain;

public record Money(float Amount, string Currency = "EUR") {
    public static Money operator +(Money left, Money right) {
        if (left.Currency != right.Currency) throw new InvalidOperationException("Currencies must match");
        return left with { Amount = left.Amount + right.Amount };
    }

    public static Money operator -(Money left, Money right) {
        if (left.Currency != right.Currency) throw new InvalidOperationException("Currencies must match");
        return left with { Amount = left.Amount - right.Amount };
    }
}
