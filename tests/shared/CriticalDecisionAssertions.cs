using System.Net;
using System.Text.Json;

namespace IdentityPlatform.Testing;

// A mutation marker proves the intended unsafe success, never a generic test failure.
internal static class CriticalDecisionAssertions
{
    internal static async Task DeniedTokensAsync(HttpResponseMessage response, Func<JsonElement, Task<bool>> validate, string marker)
    {
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            return;
        }
        Xunit.Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await ValidTokensAsync(response, validate);
        Xunit.Assert.Fail(marker);
    }

    internal static async Task SingleWinnerAsync(HttpResponseMessage[] responses, Func<JsonElement, Task<bool>> validate, string marker)
    {
        Xunit.Assert.Equal(2, responses.Length);
        if (responses.All(response => response.StatusCode == HttpStatusCode.OK))
        {
            foreach (var response in responses)
            {
                await ValidTokensAsync(response, validate);
            }
            Xunit.Assert.Fail(marker);
        }
        Xunit.Assert.True(responses.Count(response => response.StatusCode == HttpStatusCode.OK) == 1 &&
            responses.Count(response => response.StatusCode == HttpStatusCode.BadRequest) == 1,
            "Concurrent exchange must have exactly one winner and one denial.");
    }

    private static async Task ValidTokensAsync(HttpResponseMessage response, Func<JsonElement, Task<bool>> validate)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(Xunit.TestContext.Current.CancellationToken));
        }
        catch (JsonException)
        {
            Xunit.Assert.Fail("Unexpected success did not contain a JSON token response.");
            return;
        }
        using (document)
        {
            var value = document.RootElement;
            Xunit.Assert.True(value.ValueKind == JsonValueKind.Object &&
                value.TryGetProperty("access_token", out var access) && access.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(access.GetString()) &&
                value.TryGetProperty("id_token", out var identity) && identity.ValueKind == JsonValueKind.String && !string.IsNullOrEmpty(identity.GetString()) &&
                value.TryGetProperty("token_type", out var type) && type.ValueKind == JsonValueKind.String && type.GetString() == "Bearer",
                "Unexpected success did not contain the required token response shape.");
            Xunit.Assert.True(await validate(value), "Unexpected token response failed independent validation.");
        }
    }
}
