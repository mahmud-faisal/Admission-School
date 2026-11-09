using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Api;

public class SmokeTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    [Fact]
    public async Task GetClasses_Empty_ReturnsEmptyArray()
    {
        var response = await Client.GetAsync("/classes");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }
}
