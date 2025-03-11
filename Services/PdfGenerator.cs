using CheckingLeadershipTelegramBot.Entities;
using iText.IO.Font.Constants;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Layout.Element;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CheckingLeadershipTelegramBot.Services;

public class PdfGenerator
{
    public static async Task SendUserInfoPdfAsync(ITelegramBotClient botClient, long chatId, CandidateInfoEntities userResponse)
    {
        if (userResponse == null)
        {
            await botClient.SendTextMessageAsync(chatId, "❌ Xatolik: Ma'lumotlar topilmadi.");
            return;
        }

        try
        {
            using (MemoryStream memoryStream = new MemoryStream())
            {
                using (var writer = new PdfWriter(memoryStream))
                using (var pdf = new PdfDocument(writer))
                using (var document = new iText.Layout.Document(pdf))
                {
                    PdfFont boldFont = PdfFontFactory.CreateFont(StandardFonts.HELVETICA_BOLD);

                    document.Add(new Paragraph("📄 Nomzod haqida ma'lumot")
                        .SetFont(boldFont)
                        .SetFontSize(18));

                    document.Add(new Paragraph($"👤 F.I.O: {userResponse.FamilyName} {userResponse.FirstName} {userResponse.FathersName}"));
                    document.Add(new Paragraph($"📅 Tug‘ilgan sana: {userResponse.BirthDate}"));
                    document.Add(new Paragraph($"📞 Telefon raqam: {userResponse.PhoneNumber}"));
                    document.Add(new Paragraph($"💼 Lavozim: {userResponse.Position}"));
                }

                // Faylni tayyorlash
                memoryStream.Position = 0;
                InputFile file = InputFile.FromStream(memoryStream, "UserInfo.pdf");

                // Telegram orqali PDF ni jo‘natish
                await botClient.SendDocumentAsync(chatId, file, caption: "✅ Sizning ma'lumotlaringiz PDF shaklida.");
            }
        }
        catch (Exception ex)
        {
            await botClient.SendTextMessageAsync(chatId, $"❌ Xatolik yuz berdi: {ex.Message}");
        }
    }

    internal static string GenerateUserInfoPdf(CandidateInfoEntities userResponse)
    {
        throw new NotImplementedException();
    }
}
