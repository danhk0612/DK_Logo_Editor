using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DKLogoEditor.Models;

namespace DKLogoEditor.Services;

public sealed class OpenRouterLayoutPlanner
{
    private const string PlannerModelId = "google/gemini-3.1-flash-lite";
    private static readonly HttpClient SharedHttpClient = new();
    private readonly HttpClient _httpClient;

    public OpenRouterLayoutPlanner(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task<LogoLayoutPlan> PlanAsync(
        string apiKey,
        byte[] sourceImageBytes,
        string sourceMediaType,
        string subtitle,
        int outputWidth,
        int outputHeight,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        var dataUrl = $"data:{sourceMediaType};base64,{Convert.ToBase64String(sourceImageBytes)}";
        var prompt = BuildPrompt(subtitle, outputWidth, outputHeight);
        var payload = new
        {
            model = PlannerModelId,
            temperature = 0.1,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = prompt },
                        new { type = "image_url", image_url = new { url = dataUrl } }
                    }
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/chat/completions")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var content = json.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            return LogoLayoutPlan.Fallback(outputWidth, outputHeight, subtitle.Length);
        }

        try
        {
            var cleaned = StripCodeFence(content);
            using var planJson = JsonDocument.Parse(cleaned);
            var root = planJson.RootElement;

            return new LogoLayoutPlan(
                ReadNumber(root, "logo_x"),
                ReadNumber(root, "logo_y"),
                ReadNumber(root, "logo_width"),
                ReadNumber(root, "logo_height"),
                ReadNumber(root, "subtitle_x"),
                ReadNumber(root, "subtitle_y"),
                ReadNumber(root, "subtitle_width"),
                ReadNumber(root, "subtitle_height"),
                root.TryGetProperty("subtitle_alignment", out var alignment)
                    ? alignment.GetString() ?? "left"
                    : "left");
        }
        catch (JsonException)
        {
            return LogoLayoutPlan.Fallback(outputWidth, outputHeight, subtitle.Length);
        }
    }

    private static string BuildPrompt(string subtitle, int outputWidth, int outputHeight)
    {
        return $"""
You are a logo layout art director. Analyze the supplied ORIGINAL logo and decide how to add the supplementary name exactly as written: "{subtitle}".
Final canvas: {outputWidth} x {outputHeight}.

The original logo artwork itself must never be redrawn, recolored, restyled, distorted, cropped, or edited. You may only decide its proportional scale and position.
Decide the composition a professional designer would choose automatically. Do not assume the subtitle belongs below the logo. It may go below, beside, above, or in another clean open area if that is visually better.
Reduce the original logo only when needed to create a balanced composition. Keep it as large as practical otherwise.
The subtitle is secondary information and must not overpower the logo. Logo and subtitle areas must not overlap. Keep comfortable outer margins.

Return ONLY one JSON object. All coordinates and sizes are normalized from 0.0 to 1.0 relative to the final canvas:
{{
  "logo_x": 0.0,
  "logo_y": 0.0,
  "logo_width": 0.0,
  "logo_height": 0.0,
  "subtitle_x": 0.0,
  "subtitle_y": 0.0,
  "subtitle_width": 0.0,
  "subtitle_height": 0.0,
  "subtitle_alignment": "left|center|right"
}}

Use rectangles that fit fully inside the canvas and do not overlap. The program will preserve the original logo aspect ratio inside your proposed logo rectangle.
""";
    }

    private static double ReadNumber(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetDouble(out var number))
        {
            throw new JsonException($"Missing numeric layout property: {name}");
        }

        return number;
    }

    private static string StripCodeFence(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstNewline = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstNewline >= 0 && lastFence > firstNewline
            ? trimmed[(firstNewline + 1)..lastFence].Trim()
            : trimmed;
    }
}
