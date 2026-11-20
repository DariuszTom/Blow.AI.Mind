using Bogus;
using Blow.AI.Mind.FakeData;

namespace Blow.AI.Mind.Templating;

internal sealed class TemplateContext(Faker faker, int rows)
{
    public Faker Faker { get; } = faker;

    public ItFakeData It { get; } = new(faker.Random);

    public int Rows { get; } = rows;

    /// <summary>Reference window for generated login dates.</summary>
    public static readonly DateTime MinDate = new(2023, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static readonly DateTime MaxDate = new(2025, 12, 31, 23, 59, 59, DateTimeKind.Utc);

    public DateTime Date(DateTime? from = null, DateTime? to = null)
    {
        var start = from ?? MinDate;
        var end = to ?? MaxDate;
        var date = Faker.Date.Between(start, end);
        return new DateTime(date.Year, date.Month, date.Day, date.Hour, date.Minute, date.Second, DateTimeKind.Unspecified);
    }
}
