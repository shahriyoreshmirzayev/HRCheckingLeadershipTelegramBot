using CheckingLeadershipTelegramBot.Entities;
using GSF.IO;
using iText.Layout;
using Microsoft.AspNetCore.Components.Forms;
using SkiaSharp;
using System.Xml.Linq;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace CheckingLeadershipTelegramBot.Services;

public class ImagineGenerator
{
    private static Telegram.Bot.Types.InputFile file;

    public static async Task SendUserProgressImageAsync(ITelegramBotClient botClient, long chatId, int percentage)
    {
        try
        {
            string filePath = "progress.png";
            DrawProgressCircle(percentage, filePath);

            /*await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            InputFile file = InputFile.FromStream(stream, "progress.png");*/

            await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await botClient.SendPhotoAsync(chatId, new InputFileStream(stream, "progress.png"), caption: $"📊 Sizning test natijangiz: {percentage}%");


            await botClient.SendPhotoAsync(chatId, file, caption: $"📊 Sizning test natijangiz: {percentage}%");
            //await botClient.SendPhotoAsync(chatId, InputFile.FromFile(filePath), caption: $"📊 Sizning test natijangiz: {percentage}%");

        }
        catch (Exception ex)
        {
            await botClient.SendTextMessageAsync(chatId, $"❌ Xatolik yuz berdi: {ex.Message}");
        }
    }


    private static void DrawProgressCircle(int percentage, string filePath)
    {
        int width = 250, height = 250;
        using SKBitmap bitmap = new SKBitmap(width, height);
        using SKCanvas canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Black);

        SKPaint bgPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.Gray,
            StrokeWidth = 20,
            IsAntialias = true
        };

        SKPaint progressPaint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            Color = SKColors.LightGreen,
            StrokeWidth = 20,
            IsAntialias = true
        };

        SKPaint textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 40,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };

        SKPoint center = new SKPoint(width / 2, height / 2);
        float radius = width / 3;
        float startAngle = -90;
        float sweepAngle = 360 * (percentage / 100f);

        // Orqa fon doirasi
        canvas.DrawCircle(center, radius, bgPaint);

        // Progress doirasi
        using (SKPath path = new SKPath())
        {
            path.AddArc(new SKRect(center.X - radius, center.Y - radius, center.X + radius, center.Y + radius),
                        startAngle, sweepAngle);
            canvas.DrawPath(path, progressPaint);
        }

        // Markaziy foiz yozuvi
        canvas.DrawText($"{percentage}%", center.X, center.Y + 10, textPaint);

        // PNG faylga saqlash
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream fs = System.IO.File.OpenWrite(filePath);
        data.SaveTo(fs);
    }

    public static string GenerateUserInfoImage(CandidateInfoEntities userResponse, int percentage)
    {
        string filePath = "UserInfo.png";
        int width = 400, height = 400;

        using (SKBitmap bitmap = new SKBitmap(width, height))
        using (SKCanvas canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.White);

            SKPaint textPaint = new SKPaint
            {
                Color = SKColors.Black,
                TextSize = 24,
                IsAntialias = true
            };

            SKPaint progressPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                Color = SKColors.Green,
                StrokeWidth = 20,
                IsAntialias = true
            };

            // Foydalanuvchi ma’lumotlari
            canvas.DrawText($"👤 {userResponse.FamilyName} {userResponse.FirstName}", 20, 40, textPaint);
            canvas.DrawText($"📅 {userResponse.BirthDate}", 20, 80, textPaint);
            canvas.DrawText($"📞 {userResponse.PhoneNumber}", 20, 120, textPaint);
            canvas.DrawText($"💼 {userResponse.Position}", 20, 160, textPaint);

            // Progress doirasi
            float centerX = width / 2;
            float centerY = 300;
            float radius = 80;
            float startAngle = -90;
            float sweepAngle = 360 * (percentage / 100f);

            using (SKPath path = new SKPath())
            {
                path.AddArc(new SKRect(centerX - radius, centerY - radius, centerX + radius, centerY + radius),
                            startAngle, sweepAngle);
                canvas.DrawPath(path, progressPaint);
            }

            // PNG formatida saqlash
            using (SKImage image = SKImage.FromBitmap(bitmap))
            using (SKData data = image.Encode(SKEncodedImageFormat.Png, 100))
            using (FileStream fs = System.IO.File.OpenWrite(filePath))
            {
                data.SaveTo(fs);
            }
        }

        return filePath;
    }



}
