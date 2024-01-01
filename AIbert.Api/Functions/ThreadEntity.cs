﻿using System.Text.Json;
using AIbert.Api.Services;
using AIbert.Models;

namespace AIbert.Api.Functions;

public class ThreadEntity : BaseEntity
{
    public string Users { get; set; } = string.Empty;
    public string Chats { get; set; } = string.Empty;
    public string Promises { get; set; } = string.Empty;

    public static ThreadEntity ConvertFromChatThread(ChatThread thread)
    {
        return new ThreadEntity
        {
            PartitionKey = thread.threadId,
            RowKey = thread.threadId,
            Users = JsonSerializer.Serialize(thread.users),
            Chats = JsonSerializer.Serialize(thread.chats),
            Promises = JsonSerializer.Serialize(thread.promises),
        };
    }

    public ChatThread ConvertTo()
    {
        return new ChatThread(
            Chats.Length > 0 ? JsonSerializer.Deserialize<IList<Chat>>(Chats) : new List<Chat>(),
            Promises.Length > 0 ? JsonSerializer.Deserialize<IList<Promise>>(Promises) : new List<Promise>()
        );
    }
}
