# Load WinRT classes
[Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics.Imaging, ContentType = WindowsRuntime] | Out-Null
[Windows.Media.Ocr.OcrEngine, Windows.Media.Ocr, ContentType = WindowsRuntime] | Out-Null
[Windows.Storage.StorageFile, Windows.Storage, ContentType = WindowsRuntime] | Out-Null

$imgPath = "C:\Users\Yunxi\.gemini\antigravity\brain\614ac6a0-4d35-4b4f-b103-73c24ab6d7e0\media__1782124175549.png"
$file = [Windows.Storage.StorageFile]::GetFileFromPathAsync($imgPath).GetAwaiter().GetResult()
$stream = $file.OpenAsync([Windows.Storage.FileAccessMode]::Read).GetAwaiter().GetResult()
$decoder = [Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream).GetAwaiter().GetResult()
$softwareBitmap = $decoder.GetSoftwareBitmapAsync().GetAwaiter().GetResult()

$engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
if ($engine -eq $null) {
    $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromLanguage([Windows.Globalization.Language]::new("zh-CN"))
}

$result = $engine.RecognizeAsync($softwareBitmap).GetAwaiter().GetResult()

Write-Host "--- OCR Result ---"
$result.Lines | ForEach-Object { Write-Host $_.Text }
Write-Host "------------------"
