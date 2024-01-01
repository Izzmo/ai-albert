using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace AIbert.Models;

public record ChatThread(IList<Chat> chats, IList<Promise> promises)
{
    public string threadId = string.Empty;

    [JsonIgnore]
    public IEnumerable<string> users => chats.Select(c => c.userId.ToLowerInvariant()).Where(c => c != "aibert").Distinct();

    public bool HasChangedSinceLastCheck = false;

    public void AddChat(Chat newChat)
    {
        chats.Add(newChat);
    }

    public static string GetThreadIdFromUsers(IEnumerable<string> users)
    {
        var sortedUsers = users.Select(c => c.ToLowerInvariant()).Where(c => c != "aibert").OrderBy(u => u).Distinct();
        using SHA256 sha256Hash = SHA256.Create();
        byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("", sortedUsers)));

        StringBuilder builder = new();
        for (int i = 0; i < bytes.Length; i++)
        {
            builder.Append(bytes[i].ToString("x2"));
        }

        return builder.ToString();
    }
}
