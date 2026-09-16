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

            var plan = new LogoLayoutPlan(
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

            return NormalizeForOutput(plan, outputWidth, outputHeight, subtitle.Length);
        }
        catch (JsonException)
        {
            return LogoLayoutPlan.Fallback(outputWidth, outputHeight, subtitle.Length);
        }
    }

    private static string BuildPrompt(string subtitle, int outputWidth, int outputHeight)
    {
        var ratio = outputWidth / (double)outputHeight;
        var compact = outputWidth <= 320 || outputHeight <= 100;

        var sizeGuidance = compact
            ? "This is a SMALL final logo output. Use a compact lockup. Keep the supplementary name close to the main logo, never stranded near a far edge. The combined logo and subtitle should fit inside one compact visual group. The subtitle should normally occupy no more than about 25-30% of the total visual emphasis and must be clearly smaller than the main logo. Avoid dramatic spacing and avoid a wide three-part composition."
            : "Use the available canvas efficiently and keep the subtitle visibly secondary to the main logo.";

        var wideGuidance = ratio >= 2.5
            ? "This is a wide canvas. Compare a TIGHT right-side lockup, below-left alignment, below-right alignment, and centered-below. If you choose a side placement, keep the subtitle immediately adjacent to the logo group with only a small visual gap. Do not push it toward the far right edge."
            : "Compare several plausible arrangements rather than defaulting to centered-below.";

        return string.Join(Environment.NewLine,
            "You are a logo layout art director. Analyze the supplied ORIGINAL logo and decide how to add the supplementary name exactly as written: \"" + subtitle + "\".",
            $"Final canvas: {outputWidth} x {outputHeight} (aspect ratio {ratio:0.00}:1).",
            string.Empty,
            "The original logo artwork itself must never be redrawn, recolored, restyled, distorted, cropped, or edited. You may only decide its proportional scale and position.",
            "Before choosing, internally compare at least these families: tight-right beside the logo, below-left aligned to the logo structure, below-right, and centered-below. Choose the one that best fits this specific logo and target canvas.",
            sizeGuidance,
            wideGuidance,
            "Keep all important content inside a central safe area with visible outer margins. Do not place either the original logo or the supplementary name close to the canvas edges.",
            "Avoid excessive empty margins. At the same time, never solve the layout by spreading the logo and subtitle far apart.",
            "Reduce the original logo only when needed to create a balanced composition. Keep it as large as practical otherwise.",
            "The subtitle is secondary information and must not overpower the logo. Logo and subtitle areas must not overlap.",
            "Choose left, center, or right alignment based on the geometry of the original logo; do not treat center alignment as the default.",
            string.Empty,
            "Return ONLY one JSON object. All coordinates and sizes are normalized from 0.0 to 1.0 relative to the final canvas:",
            "{",
            "  \"logo_x\": 0.0,",
            "  \"logo_y\": 0.0,",
            "  \"logo_width\": 0.0,",
            "  \"logo_height\": 0.0,",
            "  \"subtitle_x\": 0.0,",
            "  \"subtitle_y\": 0.0,",
            "  \"subtitle_width\": 0.0,",
            "  \"subtitle_height\": 0.0,",
            "  \"subtitle_alignment\": \"left|center|right\"",
            "}",
            string.Empty,
            "Use rectangles that fit fully inside the canvas and do not overlap. The image editor will treat these as composition guidance, not as a request to redraw the original logo.");
    }

    private static LogoLayoutPlan NormalizeForOutput(
        LogoLayoutPlan plan,
        int outputWidth,
        int outputHeight,
        int subtitleLength)
    {
        var compact = outputWidth <= 320 || outputHeight <= 100;
        if (!compact)
        {
            return plan;
        }

        // Keep a compact safe area for tiny outputs. If the model proposes an overly
        // large or far-separated subtitle, fall back to a deterministic compact plan.
        var subtitleTooLarge = plan.SubtitleWidth > 0.36 || plan.SubtitleHeight > 0.46;
        var nearEdge = plan.SubtitleX < 0.02
                       || plan.SubtitleY < 0.02
                       || plan.SubtitleX + plan.SubtitleWidth > 0.98
                       || plan.SubtitleY + plan.SubtitleHeight > 0.98;

        var logoCenterX = plan.LogoX + plan.LogoWidth / 2.0;
        var subtitleCenterX = plan.SubtitleX + plan.SubtitleWidth / 2.0;
        var separated = Math.Abs(logoCenterX - subtitleCenterX) > 0.58
                        && plan.SubtitleY < plan.LogoY + plan.LogoHeight;

        return subtitleTooLarge || nearEdge || separated
            ? LogoLayoutPlan.Fallback(outputWidth, outputHeight, subtitleLength)
            : plan;
    }

    private static double ReadNumber(JsonElement root, string name)
    {
        if (!root.TryGetProperty(name, out var value) || !value.TryGetDouble(out var number))
        {
            throw new JsonException($"Missing numeric layout property: {name}");
        }

        return Math.Clamp(number, 0.0, 1.0);
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
