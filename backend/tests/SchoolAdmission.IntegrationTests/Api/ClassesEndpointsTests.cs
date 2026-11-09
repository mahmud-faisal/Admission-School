using System.Net;
using System.Net.Http.Json;
using System.Text;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Api;

public class ClassesEndpointsTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    [Fact]
    public async Task PostClasses_Returns201_WithSpecFields_AndLocation()
    {
        var response = await Client.PostAsJsonAsync("/classes", new { name = "Class 6 - Morning Shift", totalSeats = 120 });

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        var id = json.GetProperty("id").GetString();
        id.Should().MatchRegex("^c_[0-9a-f]{32}$");
        json.GetProperty("name").GetString().Should().Be("Class 6 - Morning Shift");
        json.GetProperty("totalSeats").GetInt32().Should().Be(120);
        response.Headers.Location!.ToString().Should().Be($"/classes/{id}");
    }

    [Theory]
    [InlineData("{\"totalSeats\":10}")]
    [InlineData("{\"name\":\"  \",\"totalSeats\":10}")]
    [InlineData("{\"name\":\"X\",\"totalSeats\":0}")]
    [InlineData("{\"name\":\"X\",\"totalSeats\":100001}")]
    [InlineData("{\"name\":\"X\",\"totalSeats\":\"abc\"}")]
    [InlineData("{\"name\":\"X\",\"totalSeats\":\"7\"}")]
    [InlineData("{\"name\":\"X\",\"totalSeats\":1.5}")]
    [InlineData("{\"name\":")]
    [InlineData("")]
    public async Task PostClasses_InvalidBody_Returns400_ProblemDetailsWithCode(string body)
    {
        var response = await Client.PostAsync("/classes", new StringContent(body, Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).Should().Be("VALIDATION_FAILED");
        (await ReadJsonAsync(response)).GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task PostClasses_WithoutJsonContentType_Returns415_WithCode()
    {
        var response = await Client.PostAsync("/classes", new StringContent("x", Encoding.UTF8, "text/plain"));

        response.StatusCode.Should().Be(HttpStatusCode.UnsupportedMediaType);
        (await ProblemCodeAsync(response)).Should().Be("UNSUPPORTED_MEDIA_TYPE");
    }

    [Fact]
    public async Task GetClasses_ReturnsSeatCounts_CamelCase()
    {
        var classId = await CreateClassAsync(seats: 3);
        var hold = await ApplyOkAsync(classId, Sid(1));
        await ApplyOkAsync(classId, Sid(2));
        (await ConfirmAsync(hold, Sid(1))).StatusCode.Should().Be(HttpStatusCode.OK);

        var list = await ReadJsonAsync(await Client.GetAsync("/classes"));

        var item = list.EnumerateArray().Single();
        item.GetProperty("id").GetString().Should().Be(classId);
        item.GetProperty("totalSeats").GetInt32().Should().Be(3);
        item.GetProperty("heldSeats").GetInt32().Should().Be(1);
        item.GetProperty("confirmedSeats").GetInt32().Should().Be(1);
        item.GetProperty("availableSeats").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task GetClasses_ListsOldestFirst_AndAllowsDuplicateNames()
    {
        var first = await CreateClassAsync("Same name");
        var second = await CreateClassAsync("Same name");

        var ids = (await ReadJsonAsync(await Client.GetAsync("/classes"))).EnumerateArray()
            .Select(c => c.GetProperty("id").GetString()).ToList();

        ids.Should().Equal(first, second);
    }

    [Fact]
    public async Task GetClass_Unknown_Returns404_NotFound()
    {
        var response = await Client.GetAsync("/classes/c_doesnotexist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(response)).Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task UnknownRoute_Returns404_AndWrongMethod_Returns405_WithCodes()
    {
        var notFound = await Client.GetAsync("/nope");
        var notAllowed = await Client.PutAsync("/classes", new StringContent("{}", Encoding.UTF8, "application/json"));

        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(notFound)).Should().Be("NOT_FOUND");
        notAllowed.StatusCode.Should().Be(HttpStatusCode.MethodNotAllowed);
        (await ProblemCodeAsync(notAllowed)).Should().Be("METHOD_NOT_ALLOWED");
    }
}
