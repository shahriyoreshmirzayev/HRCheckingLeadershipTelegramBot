using CheckingLeadershipTelegramBot.Services;
using Telegram.Bot;
using Telegram.Bot.Types;
using CheckingLeadershipTelegramBot.Services;
using Telegram.Bot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

// ✅ Telegram botni fonda ishga tushirish
builder.Services.AddSingleton<TelegramBotClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    string botToken = config["BotConfig:Token"];
    return new TelegramBotClient(botToken);
});

builder.Services.AddHostedService<BotBackgroundService>(); // ✅ Long Polling uchun servis

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();
app.Run();