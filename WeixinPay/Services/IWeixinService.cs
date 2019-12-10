using System.Threading.Tasks;

namespace WeixinPay.Services
{
    public interface IWeixinService
    {
        Task<(string SessionKey, string OpenId)> CodeToSession(string code);

        Task<bool> BuildCustQRCode(string fileName, string encryptedData, string nickname);
        Task<bool> BuildOrderQRCode(string fileName, string encryptedData, string nickname);
        Task<bool> BuildQRCode(string qrCodeFileDir, string qrCodeFileName, string encryptedData, string nickname, string page);

        Task<string> GetAccessToken();
    }
}