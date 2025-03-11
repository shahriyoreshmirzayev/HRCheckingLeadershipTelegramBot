using CheckingLeadershipTelegramBot.Entities;
using System.Globalization;
using System.Text.RegularExpressions;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace CheckingLeadershipTelegramBot.Services;

public class BotBackgroundService : BackgroundService
{
    private readonly TelegramBotClient _botClient;
    private readonly Dictionary<long, CandidateInfoEntities> _userResponses = new();

    public BotBackgroundService(TelegramBotClient botClient)
    {
        _botClient = botClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        try
        {
            _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions, stoppingToken);
            Console.WriteLine("✅ Bot ishlayapti...");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("❌ Bot to'xtatildi...");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Xatolik yuz berdi: {ex.Message}");
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        long chatId = 0;

        // Handling Message updates
        if (update.Message is { } message)
        {
            chatId = message.Chat.Id;
            string messageText = message.Text ?? "";

            if (messageText == "/start")
            {
                var replyKeyboard = new ReplyKeyboardMarkup(new[] { new KeyboardButton[] { "Qobiliyatlarni baholash" } })
                {
                    ResizeKeyboard = true
                };

                await botClient.SendTextMessageAsync(chatId, "Salom! Quyidagi tugmani bosing:", replyMarkup: replyKeyboard, cancellationToken: cancellationToken);
            }
            else if (messageText == "Qobiliyatlarni baholash")
            {
                _userResponses[chatId] = new CandidateInfoEntities();
                await botClient.SendTextMessageAsync(chatId, "Familiyangizni kiriting:", cancellationToken: cancellationToken);
            }
            else if (_userResponses.ContainsKey(chatId))
            {
                await ProcessUserInfo(botClient, chatId, messageText, cancellationToken);
            }
        }
        // Handling Callback updates
        else if (update.CallbackQuery is { } callback)
        {
            chatId = callback.Message.Chat.Id;
            string data = callback.Data;

            if (data.StartsWith("comment_"))
            {
                int questionIndex = int.Parse(data.Split('_')[1]);
                await botClient.SendTextMessageAsync(chatId, "✍️ Izohingizni yozing:", cancellationToken: cancellationToken);
            }
            else if (data.StartsWith("next_"))
            {
                int questionIndex = int.Parse(data.Split('_')[1]);
                await SendTestQuestionAsync(botClient, chatId, questionIndex + 1, cancellationToken);
            }
            else if (callback.Data == "confirm_yes")
            {
                await botClient.SendTextMessageAsync(chatId, "Tabriklayman, siz keyingi bosqichdasiz!", cancellationToken: cancellationToken);
                _userResponses.Remove(chatId);
                await SendTestQuestionAsync(botClient, chatId, 0, cancellationToken);
            }
            else if (callback.Data == "confirm_no")
            {
                await botClient.SendTextMessageAsync(chatId, "Iltimos, to'liq to'ldiring va qayta yuboring.", cancellationToken: cancellationToken);
                _userResponses.Remove(chatId);
                _userResponses[chatId] = new CandidateInfoEntities();
                await botClient.SendTextMessageAsync(chatId, "Familiyangizni kiriting:", cancellationToken: cancellationToken);
            }
        }
    }

    //private Dictionary<long, UserResponse> _userResponses = new();

    private async Task ProcessUserInfo(ITelegramBotClient botClient, long chatId, string messageText, CancellationToken cancellationToken)
    {
        var userResponse = _userResponses[chatId];

        if (userResponse.FamilyName == null)
        {
            if (!IsValidName(messageText))
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Familiyangizni to'g'ri kiriting (Birinchi harf katta bo‘lishi kerak).", cancellationToken: cancellationToken);
                return;
            }

            userResponse.FamilyName = messageText;
            await botClient.SendTextMessageAsync(chatId, "Ismingizni kiriting:", cancellationToken: cancellationToken);
        }
        else if (userResponse.FirstName == null)
        {
            if (!IsValidName(messageText))
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Ismingizni to'g'ri kiriting (Birinchi harf katta bo‘lishi kerak).", cancellationToken: cancellationToken);
                return;
            }

            userResponse.FirstName = messageText;
            await botClient.SendTextMessageAsync(chatId, "Otasining ismini kiriting:", cancellationToken: cancellationToken);
        }
        else if (userResponse.FathersName == null)
        {
            if (!IsValidName(messageText))
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Otangizning ismini to'g'ri kiriting (Birinchi harf katta bo‘lishi kerak).", cancellationToken: cancellationToken);
                return;
            }

            userResponse.FathersName = messageText;
            await botClient.SendTextMessageAsync(chatId, "Tug'ilgan sanangizni kiriting (dd-MM-yyyy):", cancellationToken: cancellationToken);
        }
        else if (string.IsNullOrEmpty(userResponse.BirthDate))
        {
            if (DateTime.TryParseExact(messageText, "dd-MM-yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime birthDate))
            {
                userResponse.BirthDate = birthDate.ToString("dd-MM-yyyy"); // Formatni saqlash
                await botClient.SendTextMessageAsync(chatId, "Telefon raqamingizni kiriting \n+998XX XXX XX XX:", cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendTextMessageAsync(chatId, "Noto‘g‘ri sana formati! Tug‘ilgan sanani 'dd-MM-yyyy' formatida kiriting.", cancellationToken: cancellationToken);
            }
        }
        else if (userResponse.PhoneNumber == null)
        {
            if (!IsValidPhoneNumber(messageText))
            {
                await botClient.SendTextMessageAsync(chatId, "❌ Telefon raqamni to'g'ri formatda kiriting: +998XX XXX XX XX", cancellationToken: cancellationToken);
                return;
            }

            userResponse.PhoneNumber = messageText;
            await botClient.SendTextMessageAsync(chatId, "Qaysi lavozimga ariza berayotganingizni kiriting:", cancellationToken: cancellationToken);
        }
        else if (userResponse.Position == null)
        {
            userResponse.Position = messageText;

            // **Admin ID-ga xabar yuborish**
            await SendUserInfoToAdmin(botClient, userResponse);
            /*if (userResponse.Position == null)
            {
                userResponse.Position = messageText;

                // ✅ Ma'lumotni admin va kanalga yuborish
                await SendUserInfoToAdmin(botClient, userResponse);
            }*/


            string confirmationMessage = $"📋 *Nomzod haqida ma'lumot*\n\n" +
                $"👤 *F.I.O:* {userResponse.FamilyName} {userResponse.FirstName} {userResponse.FathersName}\n" +
                $"📅 *Tug‘ilgan sana:* {userResponse.BirthDate}\n" +
                $"📞 *Telefon raqam:* {userResponse.PhoneNumber}\n" +
                $"💼 *Lavozim:* {userResponse.Position}\n\n" +
                $"Barcha ma'lumotlar to‘g‘rimi?\n\n" +
                "✅ *Ha* - Ma'lumotlar to‘g‘ri\n" +
                "❌ *Yo‘q* - Qayta to‘ldirish";

            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
            new[]
            {
                InlineKeyboardButton.WithCallbackData("✅ Ha", "confirm_yes"),
                InlineKeyboardButton.WithCallbackData("❌ Yo'q", "confirm_no")
            }
        });

            await botClient.SendTextMessageAsync(chatId, confirmationMessage, parseMode: ParseMode.Markdown, replyMarkup: inlineKeyboard, cancellationToken: cancellationToken);
        }
    }


    private async Task SendUserInfoToAdmin(ITelegramBotClient botClient, CandidateInfoEntities userResponse)
    {
        long adminChatId = 1585317019; // ⬅ O'zingizning Telegram ID
        long channelChatId = -1002496370860; // ⬅ O'zingizning kanal ID

        string userInfoMessage = $"📥 *Yangi nomzod ma'lumotlari:*\n\n" +
            $"👤 *F.I.O:* {userResponse.FamilyName} {userResponse.FirstName} {userResponse.FathersName}\n" +
            $"📅 *Tug‘ilgan sana:* {userResponse.BirthDate}\n" +
            $"📞 *Telefon:* {userResponse.PhoneNumber}\n" +
            $"💼 *Lavozim:* {userResponse.Position}\n\n" +
            "🆕 *Yangi ariza kelib tushdi!*";

        try
        {
            // Adminga yuborish
            await botClient.SendTextMessageAsync(adminChatId, userInfoMessage, parseMode: ParseMode.Markdown);

            // Kanalingizga yuborish
            await botClient.SendTextMessageAsync(channelChatId, userInfoMessage, parseMode: ParseMode.Markdown);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Xabar yuborishda xatolik: {ex.Message}");
        }
    }


    // ✅ **Ism, familiya va otasining ismini tekshiruvchi funksiya**
    private bool IsValidName(string name)
    {
        return char.IsUpper(name[0]);
    }

    // ✅ **Telefon raqamni tekshiruvchi funksiya**
    private bool IsValidPhoneNumber(string phoneNumber)
    {
        return Regex.IsMatch(phoneNumber, @"^\+998\d{2} \d{3} \d{2} \d{2}$");
    }






    private async Task SendTestQuestionAsync(ITelegramBotClient botClient, long chatId, int questionIndex, CancellationToken cancellationToken)
    {
        var questions = new List<string>
        {
            "Jamoada ishlash davomida biron bir fikr yoki taklifni o‘rtaga tashlaganingiz va uni amalga oshirishda liderlikni zimmangizga olib, boshqa odamlarni jalb qilganingiz haqida gapirib bering.",
            "Odamlar bilan bir guruhda ishlayotganingizda ularning yo‘lida turgan ba’zi muammolarni hal qilish orqali hamkasblaringizga yordam berganingiz haqida aytib bering.",
            "Bir guruhni boshqarganingiz va bu guruh a’zolari orqali qanday natijalarga erishganingizga misol keltiring.",
            "Muhim narsaga erishish uchun mashg‘ulishingizni xavf ostiga qo‘ygan vaqtingiz haqida aytib bering."
        };

        if (questionIndex >= questions.Count)
        {
            await botClient.SendTextMessageAsync(chatId, "✅ Savollar tugadi! Rahmat.", cancellationToken: cancellationToken);
            return;
        }

        var keyboard = new InlineKeyboardMarkup(new[] {
            new[] {
                InlineKeyboardButton.WithCallbackData("yaxshi", $"rate_1_{questionIndex}"),
                InlineKeyboardButton.WithCallbackData("a'lo", $"rate_2_{questionIndex}"),
                InlineKeyboardButton.WithCallbackData("o'rtacha", $"rate_3_{questionIndex}"),
                InlineKeyboardButton.WithCallbackData("qoniqarli", $"rate_4_{questionIndex}"),
                InlineKeyboardButton.WithCallbackData("qoniqarsiz", $"rate_5_{questionIndex}")
            },
            new[] { InlineKeyboardButton.WithCallbackData("📝 Izoh qo'shish", $"comment_{questionIndex}") },
            new[] { InlineKeyboardButton.WithCallbackData("➡️ Keyingi savol", $"next_{questionIndex}") }
        });

        await botClient.SendTextMessageAsync(chatId, questions[questionIndex], replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    private Task HandleErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error: {exception.Message}");
        return Task.CompletedTask;
    }

}
public class UserResponse
{
    public string? FamilyName { get; set; }
    public string? FirstName { get; set; }
    public string? FathersName { get; set; }
    public string? BirthDate { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Position { get; set; }
}
