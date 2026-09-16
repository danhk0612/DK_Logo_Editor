using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DKLogoEditor.Models;

namespace DKLogoEditor.Services;

public sealed class OpenRouterImageClient
{
    private static readonly HttpClient SharedHttpClient = new();
    private readonly HttpClient _httpClient;

    public OpenRouterImageClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient;
    }

    public async Task<OpenRouterImageResult> NaturalEditAsync(
        string apiKey,
        NaturalLogoEditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceMediaType);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AspectRatio);

        if (request.SourceImageBytes.Length == 0)
        {
            throw new ArgumentException("Source image is empty.", nameof(request));
        }

        var effectiveRequest = request;
        if (!string.IsNullOrWhiteSpace(request.Subtitle) && request.LayoutPlan is null)
        {
            try
            {
                var planner = new OpenRouterLayoutPlanner(_httpClient);
                var plan = await planner.PlanAsync(
                    apiKey,
                    request.SourceImageBytes,
                    request.SourceMediaType,
                    request.Subtitle,
                    request.OutputWidth,
                    request.OutputHeight,
                    cancellationToken);
                effectiveRequest = request with { LayoutPlan = plan };
            }
            catch (HttpRequestException)
            {
                // Layout planning is an enhancement. If it fails, keep the primary
                // image edit available and let the image model decide the layout itself.
            }
            catch (JsonException)
            {
                // Same fallback for an unexpected planner response.
            }
        }

        var sourceDataUrl = $"data:{effectiveRequest.SourceMediaType};base64,{Convert.ToBase64String(effectiveRequest.SourceImageBytes)}";
        var payload = new
        {
            model = effectiveRequest.ModelId,
            prompt = AiNaturalEditPromptBuilder.Build(effectiveRequest),
            resolution = effectiveRequest.Resolution,
            aspect_ratio = effectiveRequest.AspectRatio,
            quality = "high",
            output_format = "png",
            background = effectiveRequest.TransparentBackground ? "transparent" : "opaque",
            input_references = new[]
            {
                new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = sourceDataUrl
                    }
                }
            }
        };

        return await SendImageRequestAsync(apiKey, payload, effectiveRequest.ModelId, cancellationToken);
    }

    public async Task<OpenRouterImageResult> EditAsync(
        string apiKey,
        LogoSubtitleEditRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ModelId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourceMediaType);

        if (request.SourceImageBytes.Length == 0)
        {
            throw new ArgumentException("Source image is empty.", nameof(request));
        }

        var sourceDataUrl = $"data:{request.SourceMediaType};base64,{Convert.ToBase64String(request.SourceImageBytes)}";
        var payload = new
        {
            model = request.ModelId,
            prompt = LogoEditPromptBuilder.Build(request),
            input_references = new[]
            {
                new
                {
                    type = "image_url",
                    image_url = new
                    {
                        url = sourceDataUrl
                    }
                }
            }
        };

        return await SendImageRequestAsync(apiKey, payload, request.ModelId, cancellationToken);
    }

    private async Task<OpenRouterImageResult> SendImageRequestAsync(
        string apiKey,
        object payload,
        string modelId,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "https://openrouter.ai/api/v1/images")
        {
            Content = JsonContent.Create(payload)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"OpenRouter image request failed ({(int)response.StatusCode} {response.ReasonPhrase}).\n{errorBody}");
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        if (!json.RootElement.TryGetProperty("data", out var data)
            || data.ValueKind != JsonValueKind.Array
            || data.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("OpenRouter image response did not contain data.");
        }

        var first = data[0];
        if (!first.TryGetProperty("b64_json", out var base64Element))
        {
            throw new InvalidOperationException("OpenRouter image response did not contain b64_json.");
        }

        var base64 = base64Element.GetString();
        if (string.IsNullOrWhiteSpace(base64))
        {
            throw new InvalidOperationException("OpenRouter returned an empty image payload.");
        }

        var mediaType = first.TryGetProperty("media_type", out var mediaTypeElement)
            ? mediaTypeElement.GetString() ?? "image/png"
            : "image/png";

        double? costUsd = null;
        if (json.RootElement.TryGetProperty("usage", out var usage)
            && usage.TryGetProperty("cost", out var cost)
            && cost.TryGetDouble(out var parsedCost))
        {
            costUsd = parsedCost;
        }

        return new OpenRouterImageResult(
            Convert.FromBase64String(base64),
            mediaType,
            modelId,
            costUsd);
    }
}
