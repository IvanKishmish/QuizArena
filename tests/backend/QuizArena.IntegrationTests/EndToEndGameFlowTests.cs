using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR.Client;

namespace QuizArena.IntegrationTests;

public sealed class EndToEndGameFlowTests(QuizArenaWebApplicationFactory factory) : IClassFixture<QuizArenaWebApplicationFactory>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private async Task<(Guid UserId, string AccessToken)> RegisterAsync(HttpClient client, string nickName)
    {
        var email = $"{Guid.NewGuid()}@quizarena-integration.test";

        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            nickName,
            email,
            password = "Password123!"
        });

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = body.GetProperty("accessToken").GetString()!;

        var payload = accessToken.Split('.')[1];
        var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var claims = JsonSerializer.Deserialize<JsonElement>(Convert.FromBase64String(padded.Replace('-', '+').Replace('_', '/')));
        var userId = Guid.Parse(claims.GetProperty("sub").GetString()!);

        return (userId, accessToken);
    }

    [Fact]
    public async Task FullGameFlow_FromRegistrationThroughAnAnsweredGame_WorksAgainstRealInfrastructure()
    {
        var client = factory.CreateClient();

        var (_, hostAccessToken) = await RegisterAsync(client, "IntegrationHost");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hostAccessToken);

        var createQuizResponse = await client.PostAsJsonAsync("/api/quizsets", new
        {
            title = "Integration Quiz",
            description = "End-to-end coverage"
        });
        createQuizResponse.EnsureSuccessStatusCode();
        var quizSetId = await createQuizResponse.Content.ReadFromJsonAsync<Guid>();

        var addQuestionResponse = await client.PostAsJsonAsync($"/api/quizsets/{quizSetId}/questions", new
        {
            text = "2 + 2 = ?",
            questionType = 0,
            timeLimitSeconds = 30,
            points = 100,
            options = new object[]
            {
                new { text = "4", isCorrect = true, orderIndex = 0 },
                new { text = "5", isCorrect = false, orderIndex = 1 }
            }
        });
        addQuestionResponse.EnsureSuccessStatusCode();
        var questionId = await addQuestionResponse.Content.ReadFromJsonAsync<Guid>();

        var getQuestionsResponse = await client.GetAsync($"/api/quizsets/{quizSetId}/questions");
        getQuestionsResponse.EnsureSuccessStatusCode();
        var questions = await getQuestionsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var readBackQuestion = questions.EnumerateArray().Single(q => q.GetProperty("id").GetGuid() == questionId);
        var readBackOptions = readBackQuestion.GetProperty("options").EnumerateArray().ToList();

        readBackOptions.Should().HaveCount(2, "K1: options must survive the Mongo round trip");
        readBackOptions.Should().Contain(o => o.GetProperty("text").GetString() == "4" && o.GetProperty("isCorrect").GetBoolean());

        var createRoomResponse = await client.PostAsJsonAsync("/api/gamerooms", new { quizSetId });
        createRoomResponse.EnsureSuccessStatusCode();
        var roomCode = await createRoomResponse.Content.ReadFromJsonAsync<string>();
        roomCode.Should().NotBeNullOrWhiteSpace();

        var joinResponse = await client.PostAsJsonAsync(
            $"/api/gamerooms/{roomCode}/join", new { displayName = "IntegrationHost" });
        joinResponse.EnsureSuccessStatusCode();
        var joinBody = await joinResponse.Content.ReadFromJsonAsync<JsonElement>();
        var participantId = joinBody.GetProperty("participantId").GetGuid();
        var participantToken = joinBody.GetProperty("participantToken").GetString()!;

        var startResponse = await client.PostAsync($"/api/gamerooms/{roomCode}/start", null);
        startResponse.EnsureSuccessStatusCode();

        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, "hubs/game"), options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.AccessTokenProvider = () => Task.FromResult(hostAccessToken)!;
                options.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
            })
            .Build();

        var errors = new List<string>();
        connection.On<string>("Error", message => errors.Add(message));

        var answerResultReceived = new TaskCompletionSource<int>();
        connection.On<JsonElement>("AnswerResult", payload =>
            answerResultReceived.TrySetResult(payload.GetProperty("score").GetInt32()));

        await connection.StartAsync();
        await connection.InvokeAsync("RegisterParticipant", roomCode, participantId, participantToken);

        await connection.InvokeAsync("NextQuestion", roomCode);

        await connection.InvokeAsync("SubmitAnswer", roomCode, participantId, questionId, new List<int> { 0 });
        var firstScore = await answerResultReceived.Task.WaitAsync(TimeSpan.FromSeconds(10));
        firstScore.Should().BeGreaterThan(0);

        errors.Clear();
        await connection.InvokeAsync("SubmitAnswer", roomCode, participantId, questionId, new List<int> { 0 });
        await Task.Delay(500);

        errors.Should().ContainSingle(e => e.Contains("already answered", StringComparison.OrdinalIgnoreCase));
    }
}
