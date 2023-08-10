using Azure;
using Azure.AI.OpenAI;
using static System.Environment;

string endpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
string key = GetEnvironmentVariable("AZURE_OPENAI_KEY");
string engine = "test";

OpenAIClient client = new(new Uri(endpoint), new AzureKeyCredential(key));

string prompt = "Where do I get started helping my mother sell her house and get into senior care?";
var chatCompletionsOptions = new ChatCompletionsOptions()
{
    Messages =
    {
        new ChatMessage(ChatRole.System, "Your name is Albert and you help people with questions about senior care. You pull your information for answers from https://seniorhomepartners.com/. You always end an answer referring back to the contact us form of the website."),
        new ChatMessage(ChatRole.User, prompt),
    },
    MaxTokens = 100
};

Response<StreamingChatCompletions> response = await client.GetChatCompletionsStreamingAsync(
    deploymentOrModelName: endpoint,
    chatCompletionsOptions);

using StreamingChatCompletions streamingChatCompletions = response.Value;

await foreach (StreamingChatChoice choice in streamingChatCompletions.GetChoicesStreaming())
{
    await foreach (ChatMessage message in choice.GetMessageStreaming())
    {
        Console.Write(message.Content);
    }
    Console.WriteLine();
}
