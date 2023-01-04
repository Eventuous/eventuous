namespace Eventuous;

public class DomainException : Exception {
    public DomainException(string message) : base(message) { }
}