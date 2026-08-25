using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using AssetManagement.Web.Models;

namespace AssetManagement.Web.Services
{
    public class RealisticCaptchaService
    {
        private static readonly string[] Fonts = { "Arial", "Verdana", "Tahoma", "Georgia" };
        private static readonly Color[] Colors =
        {
            Color.FromArgb(50, 50, 50),
            Color.FromArgb(100, 50, 150),
            Color.FromArgb(150, 50, 100),
            Color.FromArgb(50, 100, 150)
        };

        private static readonly Color[] BackgroundColors =
        {
            Color.FromArgb(240, 248, 255),
            Color.FromArgb(255, 250, 240),
            Color.FromArgb(250, 250, 250),
            Color.FromArgb(248, 248, 255)
        };

        public CaptchaResponse GenerateCaptcha()
        {
            const int width = 200;
            const int height = 80;
            var text = GenerateRandomText(6);

            using (var bitmap = new Bitmap(width, height))
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.TextRenderingHint = TextRenderingHint.AntiAlias;

                var bgColor = BackgroundColors[SecureNext(BackgroundColors.Length)];
                using (var brush = new SolidBrush(bgColor))
                {
                    graphics.FillRectangle(brush, 0, 0, width, height);
                }

                AddNoise(graphics, width, height);
                AddInterferenceLines(graphics, width, height);
                DrawText(graphics, text, width, height);

                using (var ms = new MemoryStream())
                {
                    bitmap.Save(ms, ImageFormat.Png);
                    return new CaptchaResponse
                    {
                        CaptchaId = Guid.NewGuid().ToString(),
                        CaptchaText = text,
                        CaptchaBase64 = Convert.ToBase64String(ms.ToArray()),
                        ExpiresAt = DateTime.UtcNow.AddMinutes(10)
                    };
                }
            }
        }

        private static string GenerateRandomText(int length)
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz23456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[SecureNext(s.Length)]).ToArray());
        }

        private static void AddNoise(Graphics graphics, int width, int height)
        {
            for (var i = 0; i < 100; i++)
            {
                var x = SecureNext(width);
                var y = SecureNext(height);
                var color = Color.FromArgb(SecureNext(100, 200), SecureNext(100, 200), SecureNext(100, 200));
                using (var brush = new SolidBrush(color))
                {
                    graphics.FillEllipse(brush, x, y, 1, 1);
                }
            }
        }

        private static void AddInterferenceLines(Graphics graphics, int width, int height)
        {
            for (var i = 0; i < 5; i++)
            {
                var x1 = SecureNext(width);
                var y1 = SecureNext(height);
                var x2 = SecureNext(width);
                var y2 = SecureNext(height);
                var color = Color.FromArgb(SecureNext(50, 150), SecureNext(50, 150), SecureNext(50, 150));
                using (var pen = new Pen(color, 1))
                {
                    graphics.DrawLine(pen, x1, y1, x2, y2);
                }
            }
        }

        private static void DrawText(Graphics graphics, string text, int width, int height)
        {
            const int fontSize = 24;
            using (var font = new Font(Fonts[SecureNext(Fonts.Length)], fontSize, FontStyle.Bold))
            using (var brush = new SolidBrush(Colors[SecureNext(Colors.Length)]))
            {
                var textSize = graphics.MeasureString(text, font);
                var x = (width - textSize.Width) / 2;
                var y = (height - textSize.Height) / 2;

                graphics.TranslateTransform(x + textSize.Width / 2, y + textSize.Height / 2);
                graphics.RotateTransform(SecureNext(-10, 11));
                graphics.TranslateTransform(-(x + textSize.Width / 2), -(y + textSize.Height / 2));
                graphics.DrawString(text, font, brush, x, y);
                graphics.ResetTransform();
            }
        }

        private static int SecureNext(int maxExclusive)
        {
            if (maxExclusive <= 0)
            {
                return 0;
            }

            using (var rng = new RNGCryptoServiceProvider())
            {
                var bytes = new byte[4];
                rng.GetBytes(bytes);
                return (int)(BitConverter.ToUInt32(bytes, 0) % (uint)maxExclusive);
            }
        }

        private static int SecureNext(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive)
            {
                return minInclusive;
            }

            return minInclusive + SecureNext(maxExclusive - minInclusive);
        }
    }
}
