using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using Tesseract;

namespace OCRFile.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TesseractController : ControllerBase
    {
        [HttpGet("Tesseract")]
        public IActionResult ReadPdfWithTesseract(
    [FromQuery] string fileName,
    int start = 1,
    int end = int.MaxValue,
    [FromQuery] string startMarker = null,
    [FromQuery] string endMarker = null)
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound("Không tìm thấy file PDF!");

            try
            {
                var outputFolder = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
                Directory.CreateDirectory(outputFolder);

                // Gợi ý: Dùng tool bên ngoài như ImageMagick để convert PDF sang ảnh PNG từng trang
                // magick -density 300 input.pdf -quality 100 outputFolder/page.png

                var imageFiles = Directory.GetFiles(outputFolder, "*.png")
                                          .OrderBy(f => f).ToList();

                var tessDataPath = Path.Combine(Directory.GetCurrentDirectory(), "Tessdata");
                using var engine = new TesseractEngine(tessDataPath, "vie", EngineMode.Default);

                var sb = new StringBuilder();
                for (int i = start - 1; i < Math.Min(end, imageFiles.Count); i++)
                {
                    using var img = Pix.LoadFromFile(imageFiles[i]);
                    using var page = engine.Process(img);
                    var text = page.GetText();
                    sb.AppendLine($"--- Trang {i + 1} ---\n{text}");
                }

                var fullText = sb.ToString();
                string resultText = fullText;

                //if (!string.IsNullOrEmpty(startMarker) && !string.IsNullOrEmpty(endMarker))
                //{
                //    int indexStart = fullText.IndexOf(startMarker);
                //    if (indexStart != -1)
                //    {
                //        int indexAfterStart = indexStart + startMarker.Length;
                //        int indexEnd = fullText.IndexOf(endMarker, indexAfterStart);

                //        if (indexEnd != -1 && indexEnd > indexAfterStart)
                //        {
                //            resultText = fullText.Substring(indexAfterStart, indexEnd - indexAfterStart);
                //        }
                //        else
                //        {
                //            resultText = "Không tìm thấy `endMarker` sau `startMarker`.";
                //        }
                //    }
                //    else
                //    {
                //        resultText = "Không tìm thấy `startMarker`.";
                //    }
                //}


                return Ok(new
                {
                    file = fileName,
                    text = resultText
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"OCR lỗi: {ex.Message}");
            }
        }


    }
}
