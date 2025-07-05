using IronOcr;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OCRFile.Models;
using System.Globalization;
using System.Text;
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
                    Text = ExtractBetweenWithRegex_Fuzzy(result.Text, start, end),
                    pages = result.Pages.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Đọc PDF thất bại: {ex.Message}");
            }
        }
        //V1
        //private string ExtractBetweenWithRegex(string fullText, string start, string end)
        //{
        //    // Regex sẽ tìm đoạn nằm giữa start và end, không bao gồm chính start/end
        //    // Làm sạch dấu xuống dòng để regex không bị đứt đoạn
        //    if (string.IsNullOrEmpty(fullText)) return $"Không tìm thấy nội dung File văn bản";

        //    var cleanedText = fullText.Replace("\r", "").Replace("\n", " ");

        //    // Regex không phân biệt hoa thường, bắt đoạn giữa start & end
        //    var pattern = $"{Regex.Escape(start)}(.*?){Regex.Escape(end)}";
        //    var match = Regex.Match(cleanedText, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        //    if (match.Success)
        //    {
        //        return match.Groups[1].Value.Trim();
        //    }
        //    return $"Không tìm thấy nội dung giữa \"{start}\" và \"{end}\"";
        //}
        private string ExtractBetweenWithRegex_Fuzzy(string fullText, string start, string end)
        {
            if (string.IsNullOrEmpty(fullText))
                return $"Không tìm thấy nội dung File văn bản";

            // Làm sạch đoạn văn bản đầu vào
            var cleanedText = fullText.Replace("\r", "").Replace("\n", " ");

            // Chuyển thành không dấu và lowercase để so sánh dễ hơn
            string RemoveDiacritics(string input) =>
                string.Concat(input.Normalize(NormalizationForm.FormD)
                    .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
                    .ToLower();

            var normalizedText = RemoveDiacritics(cleanedText);
            var normalizedStart = RemoveDiacritics(start);
            var normalizedEnd = RemoveDiacritics(end);

            // Cho phép khớp mờ các khoảng trắng: thay ' ' bằng '\s*' trong regex
            string BuildLoosePattern(string raw) =>
                Regex.Escape(raw).Replace("\\ ", "\\s*"); // ví dụ: "dieu 1" -> "dieu\\s*1"

            var pattern = $"{BuildLoosePattern(normalizedStart)}(.*?){BuildLoosePattern(normalizedEnd)}";

            var match = Regex.Match(normalizedText, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

            if (match.Success)
            {
                // Lấy lại phần text gốc theo vị trí match
                int startIndex = normalizedText.IndexOf(match.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
                if (startIndex >= 0)
                {
                    return cleanedText.Substring(startIndex, match.Groups[1].Value.Length).Trim();
                }
                return match.Groups[1].Value.Trim(); // fallback
            }

            return $"Không tìm thấy nội dung giữa \"{start}\" và \"{end}\"";
        }

        #region V2

        private static readonly RegexOptions RegexOpts = RegexOptions.Singleline | RegexOptions.IgnoreCase | RegexOptions.Compiled;

        private string ExtractFuzzyBetween(string fullText, string start, string end)
        {
            if (string.IsNullOrWhiteSpace(fullText)) return "Không có văn bản đầu vào.";
            if (string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end)) return "Thiếu từ khóa.";

            // Làm sạch văn bản nhưng vẫn giữ khoảng trắng để tìm lại chính xác
            ReadOnlySpan<char> fullSpan = fullText.AsSpan();
            var cleanedText = fullText.Replace("\r", "").Replace("\n", " ");
            var raw = StripForOcr(cleanedText);
            var startKey = StripForOcr(start);
            var endKey = StripForOcr(end);

            int startIdx = raw.IndexOf(startKey, StringComparison.Ordinal);
            if (startIdx < 0) return $"Không tìm thấy từ bắt đầu: \"{start}\".";

            int endIdx = raw.IndexOf(endKey, startIdx + startKey.Length, StringComparison.Ordinal);
            if (endIdx < 0) return $"Không tìm thấy từ kết thúc: \"{end}\" sau \"{start}\".";

            // Ước lượng lại vị trí trong văn bản gốc
            double ratio = (double)cleanedText.Length / raw.Length;
            int approxStart = Math.Max(0, (int)(startIdx * ratio));
            int approxEnd = Math.Min(cleanedText.Length, (int)((endIdx + endKey.Length) * ratio));

            int windowStart = Math.Max(0, approxStart - 50);
            int windowLength = Math.Min(cleanedText.Length - windowStart, approxEnd - windowStart + 100);
            var window = cleanedText.Substring(windowStart, windowLength);

            // Dùng Regex để match lại chính xác
            var pattern = $"{Regex.Escape(start)}(.*?){Regex.Escape(end)}";
            var match = Regex.Match(window, pattern, RegexOpts);

            return match.Success
                ? match.Groups[1].Value.Trim()
                : "(Tìm gần đúng nhưng không khớp được mẫu chính xác)";
        }
        private string StripForOcr(string input)
{
    if (string.IsNullOrEmpty(input)) return "";

    var normalized = input.Normalize(NormalizationForm.FormD);
    var sb = new StringBuilder(normalized.Length);

    foreach (var c in normalized)
    {
        var uc = CharUnicodeInfo.GetUnicodeCategory(c);
        if (uc != UnicodeCategory.NonSpacingMark && !char.IsWhiteSpace(c))
        {
            sb.Append(char.ToLowerInvariant(c));
        }
    }

    return sb.ToString().Normalize(NormalizationForm.FormC);
}

        #endregion

        [HttpGet("pdf-list")]
        public IActionResult GetPdfList()
        {
            var scanner = new PdfScannerService();
            var list = scanner.Scan();
            return Ok(list);
        }


        public class PdfScannerService
        {
             string folderPath = Path.Combine(Directory.GetCurrentDirectory(),"Uploads");

            public List<PdfFileInfo> Scan()
            {
                var files = Directory.GetFiles(folderPath, "*.pdf");
                var list = new List<PdfFileInfo>();
                int id = 1;

                foreach (var path in files)
                {
                    var file = new FileInfo(path);

                    list.Add(new PdfFileInfo
                    {
                        IdLo = id++,
                        TenFileGocNhanDuoc = file.Name,
                        MaFileGocNhanDuoc = Path.GetFileNameWithoutExtension(file.Name),
                        SoTrang = 0,
                        DinhDangFileGoc = file.Extension,
                        DuongDanFile = path,
                        TenSauKhiChuanHoa = file.Name.ToLower().Replace(" ", "_"),
                        DungLuongFile = file.Length,
                        IsNhanDang = false,
                        IsKySo = false,
                        Note = ""
                    });
                }

                return list;
            }
        }

    }
}
