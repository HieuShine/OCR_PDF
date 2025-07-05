1. Install-Package IronOcr
2. Install-Package IronOcr.Languages.Vietnamese
3. 
            var ocr = new IronTesseract
            {
                Language = OcrLanguage.Vietnamese // bảo đảm dùng ngôn ngữ tiếng Việt
            };

            using var input = new OcrInput();
           input.LoadPdf(filePath);
           input.DeNoise(); // khử nhiễu hình ảnh
           input.Deskew();  // làm thẳng ảnh scan bị nghiêng

           var result = ocr.Read(input)
   //1  vài tham số thường dùng OCR
   /n
           result.Text //Fulltext
   /n
           result.Pages //Danh sách từng trang PDF
   /n
           result.Languages // Danh sách ngôn ngữ OCR nhận dạng
   /n
           result.Barcodes // nếu có mã vạch  (List<OcrResult.Barcode>)
/n
   //Yêu cầu thư viện SixLabors.ImageSharp >= 3.1.8
            
