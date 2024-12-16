using Bogus;
using Eventuous.Sut.App;

namespace ElasticPlayground;

public class Generator{
    public static string RandomString() => Guid.NewGuid().ToString();
    
    static readonly Faker<Commands.BookRoom> Faker = new Faker<Commands.BookRoom>()
        .RuleFor(x => x.BookingId, _ => Guid.NewGuid().ToString("N"))
        .RuleFor(x => x.RoomId, _ => Guid.NewGuid().ToString("N"))
        .RuleFor(x => x.Price, f => f.Random.Number(50, 200))
        .RuleFor(x => x.CheckIn, f => f.Noda().LocalDate.Soon())
        .RuleFor(x => x.CheckOut, (f, c) => c.CheckIn.PlusDays(f.Random.Number(1, 5)));

    public static Commands.BookRoom CreateBookRoomCommand() => Faker.Generate();
}