using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Api;

/// <summary>Versioning, API docs in Production, CORS and health — the cross-cutting rules from CLAUDE.md.</summary>
public class PlatformTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    [Fact]
    public async Task Versioning_NoVersion_HeaderVersion_QueryVersion_AllReturn200_SamePaths()
    {
        var none = await Client.GetAsync("/classes");
        var query = await Client.GetAsync("/classes?api-version=1.0");
        var headerRequest = new HttpRequestMessage(HttpMethod.Get, "/classes");
        headerRequest.Headers.Add("api-version", "1.0");
        var header = await Client.SendAsync(headerRequest);

        none.StatusCode.Should().Be(HttpStatusCode.OK);
        query.StatusCode.Should().Be(HttpStatusCode.OK);
        header.StatusCode.Should().Be(HttpStatusCode.OK);
        none.Headers.GetValues("api-supported-versions").Should().Contain("1.0");
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("abc")]
    public async Task Versioning_Unsupported_Returns400(string version)
    {
        var response = await Client.GetAsync($"/classes?api-version={version}");

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).Should().Be("UNSUPPORTED_API_VERSION");
    }

    [Fact]
    public async Task OpenApi_SwaggerAndScalar_AreServed_InProductionEnvironment()
    {
        var production = StartApiAsync(new Dictionary<string, string?>(), environment: "Production");

        foreach (var path in new[] { "/openapi/v1.json", "/swagger/index.html", "/scalar/" })
        {
            var response = await production.GetAsync(path);
            response.StatusCode.Should().Be(HttpStatusCode.OK, path);
        }

        var document = await ReadJsonAsync(await production.GetAsync("/openapi/v1.json"));
        var paths = document.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();
        paths.Should().BeEquivalentTo(
            "/classes", "/classes/{classId}", "/classes/{classId}/holds", "/holds/{holdId}", "/holds/{holdId}/confirm", "/me/holds");
    }

    [Fact]
    public async Task Cors_Preflight_AllowsConfiguredOrigin_AndXStudentIdHeader()
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/classes/c_1/holds");
        request.Headers.Add("Origin", "http://localhost:3000");
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "x-student-id");

        var response = await Client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Access-Control-Allow-Origin").Should().Contain("http://localhost:3000");
        response.Headers.GetValues("Access-Control-Allow-Headers").Single().Should().ContainEquivalentOf("x-student-id");
    }

    [Fact]
    public async Task Cors_UnknownOrigin_IsNotAllowed()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/classes");
        request.Headers.Add("Origin", "http://evil.example");

        var response = await Client.SendAsync(request);

        response.Headers.Contains("Access-Control-Allow-Origin").Should().BeFalse();
    }

    [Fact]
    public async Task Health_LiveAndReady_Return200_AndRootRedirectsToSwagger()
    {
        (await Client.GetAsync("/health")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await Client.GetAsync("/health/ready")).StatusCode.Should().Be(HttpStatusCode.OK);

        // The test client follows redirects: "/" → "/swagger" → "/swagger/index.html".
        var root = await Client.GetAsync("/");
        root.StatusCode.Should().Be(HttpStatusCode.OK);
        root.RequestMessage!.RequestUri!.AbsolutePath.Should().StartWith("/swagger");
    }

    [Fact]
    public async Task EveryResponse_HasServerTimeHeader()
    {
        var response = await Client.GetAsync("/classes");

        response.Headers.GetValues("X-Server-Time").Single().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z$");
    }
}
