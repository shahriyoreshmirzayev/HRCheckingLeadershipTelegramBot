using CheckingLeadershipTelegramBot.Entities;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CheckingLeadershipTelegramBot.Services
{
    public class TelegramService
    {
        private readonly ITelegramBotClient _botClient;

        public TelegramService(ITelegramBotClient botClient)
        {
            _botClient = botClient;
        }

        public async Task SendUserInfoImageAsync(long chatId, CandidateInfoEntities userResponse, int percentage)
        {
            //object ImageGenerator = null;
            //string filePath = ImageGenerator.GenerateUserInfoImage(userResponse, percentage);

            //await _botClient.SendPhotoAsync(chatId, InputFile.FromFileId(filePath), caption: "📄 Sizning ma’lumotlaringiz");
        }

        internal async Task SendUserInfoImageAsync(object chatId, UserResponse userResponse, int v)
        {
            throw new NotImplementedException();
        }
    }
}
