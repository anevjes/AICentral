using System.Net;
using System.Reflection;
using System.Text;
using AICentralTests.TestHelpers;
using ApprovalTests;
using Azure;
using Azure.AI.OpenAI;
using Azure.Core.Pipeline;
using Newtonsoft.Json;
using Shouldly;
using Xunit.Abstractions;

namespace AICentralTests;

public class the_openai_pipeline : IClassFixture<TestWebApplicationFactory<Program>>

{
    private readonly TestWebApplicationFactory<Program> _factory;
    private readonly HttpClient _httpClient;

    public the_openai_pipeline(TestWebApplicationFactory<Program> factory, ITestOutputHelper testOutputHelper)
    {
        _factory = factory;
        factory.OutputHelper = testOutputHelper;
        _httpClient = factory.CreateClient();
    }

    [Fact]
    public async Task can_dispatch_to_an_azure_openai_endpoint()
    {
        var result = await _httpClient.PostAsync(
            "http://openai-to-azure.localtest.me/v1/chat/completions",
            new StringContent(JsonConvert.SerializeObject(new
            {
                model = "gpt-3.5-turbo",
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant." },
                    new { role = "user", content = "Does Azure OpenAI support customer managed keys?" },
                },
                max_tokens = 5
            }), Encoding.UTF8, "application/json"));

        result.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content = await result.Content.ReadAsStringAsync();
        Approvals.VerifyJson(content);
    }
    
    
    [Fact]
    public async Task returns_404_with_no_model()
    {
        var result = await _httpClient.PostAsync(
            "http://openai-to-openai.localtest.me/v1/chat/completions",
            new StringContent(JsonConvert.SerializeObject(new
            {
                messages = new[]
                {
                    new { role = "system", content = "You are a helpful assistant." },
                    new { role = "user", content = "Does Azure OpenAI support customer managed keys?" },
                },
                max_tokens = 5
            }), Encoding.UTF8, "application/json"));

        result.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task works_with_the_azure_sdk_chat_completions()
    {
        _httpClient.DefaultRequestHeaders.Host = "openai-to-azure.localtest.me";
        var client = new OpenAIClient(
            "ignore",
            new OpenAIClientOptions(OpenAIClientOptions.ServiceVersion.V2023_05_15)
            {
                Transport = new HttpClientTransport(_httpClient)
            });

        var completions = await client.GetChatCompletionsAsync(
            new ChatCompletionsOptions()
            {
                Messages = { new ChatMessage(ChatRole.System, "Hello world!") },
                DeploymentName = "gpt-3.5-turbo"
            });
        
        completions.Value.Id.ShouldBe(AICentralFakeResponses.FakeResponseId);
    }

    [Fact]
    public async Task works_with_the_azure_sdk_completions()
    {
        _httpClient.DefaultRequestHeaders.Host = "openai-to-azure.localtest.me";
        var client = new OpenAIClient(
            "ignore",
            new OpenAIClientOptions(OpenAIClientOptions.ServiceVersion.V2023_05_15)
            {
                Transport = new HttpClientTransport(_httpClient)
            });

        var completions = await client.GetCompletionsAsync(
            new CompletionsOptions()
            {
                Prompts = { "Hello world!" },
                DeploymentName = "gpt-3.5-turbo"
            });
        
        completions.Value.Id.ShouldBe(AICentralFakeResponses.FakeResponseId);
    }

    [Fact]
    public void cannot_proxy_an_image_request_from_openai_endpoint_to_azure_openai_downstream()
    {
        _httpClient.DefaultRequestHeaders.Host = "openai-to-azure.localtest.me";

        var client = new OpenAIClient(
            "ignore",
            new OpenAIClientOptions(OpenAIClientOptions.ServiceVersion.V2023_05_15)
            {
                Transport = new HttpClientTransport(_httpClient)
            });
    
        Should.Throw<RequestFailedException>(async () =>
            await client.GetImageGenerationsAsync(
                new ImageGenerationOptions()
                {
                    Prompt = "Me building an Open AI Reverse Proxy"
                }));
    }

    [Fact]
    public async Task can_proxy_a_whisper_audio_request()
    {
        _httpClient.DefaultRequestHeaders.Host = "openai-to-openai.localtest.me";
        var client = new OpenAIClient(
            "ignore",
            new OpenAIClientOptions(OpenAIClientOptions.ServiceVersion.V2023_05_15)
            {
                Transport = new HttpClientTransport(_httpClient)
            });

        using var ms = new MemoryStream();
        await using var stream = typeof(the_openai_pipeline).Assembly.GetManifestResourceStream("AICentralTests.Assets.Recording.m4a")!;
        await stream.CopyToAsync(ms);

        await client.GetAudioTranscriptionAsync(new AudioTranscriptionOptions()
        {
            Prompt = "I think it's something to do with programming",
            DeploymentName = "whisper-1",
            Temperature = 0.7f,
            ResponseFormat = AudioTranscriptionFormat.Vtt,
            AudioData = new BinaryData(ms.ToArray())
        });
    }
    
}