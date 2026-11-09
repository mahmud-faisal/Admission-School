using System.Net;
using SchoolAdmission.IntegrationTests.Infrastructure;

namespace SchoolAdmission.IntegrationTests.Api;

public class HoldsEndpointsTests(PostgresFixture postgres) : IntegrationTest(postgres)
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2015269123456789")]   // 16 digits
    [InlineData("201526912345678901")] // 18 digits
    [InlineData("2015269123456789a")]
    public async Task PostHold_MissingOrInvalidStudentId_Returns400_InvalidStudentId(string? studentId)
    {
        var classId = await CreateClassAsync();

        var response = await ApplyAsync(classId, studentId);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ProblemCodeAsync(response)).Should().Be("INVALID_STUDENT_ID");
    }

    [Fact]
    public async Task InvalidStudentId_IsCheckedBeforeLookup_OnEveryStudentEndpoint()
    {
        (await ApplyAsync("c_unknown", "123")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ConfirmAsync("h_unknown", "123")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await CancelAsync("h_unknown", "123")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await SendAsStudentAsync(HttpMethod.Get, "/me/holds", null)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostHold_Returns201_StatusHeld_ExpiresAtUtcZ()
    {
        var classId = await CreateClassAsync();

        var response = await ApplyAsync(classId, Sid(1));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var json = await ReadJsonAsync(response);
        var holdId = json.GetProperty("holdId").GetString();
        holdId.Should().MatchRegex("^h_[0-9a-f]{32}$");
        json.GetProperty("classId").GetString().Should().Be(classId);
        json.GetProperty("studentId").GetString().Should().Be(Sid(1));
        json.GetProperty("status").GetString().Should().Be("HELD");
        json.GetProperty("expiresAt").GetString().Should().MatchRegex(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}Z$");
        response.Headers.Location!.ToString().Should().Be($"/holds/{holdId}");

        var expires = json.GetProperty("expiresAt").GetDateTime();
        var serverTime = json.GetProperty("serverTime").GetDateTime();
        (expires - serverTime).TotalSeconds.Should().BeInRange(599, 600);
    }

    [Fact]
    public async Task PostHold_UnknownClass_Returns404()
    {
        var response = await ApplyAsync("c_unknown", Sid(1));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(response)).Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task PostHold_DuplicateActive_Returns409_DuplicateApplication()
    {
        var classId = await CreateClassAsync();
        await ApplyOkAsync(classId, Sid(1));

        var second = await ApplyAsync(classId, Sid(1));

        second.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(second)).Should().Be("DUPLICATE_APPLICATION");
        (await ScalarAsync<long>("SELECT count(*) FROM holds")).Should().Be(1);
    }

    [Fact]
    public async Task PostHold_DuplicateAfterConfirm_Returns409_DuplicateApplication()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));
        await ConfirmAsync(holdId, Sid(1));

        var again = await ApplyAsync(classId, Sid(1));

        again.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(again)).Should().Be("DUPLICATE_APPLICATION");
    }

    [Fact]
    public async Task PostHold_SameStudentDifferentClasses_BothSucceed()
    {
        var a = await CreateClassAsync("Class 6 - Morning Shift");
        var b = await CreateClassAsync("Class 6 - Day Shift");

        await ApplyOkAsync(a, Sid(1));
        await ApplyOkAsync(b, Sid(1));
    }

    [Fact]
    public async Task PostHold_AfterCancel_AllowsReapply_Returns201()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));
        (await CancelAsync(holdId, Sid(1))).StatusCode.Should().Be(HttpStatusCode.NoContent);

        var newHold = await ApplyOkAsync(classId, Sid(1));

        newHold.Should().NotBe(holdId);
    }

    [Fact]
    public async Task PostHold_LastSeat_ThenNext_Returns409_SeatsFull()
    {
        var classId = await CreateClassAsync(seats: 1);
        await ApplyOkAsync(classId, Sid(1));

        var next = await ApplyAsync(classId, Sid(2));

        next.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(next)).Should().Be("SEATS_FULL");
    }

    [Fact]
    public async Task GetHold_WithoutHeader_Returns200()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));

        var response = await Client.GetAsync($"/holds/{holdId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await ReadJsonAsync(response);
        json.GetProperty("holdId").GetString().Should().Be(holdId);
        json.GetProperty("classId").GetString().Should().Be(classId);
        json.GetProperty("status").GetString().Should().Be("HELD");
    }

    [Fact]
    public async Task GetHold_Unknown_Returns404()
    {
        var response = await Client.GetAsync("/holds/h_unknown");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(response)).Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task ConfirmHold_OtherStudent_Returns403()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));

        var response = await ConfirmAsync(holdId, Sid(2));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(response)).Should().Be("FORBIDDEN");
        (await GetClassAsync(classId)).GetProperty("confirmedSeats").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task ConfirmHold_Twice_Returns200_Both_ConfirmedSeatsIncrementsOnce()
    {
        var classId = await CreateClassAsync(seats: 5);
        var holdId = await ApplyOkAsync(classId, Sid(1));

        var first = await ConfirmAsync(holdId, Sid(1));
        var second = await ConfirmAsync(holdId, Sid(1));

        first.StatusCode.Should().Be(HttpStatusCode.OK);
        second.StatusCode.Should().Be(HttpStatusCode.OK);
        (await ReadJsonAsync(first)).GetProperty("status").GetString().Should().Be("CONFIRMED");
        (await ReadJsonAsync(second)).GetProperty("status").GetString().Should().Be("CONFIRMED");
        (await ReadJsonAsync(second)).GetProperty("holdId").GetString().Should().Be(holdId);

        var seats = await GetClassAsync(classId);
        seats.GetProperty("confirmedSeats").GetInt32().Should().Be(1);
        seats.GetProperty("heldSeats").GetInt32().Should().Be(0);
        seats.GetProperty("availableSeats").GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task ConfirmHold_Cancelled_Returns409_HoldCancelled()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));
        await CancelAsync(holdId, Sid(1));

        var response = await ConfirmAsync(holdId, Sid(1));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(response)).Should().Be("HOLD_CANCELLED");
    }

    [Fact]
    public async Task ConfirmHold_Unknown_Returns404()
    {
        var response = await ConfirmAsync("h_unknown", Sid(1));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await ProblemCodeAsync(response)).Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task CancelHold_Returns204_SeatReleased()
    {
        var classId = await CreateClassAsync(seats: 1);
        var holdId = await ApplyOkAsync(classId, Sid(1));

        var response = await CancelAsync(holdId, Sid(1));

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await GetClassAsync(classId)).GetProperty("availableSeats").GetInt32().Should().Be(1);
        (await ReadJsonAsync(await Client.GetAsync($"/holds/{holdId}"))).GetProperty("status").GetString().Should().Be("CANCELLED");
        await ApplyOkAsync(classId, Sid(2));
    }

    [Fact]
    public async Task CancelHold_Twice_Returns204()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));

        (await CancelAsync(holdId, Sid(1))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await CancelAsync(holdId, Sid(1))).StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task CancelHold_Confirmed_Returns409()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));
        await ConfirmAsync(holdId, Sid(1));

        var response = await CancelAsync(holdId, Sid(1));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await ProblemCodeAsync(response)).Should().Be("HOLD_ALREADY_CONFIRMED");
        (await GetClassAsync(classId)).GetProperty("confirmedSeats").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task CancelHold_OtherStudent_Returns403()
    {
        var classId = await CreateClassAsync();
        var holdId = await ApplyOkAsync(classId, Sid(1));

        var response = await CancelAsync(holdId, Sid(2));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await ProblemCodeAsync(response)).Should().Be("FORBIDDEN");
    }

    [Fact]
    public async Task GetMyHolds_ReturnsAllStatuses_WithClassName_NewestFirst()
    {
        var a = await CreateClassAsync("Class 6 - Morning Shift");
        var b = await CreateClassAsync("Class 7 - Day Shift");
        var c = await CreateClassAsync("Class 8 - Day Shift");
        var held = await ApplyOkAsync(a, Sid(1));
        var confirmed = await ApplyOkAsync(b, Sid(1));
        await ConfirmAsync(confirmed, Sid(1));
        var cancelled = await ApplyOkAsync(c, Sid(1));
        await CancelAsync(cancelled, Sid(1));
        await ApplyOkAsync(a, Sid(2)); // another student's hold must not appear

        var response = await SendAsStudentAsync(HttpMethod.Get, "/me/holds", Sid(1));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = (await ReadJsonAsync(response)).EnumerateArray().ToList();
        items.Select(i => i.GetProperty("holdId").GetString()).Should().Equal(cancelled, confirmed, held);
        items.Select(i => i.GetProperty("status").GetString()).Should().Equal("CANCELLED", "CONFIRMED", "HELD");
        items[2].GetProperty("className").GetString().Should().Be("Class 6 - Morning Shift");
        items[2].GetProperty("classId").GetString().Should().Be(a);
    }

    [Fact]
    public async Task GetMyHolds_NoApplications_ReturnsEmptyArray()
    {
        var response = await SendAsStudentAsync(HttpMethod.Get, "/me/holds", Sid(9));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).Should().Be("[]");
    }
}
