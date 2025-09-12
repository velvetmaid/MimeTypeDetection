using System;
using System.IO;

class Program
{
    static void Main()
    {
        var service = new MimeTypeDetection.MimeTypeService();

        Console.WriteLine("=== MIME Detection Test ===");

        // Tes 1: (.pdf)
        var result1 = service.GetFileType("file.pdf");
        PrintResult("file.pdf", result1);

        // Tes 2: JPEG header with .exe extension
        var jpegHeader = new byte[] { 0xFF, 0xD8, 0xFF };
        var result2 = service.GetFileType("renamed.exe", jpegHeader);
        PrintResult("renamed.exe (JPEG header)", result2);

        // Tes 3: ZIP header with .docx extension
        var zipHeader = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var result3 = service.GetFileType("resume.docx", zipHeader);
        PrintResult("resume.docx (ZIP header)", result3);

        // Tes 4: ZIP header with .zip extension
        var result4 = service.GetFileType("archive.zip", zipHeader);
        PrintResult("archive.zip (ZIP header)", result4);

        // Tes 5: Unknown file type
        var result5 = service.GetFileType("unknown.xyz");
        PrintResult("unknown.xyz", result5);

        // Tes 6: Uppercase extension .DOCX
        var result6 = service.GetFileType("RESUME.DOCX", zipHeader);
        PrintResult("RESUME.DOCX", result6);

        // Tes 7: ZIP header with no extension
        var result7 = service.GetFileType("noext", zipHeader);
        PrintResult("noext (ZIP header)", result7);

        Console.WriteLine("=== Done ===");

        Console.WriteLine("=== MIME Detection Test dengan file asli ===");

        TestFile(service, @"D:\File Samples\sample1 PDF.pdf");
        TestFile(service, @"D:\File Samples\sample_640×426 GIF.gif");
        TestFile(service, @"D:\File Samples\sample1 MP3.mp3");
        TestFile(service, @"D:\File Samples\zip_2MB ZIP.zip");
        TestFile(service, @"D:\File Samples\sample2 DOC.doc");
        TestFile(service, @"D:\File Samples\sample2 DOCX.docx");
        TestFile(service, @"D:\File Samples\sample1 XLS.xls");
        TestFile(service, @"D:\File Samples\sample2 XLSX.xlsx");
        TestFile(service, @"D:\File Samples\sample2 CSV.csv");
        TestFile(service, @"D:\File Samples\sample2 PPT.ppt");
        TestFile(service, @"D:\File Samples\sample2 RB.rb");
        TestFile(service, @"D:\File Samples\sample3 TXT.txt");

        Console.WriteLine("=== Done ===");
    }

    static void TestFile(MimeTypeDetection.MimeTypeService service, string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"[SKIP] {path} (File Not Found!)");
            return;
        }

        byte[] header = File.ReadAllBytes(path);
        var result = service.GetFileType(path, header);
        PrintResult(Path.GetFileName(path), result);
    }

    static void PrintResult(string label, MimeTypeDetection.FileTypeRecord result)
    {
        Console.WriteLine($"{label} → {result.Extension} → {result.MimeType}" +
            (string.IsNullOrEmpty(result.ErrorMessage) ? "" : $" [ERROR: {result.ErrorMessage}]"));
    }
}
