using Bogus;
using Eventuous.Sut.App;

namespace ElasticPlayground;

public class Generator{
    public static string RandomString() => Guid.NewGuid().ToString();
    
    static readonly Faker<Commands.BookRoom> Faker = new Faker<Commands.BookRoom>()
        .CustomInstantiator(
            f => {
                var checkin  = f.Noda().LocalDate.Soon();
                var checkout = checkin.PlusDays(f.Random.Number(1, 5));

                return new(f.Random.Guid().ToString("N"), f.Random.Guid().ToString("N"), checkin, checkout, f.Random.Number(50, 200));
            }
        );

    public static Commands.BookRoom CreateBookRoomCommand() => Faker.Generate();
}