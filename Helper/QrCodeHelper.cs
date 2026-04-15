namespace DynamicFormBuilder.Helper
{
    using QRCoder;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;

    public static class QrCodeHelper
    {
        public static byte[] GenerateQrCode(string text, int pixelsPerModule = 10)
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);

            return qrCode.GetGraphic(pixelsPerModule);
        }
    }
}
