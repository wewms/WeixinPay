using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace WeixinPay.Services
{
    public partial class WeixinService
    {
        public async Task<bool> BuildCustQRCode(string fileName, string encryptedData, string nickname)
        {
            var qrCodeFileDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "qrcode");
            return await BuildQRCode(qrCodeFileDir, fileName, encryptedData, nickname, "pages/customer/bind");
        }

        public async Task<bool> BuildOrderQRCode(string fileName, string encryptedData, string nickname = "提货码")
        {
            var qrCodeFileDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wwwroot", "images", "qrcode", "order");
            return await BuildQRCode(qrCodeFileDir, fileName, encryptedData, nickname, "pages/branch/scan");
        }

        public async Task<bool> BuildQRCode(string qrCodeFileDir, string qrCodeFileName, string encryptedData, string nickname, string page)
        {
            var fileName = Path.Combine(qrCodeFileDir, $"{qrCodeFileName}.jpg");
            if(File.Exists(fileName)) return true;

            var accessToken = await GetAccessToken();
            if(string.IsNullOrEmpty(accessToken)) return false;

            try
            {
                var content = new StringContent($"{{\"scene\":\"{encryptedData}\",\"page\":\"{page}\"}}");
                var res = await _client.PostAsync($"https://api.weixin.qq.com/wxa/getwxacodeunlimit?access_token={accessToken}", content);
                if(!res.Content.Headers.ContentType.MediaType.StartsWith("image", StringComparison.Ordinal))
                {
                    var ret = res.Content.ReadAsStringAsync().Result;
                    _logger.EnqueueMessage($"{nameof(global::WeixinPay.Services.WeixinService)}.{nameof(BuildQRCode)} error, details: {ret}");
                    return false;
                }

                if(!Directory.Exists(qrCodeFileDir)) Directory.CreateDirectory(qrCodeFileDir);

                var stream = await res.Content.ReadAsStreamAsync();
                var image = Image.FromStream(stream);

                if(!string.IsNullOrEmpty(nickname))
                {
                    using Graphics graphics = Graphics.FromImage(image);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawString(nickname, new Font(FontFamily.GenericSansSerif, 10, FontStyle.Regular), new SolidBrush(Color.DarkOrange), 5, 5);
                }

                image.Save(fileName, ImageFormat.Jpeg);
                return true;
            }
            catch(Exception ex)
            {
                _logger.EnqueueMessage($"{nameof(global::WeixinPay.Services.WeixinService)}.{nameof(BuildQRCode)} error, details: {ex.Message} {ex.StackTrace}");
            }

            return false;
        }
    }
}