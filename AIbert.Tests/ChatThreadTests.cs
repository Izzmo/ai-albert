using AIbert.Models;

namespace AIbert.Tests;

public class ChatThreadTests
{
    private readonly List<Chat> chats = new()
    {
        new(Guid.NewGuid(), "Can you send me over that report tomorrow?", "Alice", DateTimeOffset.UtcNow),
        new(Guid.NewGuid(), "Sure, what time?", "5124452945", DateTimeOffset.UtcNow.AddMinutes(5)),
        new(Guid.NewGuid(), "9am", "Alice", DateTimeOffset.UtcNow.AddMinutes(10)),
        new(Guid.NewGuid(), "Sounds good.", "5124452945", DateTimeOffset.UtcNow.AddMinutes(15)),
        new(Guid.NewGuid(), "Hey Alice and 5124452945, just wanted to confirm the promise to get the report. The deadline is tomorrow at 9pm. Please let me know if there are any changes or if you need any further clarification. Thanks!", "AIbert", DateTimeOffset.UtcNow.AddMinutes(30)),
    };
    private readonly List<Promise> promises = new()
    {
        new(Guid.NewGuid(), "Send reports", DateTimeOffset.UtcNow.AddMinutes(30).ToString(), "5124452945", "Alice")
    };
    private readonly ChatThread thread;

    public ChatThreadTests()
    {
        thread = new(chats, promises);
    }

    [Fact]
    public void ChatsInAscendingOrder()
    {
        var previousChat = thread.chats.First();
        foreach (var chat in thread.chats.Skip(1))
        {
            Assert.True(chat.timestamp > previousChat.timestamp);
            previousChat = chat;
        }
    }

    [Fact]
    public void CanAddChatToThread()
    {
        Chat newChat = new(Guid.NewGuid(), "How is it going?", "Alice", DateTimeOffset.UtcNow.AddDays(1));
        thread.AddChat(newChat);

        Assert.Contains(newChat, thread.chats);
    }
}
