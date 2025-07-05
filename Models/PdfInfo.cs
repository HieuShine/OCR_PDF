namespace OCRFile.Models
{
    public class PdfFileInfo
    {
        public Guid Id => new Guid();
        public int IdLo { get; set; }
        public string? TenFileGocNhanDuoc { get; set; }
        public string? MaFileGocNhanDuoc { get; set; }
        public int SoTrang { get; set; }
        public string? DinhDangFileGoc { get; set; }
        public string? DuongDanFile { get; set; }
        public string? TenSauKhiChuanHoa { get; set; }
        public long DungLuongFile { get; set; } // bytes
        public bool IsNhanDang { get; set; }
        public bool IsKySo { get; set; }
        public string? Note { get; set; }
    }

}
