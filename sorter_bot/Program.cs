using System.Net;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using System.Text.Json;
using Serilog;
using System.IO;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 1)
    .CreateLogger();
    
using var cts = new CancellationTokenSource();
Log.Information("Reading the configuration from file");
string jsonSettings = await File.ReadAllTextAsync(Path.Combine("settings", "settings.json"));
Settings? settings = JsonSerializer.Deserialize<Settings>(jsonSettings);
Log.Information("Configuration file was read successfully");
WebProxy proxy = new (new Uri(settings?.Proxy));
HttpClient httpClient = new (
    new SocketsHttpHandler { Proxy = proxy, UseProxy = true, }
);

var bot = new TelegramBotClient(settings?.Token, httpClient);
var me = await bot.GetMe();
bot.OnMessage += OnMessage;

Log.Information("Bot is running now");

// This is for Logger to finish work correctly
try
{
    await Task.Delay(Timeout.Infinite, cts.Token);
}
catch (OperationCanceledException)
{
   Log.Information("Stopping bot...");
}
finally
{
    Log.CloseAndFlush();
}


/// <summary
/// Function to handle all messages and route them to correct sub-handlers
/// </summary>

async Task OnMessage(Message msg, UpdateType type)
{
    Log.Information($"Received {type} '{msg.Text}' in {msg.Chat}, topic {msg.MessageThreadId}");
    if (msg.Type == MessageType.Text && msg.Text.StartsWith('/'))
    {
        await HandleCommand(msg);
    }
    else
    {
        await HandleMessage(msg);
    }

}

///<summary>
/// Function to check, if message author is admin. It is needed for technical commands
/// </summary>

async Task<bool> IsAdmin(long chatId, long userId)
{
    var member = await bot.GetChatMember(chatId, userId);

    return member.Status is
        ChatMemberStatus.Administrator or
        ChatMemberStatus.Creator;
}

///<summary>
/// Function to handle technical commands. Works only for messages starting with "/"
/// </summary>

async Task HandleCommand(Message msg)
{
    Log.Information("The message is technical command. Processing.");
    string[] message_array = msg.Text.Split(' ');
    if (msg.Type==MessageType.Text && message_array[0].Equals("/setPhotoThread"))
    {
        if(await IsAdmin(msg.Chat.Id, msg.From.Id))
        {
            // Setting the media topic by its id.
            await SetPhotoThread(msg);
        }
        else
        {
            Log.Information("User does not have rights to perform this operation.");
            await bot.SendMessage(msg.Chat.Id, "Только администратор может делать это!");
        }
    }
    else if (msg.Type==MessageType.Text && message_array[0].Equals("/setTextThread"))
    {
        if(await IsAdmin(msg.Chat.Id, msg.From.Id))
        {
            // Setting the text topic by its id.
            await SetTextThread(msg);
        }
        else
        {
            Log.Information("User does not have rights to perform this operation.");
            await bot.SendMessage(msg.Chat.Id, "Только администратор может делать это!");
        }
    }
}

///<summary>
/// Function, which sets the thread, where all text messages from media thread will be forwarded. The thread id is received from message of the command. Text thread will be one, where the command was used last time.
///</summary>


async Task SetTextThread(Message msg)
{
    settings.TextThread = (int)msg.MessageThreadId;
    await bot.SendMessage(msg.Chat.Id, "Теперь сюда будут пересылаться текстовые сообщения!");
    jsonSettings = JsonSerializer.Serialize(settings);
    Log.Information("Settings updated!");
    await File.WriteAllTextAsync("settings.json", jsonSettings);
    Log.Information("New settings have been written to config file");
}
///<summary>
/// Function, which sets the thread, where all media messages from text thread will be forwarded. The thread id is received from message of the command. Text thread will be one, where the command was used last time.
///</summary>
async Task SetPhotoThread(Message msg)
{
    settings.PhotoThread = (int)msg.MessageThreadId;
    await bot.SendMessage(msg.Chat.Id, "Теперь сюда будут пересылаться фото и видео!");
    jsonSettings = JsonSerializer.Serialize(settings);
    Log.Information("Settings updated!");
    await File.WriteAllTextAsync("settings.json", jsonSettings);
    Log.Information("New settings have been written to config file");

}

///<summary>
/// Function, which will handle all normal, i.e. non-command messages. The message is checked for its topic and content to match. If the do not, message is forwarded to correct topic and old message is deleted.
///</summary>
async Task HandleMessage(Message msg)
{
    if (settings.TextThread == 0 || settings.PhotoThread == 0)
    {
        Log.Information("One of the threads is not set. Message will not be processed.");
        await bot.SendMessage(msg.Chat.Id, "Сначала нужно указать топики для сообщений с медиа и текстом. Используйте команды /setPhotoThread и /setTextThread в нужных топиках.");
    }
    else if (msg.MessageThreadId == settings.PhotoThread && msg.Type == MessageType.Text)
    {
            await bot.ForwardMessage(msg.Chat.Id, msg.Chat.Id, msg.Id, messageThreadId: settings.TextThread, disableNotification: true);
            Log.Information("Forwarded text message to correct topic");
            await bot.DeleteMessage(msg.Chat.Id, msg.Id);
            Log.Information("Deleted the old message");

    }
    else if (msg.MessageThreadId == settings.TextThread && (msg.Type == MessageType.Photo || msg.Type == MessageType.Video || msg.Type == MessageType.VideoNote))
    {
            await bot.ForwardMessage(msg.Chat.Id, msg.Chat.Id, msg.Id, messageThreadId: settings.PhotoThread, disableNotification: true);
            Log.Information("Forwarded media message to correct topic");
            await bot.DeleteMessage(msg.Chat.Id, msg.Id);
            Log.Information("Deleted the old message");

    }
}

/// <summary>
/// Just a class to work with config in json. It saves ids of both thereads, bot token and proxy url.
/// </summary>
class Settings
{
    public int TextThread { get; set; }
    public int PhotoThread { get; set; }
    public string Token { get;  set; } = "";
    public string Proxy { get; set; } = "";
}