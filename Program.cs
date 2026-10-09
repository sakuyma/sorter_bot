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

if (settings is null)
{
    Log.Fatal("Settings could not be loaded");
    return;
}

Log.Information("Configuration file was read successfully");
Log.Information("Proxy from settings: {Proxy}", settings.Proxy);
Log.Information("Token from settings: {Token}", settings.Token);
Log.Information("TextThread: {Text}, PhotoThread: {Photo}", settings.TextThread, settings.PhotoThread);

// --- Прокси ---
var proxyUri = new Uri(settings.Proxy);
var proxy = new WebProxy(proxyUri);

var handler = new SocketsHttpHandler
{
    Proxy = proxy,
    UseProxy = true,
};

var httpClient = new HttpClient(handler);

var bot = new TelegramBotClient(settings.Token, httpClient);
var me = await bot.GetMe();
Log.Information("Bot @{Username} is running...", me.Username);

bot.OnMessage += OnMessage;

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

// ----- Handlers -----

async Task OnMessage(Message msg, UpdateType type)
{
    Log.Information(
        "Received {Type} '{Text}' in {Chat}, topic {Topic}",
        type, msg.Text, msg.Chat, msg.MessageThreadId);

    if (msg.Type == MessageType.Text && msg.Text is not null && msg.Text.StartsWith('/'))
    {
        await HandleCommand(msg);
    }
    else
    {
        await HandleMessage(msg);
    }
}

async Task HandleCommand(Message msg)
{
    if (msg.Text == "/start")
    {
        await bot.SendMessage(
            chatId: msg.Chat.Id,
            text: "Привет! Бот запущен и готов к работе.",
            messageThreadId: msg.MessageThreadId);
    }
}

async Task HandleMessage(Message msg)
{
    var chatId = msg.Chat.Id;
    var messageId = msg.Id;
    var threadId = msg.MessageThreadId;

    // Тема 2 → текст в тему 4
    if (threadId == settings.TextThread && msg.Text is not null)
    {
        await bot.ForwardMessage(
            chatId: chatId,
            fromChatId: chatId,
            messageId: messageId,
            messageThreadId: settings.PhotoThread,
            disableNotification: true);

        await bot.DeleteMessage(chatId: chatId, messageId: messageId);

        Log.Information("Forwarded text {Id} from topic {From} to {To}",
            messageId, settings.TextThread, settings.PhotoThread);
    }
    // Тема 4 → всё кроме фото/видео/видеосообщений в тему 2
    else if (threadId == settings.PhotoThread
             && msg.Photo is null
             && msg.Video is null
             && msg.VideoNote is null)
    {
        await bot.ForwardMessage(
            chatId: chatId,
            fromChatId: chatId,
            messageId: messageId,
            messageThreadId: settings.TextThread,
            disableNotification: true);

        await bot.DeleteMessage(chatId: chatId, messageId: messageId);

        Log.Information("Forwarded {Id} from topic {From} to {To}",
            messageId, settings.PhotoThread, settings.TextThread);
    }
}

// ----- Settings model -----

public class Settings
{
    public int TextThread { get; set; }
    public int PhotoThread { get; set; }
    public string Token { get; set; } = "";
    public string Proxy { get; set; } = "";
}
