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
    private Dictionary<long, List<int>> userRatings = new Dictionary<long, List<int>>();


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

            else if (data.StartsWith("rate_")) // ✅ Foydalanuvchi baho qo‘yganda
            {
                await HandleCallbackQueryAsync(botClient, callback, cancellationToken);

                // Bahoni olishga harakat qilamiz
                if (!int.TryParse(data.Replace("rate_", ""), out int userRating))
                {
                    return;
                }

                // Yangi bahoni hisoblash uchun listga qo‘shamiz
                List<int> userRatings = new List<int> { userRating };

                // O‘rtacha foizni hisoblash
                string result = await CalculateAverageRatingPercentageAsync(userRatings);

                await botClient.SendTextMessageAsync(
                    chatId: callback.Message.Chat.Id,
                    text: result,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken
                );
            }





            else if (callback.Data == "confirm_yes")
            {
                await botClient.SendTextMessageAsync(chatId, "Tabriklayman, siz keyingi bosqichdasiz!", cancellationToken: cancellationToken);
                _userResponses.Remove(chatId);
                await SendQuestionInformationAsync(botClient, chatId, 0, cancellationToken);
            }

            // "Testni boshlash" tugmasi bosilganda testni yuborish
            else if (callback.Data == "start_test")
            {
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
    private async Task OnCallbackQueryReceived(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        if (callbackQuery.Message == null)
        {
            Console.WriteLine("❌ Xatolik: CallbackQuery.Message null!");
            return;
        }

        var chatId = callbackQuery.Message.Chat.Id;
        var data = callbackQuery.Data;

        Console.WriteLine($"📩 Callback Query: {data}"); // Debug uchun

        if (data.StartsWith("next_"))
        {
            int questionIndex = int.Parse(data.Split('_')[1]) + 1;
            await SendTestQuestionAsync(botClient, chatId, questionIndex, cancellationToken);
        }
        else if (data.StartsWith("rate_"))
        {
            await botClient.SendTextMessageAsync(chatId, "✅ Sizning bahoyingiz qabul qilindi!", cancellationToken: cancellationToken);
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



    private async Task SendQuestionInformationAsync(ITelegramBotClient botClient, long chatId, int informationIndex, CancellationToken cancellationToken)
    {
        string message = "📝 *I. Liderlik* – rahbarlik qilish va boshqalarni ergashtira olish qobiliyati\n\n" +
                         "📌 Yangi imkoniyatlarni ko‘radi, hodisalarning borishini o‘zgartirish, yaxshi natijalarga erishish haqida tasavvur hosil qiladi va maqsadga erishish jarayoniga boshqalarni jalb qila oladi.\n\n" +
                         "📌 Qo‘l ostidagilarning qobiliyatlarini to‘la ro’yobga chiqarish uchun sharoit yaratadi, yordam beradi, ularga to‘sqinlik qiluvchi muammolarni hal qiladi.\n\n" +
                         "📌 Haqiqatdan qochmaydi, qiyin qarorlar qabul qiladi, ular an’anaviy bo‘lmasa ham, lekin maqsadlarga erishishga olib keluvchi qarorlarni ko‘zlaydi.";

        var inlineKeyboard = new InlineKeyboardMarkup(new[]
        {
            new[] { InlineKeyboardButton.WithCallbackData("✅ Testni boshlash", "start_test") }
        }
        );

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: message,
            parseMode: Telegram.Bot.Types.Enums.ParseMode.Markdown,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken
        );
    }



    // ✅ Savolni jo'natish funksiyasi
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
            await SendFinalRatingAsync(botClient, chatId, cancellationToken);
            return;
        }



        var keyboard = GenerateKeyboard(questionIndex);
        await botClient.SendTextMessageAsync(chatId, $"📝 {questions[questionIndex]}", replyMarkup: keyboard, cancellationToken: cancellationToken);
    }

    // ✅ Inline keyboard generatsiya qilish funksiyasi (1 dan 5 gacha)
    private InlineKeyboardMarkup GenerateKeyboard(int questionIndex)
    {
        return new InlineKeyboardMarkup(new[]
        {
        new[]
        {
            InlineKeyboardButton.WithCallbackData("1️", $"rate_1_0{questionIndex}"),
            InlineKeyboardButton.WithCallbackData("2️", $"rate_2_0{questionIndex}"),
            InlineKeyboardButton.WithCallbackData("3️", $"rate_3_0{questionIndex}"),
            InlineKeyboardButton.WithCallbackData("4️", $"rate_4_0{questionIndex}"),
            InlineKeyboardButton.WithCallbackData("5️", $"rate_5_0{questionIndex}")
        },
    });
    }

    // ✅ Callback query ishlov berish
    private async Task HandleCallbackQueryAsync(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        var data = callbackQuery.Data;

        if (data.StartsWith("rate_"))
        {
            var parts = data.Split('_');
            if (parts.Length == 3 && int.TryParse(parts[1], out int rating) && int.TryParse(parts[2], out int questionIndex))
            {
                if (!userRatings.ContainsKey(chatId))
                {
                    userRatings[chatId] = new List<int>();
                }
                userRatings[chatId].Add(rating); // ✅ Bahoni saqlash

                await botClient.AnswerCallbackQueryAsync(callbackQuery.Id, $"✅ Baho {rating} saqlandi!", cancellationToken: cancellationToken);
                await SendTestQuestionAsync(botClient, chatId, questionIndex + 1, cancellationToken);
            }
        }
        else if (data.StartsWith("next_"))
        {
            var parts = data.Split('_');
            if (parts.Length == 2 && int.TryParse(parts[1], out int questionIndex))
            {
                await SendTestQuestionAsync(botClient, chatId, questionIndex + 1, cancellationToken);
            }
        }
    }

    private async Task SendFinalRatingAsync(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        int totalQuestions = 0;

        if (userRatings.ContainsKey(chatId) && userRatings[chatId].Count > 0)
        {
            double averageRating = userRatings[chatId].Average();
            string resultMessage = $"📊 Sizning umumiy bahoingiz: {averageRating:F1} / 5.0";

            await botClient.SendTextMessageAsync(chatId, resultMessage, cancellationToken: cancellationToken);


        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Hech qanday baho kiritilmadi.", cancellationToken: cancellationToken);
        }

        if (userRatings.ContainsKey(chatId) && userRatings[chatId].Count > 0)
        {
            // Baholarni foizga aylantirish uchun sozlangan foizlar
            var fixedPercentages = new Dictionary<int, int>
        {
            { 1, 20 }, { 2, 40 }, { 3, 60 }, { 4, 80 }, { 5, 100 }
        };

            double totalPercentage = 0;

            foreach (var rating in userRatings[chatId])
            {
                if (fixedPercentages.TryGetValue(rating, out int percentage))
                {
                    totalPercentage += percentage;
                }
            }

            double averagePercentage = totalPercentage / userRatings[chatId].Count;
            double questionCoverage = (double)userRatings[chatId].Count / totalQuestions * 100;

            string resultMessage = $"📊 Sizning umumiy baho foizingiz: {averagePercentage:F1}% / 100 %";

            await botClient.SendTextMessageAsync(chatId, resultMessage, cancellationToken: cancellationToken);

            // ✅ Baholarni tozalash
            userRatings.Remove(chatId);
        }
        else
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Hech qanday baho kiritilmadi.", cancellationToken: cancellationToken);
        }
    }

    private async Task<string> CalculateAverageRatingPercentageAsync(List<int> userRatings)
    {
        await Task.Delay(10); // Asinxron ishlash uchun kichik kutish

        if (userRatings == null || userRatings.Count == 0)
            return "❌ Baholar topilmadi.";

        var fixedPercentages = new Dictionary<int, int>
    {
        { 1, 20 }, { 2, 40 }, { 3, 60 }, { 4, 80 }, { 5, 100 }
    };

        double totalPercentage = 0;

        foreach (var rating in userRatings)
        {
            if (fixedPercentages.TryGetValue(rating, out int percentage))
            {
                totalPercentage += percentage;
            }
            else
            {
                Console.WriteLine($"❌ Noto‘g‘ri baho: {rating}"); // Xatolikni tekshirish
            }
        }

        double averagePercentage = totalPercentage / userRatings.Count;

        return $"📊 *Sizning umumiy baho foizingiz:* {averagePercentage:F1}%";
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