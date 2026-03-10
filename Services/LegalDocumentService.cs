namespace DynamicFormBuilder.Services
{
    using DynamicFormBuilder.Models;
    using System.Security.Cryptography;
    using System.Text;

    public interface ILegalDocumentService
    {
        LegalDocumentInfo GetCurrentTerms();
        LegalDocumentInfo GetCurrentPrivacy();
    }

    public class LegalDocumentService : ILegalDocumentService
    {
        private readonly IWebHostEnvironment _env;

        public LegalDocumentService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public LegalDocumentInfo GetCurrentTerms()
        {
            var filePath = Path.Combine(_env.ContentRootPath, "legal", "terms_v1.html");
            return Build("terms", "v1", filePath);
        }

        public LegalDocumentInfo GetCurrentPrivacy()
        {
            var filePath = Path.Combine(_env.ContentRootPath, "legal", "privacy_v1.html");
            return Build("privacy", "v1", filePath);
        }

        private static LegalDocumentInfo Build(string type, string version, string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Legal file not found: {filePath}");

            var html = File.ReadAllText(filePath);
            var hash = ComputeSha256(html);

            return new LegalDocumentInfo
            {
                Type = type,
                Version = version,
                FilePath = filePath,
                Hash = hash
            };
        }

        private static string ComputeSha256(string content)
        {
            using var sha = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(content);
            var hashBytes = sha.ComputeHash(bytes);
            return Convert.ToHexString(hashBytes);
        }
    }
}
