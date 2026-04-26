using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using Clicky.Windows.Models;
using Clicky.Windows.Services;

namespace Clicky.Windows.ScreenCapture;

public sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    public Task<IReadOnlyList<CapturedDisplayImage>> CaptureAllDisplaysAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() =>
        {
            Point cursorPosition = Cursor.Position;
            Screen[] allScreens = Screen.AllScreens;
            List<Screen> sortedScreens = allScreens
                .OrderByDescending(screen => screen.Bounds.Contains(cursorPosition))
                .ThenBy(screen => screen.Bounds.Left)
                .ThenBy(screen => screen.Bounds.Top)
                .ToList();

            List<CapturedDisplayImage> capturedDisplayImages = [];
            for (int displayIndex = 0; displayIndex < sortedScreens.Count; displayIndex++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                Screen screen = sortedScreens[displayIndex];
                Rectangle screenBounds = screen.Bounds;
                bool isCursorScreen = screenBounds.Contains(cursorPosition);

                using var bitmap = new Bitmap(screenBounds.Width, screenBounds.Height, PixelFormat.Format24bppRgb);
                using (Graphics graphics = Graphics.FromImage(bitmap))
                {
                    graphics.CopyFromScreen(screenBounds.Left, screenBounds.Top, 0, 0, screenBounds.Size, CopyPixelOperation.SourceCopy);
                }

                using var memoryStream = new MemoryStream();
                ImageCodecInfo jpegCodec = ImageCodecInfo.GetImageEncoders().First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);
                using var encoderParameters = new EncoderParameters(1);
                encoderParameters.Param[0] = new EncoderParameter(Encoder.Quality, 82L);
                bitmap.Save(memoryStream, jpegCodec, encoderParameters);

                int screenNumber = displayIndex + 1;
                string label = BuildScreenLabel(sortedScreens.Count, screenNumber, isCursorScreen);
                capturedDisplayImages.Add(new CapturedDisplayImage(
                    ImageBytes: memoryStream.ToArray(),
                    Label: label,
                    ScreenNumber: screenNumber,
                    IsCursorScreen: isCursorScreen,
                    PixelBounds: screenBounds,
                    ScreenshotWidthInPixels: bitmap.Width,
                    ScreenshotHeightInPixels: bitmap.Height));
            }

            return (IReadOnlyList<CapturedDisplayImage>)capturedDisplayImages;
        }, cancellationToken);
    }

    private static string BuildScreenLabel(int screenCount, int screenNumber, bool isCursorScreen)
    {
        if (screenCount == 1)
        {
            return "user's screen (cursor is here)";
        }

        return isCursorScreen
            ? $"screen {screenNumber} of {screenCount} - cursor is on this screen (primary focus)"
            : $"screen {screenNumber} of {screenCount} - secondary screen";
    }
}
