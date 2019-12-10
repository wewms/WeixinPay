using System;
using System.Text;
using System.Threading.Tasks;
using Jil;
using Microsoft.Extensions.Caching.Distributed;
using WeixinPay.Models;

namespace WeixinPay.Services
{
    public partial class WeixinService
    {
        private const string WxAccessTokenKey = "wxaccesstoken";

        public async Task<string> GetAccessToken()
        {
            // from cache
            var accessToken = await _cache.GetStringAsync(WxAccessTokenKey);
            if(!string.IsNullOrEmpty(accessToken)) return accessToken;

            // from weixin
            var json = await _client.GetStringAsync($"https://api.weixin.qq.com/cgi-bin/token?grant_type=client_credential&appid={_wxOptions.WxAppId}&secret={_wxOptions.WxSecretKey}");
            if(json.IndexOf("errcode", StringComparison.OrdinalIgnoreCase) != -1)
            {
                _logger.EnqueueMessage($"{nameof(WeixinService)}.{nameof(GetAccessToken)} error, details: {json}");
                return string.Empty;
            }

            // {"access_token":"18_oHWyV7qNzDLVOtoQ5IMB66ljT4lqbT1zdMcDQRnbsfujWcDOjalUwesvks0Zz5yVPNmw0CzlAtNZ2XgBce99lY_WsSZg2qur8pUaijbBTLcJ1sSWNEB4TFdVstGehWHHRyRLv-HKQ-5XermgYHIcAHAHQR","expires_in":7200}
            var result = JSON.Deserialize<WxAccessToken>(json);
            if(result == null || string.IsNullOrEmpty(result.access_token))
            {
                _logger.EnqueueMessage($"{nameof(WeixinService)}.{nameof(GetAccessToken)} Jil.JSON.Deserialize() error, details: {json}");
                return string.Empty;
            }

            var accessTokenBytes = Encoding.UTF8.GetBytes(result.access_token);
            await _cache.SetAsync(WxAccessTokenKey, accessTokenBytes, new DistributedCacheEntryOptions {AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(result.expires_in)});

            return result.access_token;
        }
    }
}