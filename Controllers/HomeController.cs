using IronOcr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace OCRFile.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        [Route("GetString")]
        public IActionResult GetString()
        {
            return Ok("Hello from the OCR API!");
        }

        [HttpGet("pdf")]
        public IActionResult ReadPdf([FromQuery] string fileName, string start, string end)
        {
            // Đường dẫn tới file PDF trong thư mục "Uploads"
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Uploads", fileName);

            if (!System.IO.File.Exists(filePath))
                return NotFound("Không tìm thấy file PDF!");

            var ocr = new IronTesseract
            {
                Language = OcrLanguage.Vietnamese // bảo đảm dùng ngôn ngữ tiếng Việt
            };

            using var input = new OcrInput();
            try
            {
                input.LoadPdf(filePath);
                input.DeNoise(); // khử nhiễu hình ảnh
                input.Deskew();  // làm thẳng ảnh scan bị nghiêng

                var result = ocr.Read(input);

                if (string.IsNullOrWhiteSpace(result.Text))
                {
                    return Ok(new
                    {
                        file = fileName,
                        message = "Không trích xuất được văn bản — có thể file scan mờ hoặc font lạ.",
                        pages = result.Pages.Count
                    });
                }

                return Ok(new
                {
                    file = fileName,
                    Text = ExtractBetweenWithRegex(result.Text, start, end),
                    pages = result.Pages.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Đọc PDF thất bại: {ex.Message}");
            }
        }
        private string ExtractBetweenWithRegex(string fullText, string start, string end)
        {
            // Regex sẽ tìm đoạn nằm giữa start và end, không bao gồm chính start/end
            // Làm sạch dấu xuống dòng để regex không bị đứt đoạn
            if (string.IsNullOrEmpty(fullText)) return $"Không tìm thấy nội dung File văn bản";

            var cleanedText = fullText.Replace("\r", "").Replace("\n", " ");

            // Regex không phân biệt hoa thường, bắt đoạn giữa start & end
            var pattern = $"{Regex.Escape(start)}(.*?){Regex.Escape(end)}";
            var match = Regex.Match(cleanedText, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
            return $"Không tìm thấy nội dung giữa \"{start}\" và \"{end}\"";
        }


    }
}
