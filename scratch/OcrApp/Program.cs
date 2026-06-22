using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            string imgPath = @"c:\Users\Yunxi\Desktop\MediaMusic\img_enhanced.png";
            if (!File.Exists(imgPath))
            {
                Console.WriteLine("Image path not found: " + imgPath);
                return;
            }

            var file = await StorageFile.GetFileFromPathAsync(imgPath);
            using (var stream = await file.OpenAsync(FileAccessMode.Read))
            {
                var decoder = await BitmapDecoder.CreateAsync(stream);
                var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                // Test English first
                Console.WriteLine("\n--- OCR English Engine ---");
                var enLang = new Windows.Globalization.Language("en-US");
                if (OcrEngine.IsLanguageSupported(enLang))
                {
                    var engineEn = OcrEngine.TryCreateFromLanguage(enLang);
                    if (engineEn != null)
                    {
                        var result = await engineEn.RecognizeAsync(softwareBitmap);
                        foreach (var line in result.Lines)
                        {
                            Console.WriteLine($"[{line.Text}]");
                        }
                    }
                }

                // Test Chinese
                Console.WriteLine("\n--- OCR Chinese Engine ---");
                var zhLang = new Windows.Globalization.Language("zh-Hans-CN");
                if (!OcrEngine.IsLanguageSupported(zhLang))
                {
                    zhLang = new Windows.Globalization.Language("zh-CN");
                }
                if (OcrEngine.IsLanguageSupported(zhLang))
                {
                    var engineZh = OcrEngine.TryCreateFromLanguage(zhLang);
                    if (engineZh != null)
                    {
                        var result = await engineZh.RecognizeAsync(softwareBitmap);
                        foreach (var line in result.Lines)
                        {
                            Console.WriteLine($"[{line.Text}]");
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error: " + ex);
        }
    }
}
