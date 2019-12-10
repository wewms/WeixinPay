using System;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using WeixinPay.Logging;
using WeixinPay.Models;

namespace WeixinPay.Services
{
    public partial class WeixinService : IWeixinService
    {
        private static readonly Regex _sessionKeyPattern = new Regex("\"session_key\":\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex _openIdPattern = new Regex("\"openid\":\"([^\"]+)\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly IDistributedCache _cache;

        private readonly HttpClient _client;
        private readonly AppLogger _logger;
        private readonly WxOptions _wxOptions;

        public WeixinService(AppLogger logger, HttpClient client, IOptions<WxOptions> wxOptions, IDistributedCache cache)
        {
            _logger = logger;
            _client = client;
            _cache = cache;
            _wxOptions = wxOptions.Value;
        }

        public async Task<(string SessionKey, string OpenId)> CodeToSession(string code)
        {
            try
            {
                // https://api.weixin.qq.com/sns/jscode2session?appid=APPID&secret=SECRET&js_code=JSCODE&grant_type=authorization_code
                // {"session_key":"aNpIMqbx2dh4s4zikIBKGg==","expires_in":7200,"openid":"o21z-0E5BiJ6QE1QPlo-cyU-TJ78"}
                var html = await _client.GetStringAsync($"https://api.weixin.qq.com/sns/jscode2session?appid={_wxOptions.WxAppId}&secret={_wxOptions.WxSecretKey}&js_code={code}&grant_type=authorization_code");
                var sessionKey = _sessionKeyPattern.Match(html).Groups[1].Value; // // The input is not a valid Base-64 string as it contains a non-base 64 character, sometimes wexin session key returned contains some illegal base 64 character, fixed in sql.
                var openId = _openIdPattern.Match(html).Groups[1].Value;

                if(string.IsNullOrEmpty(openId) || string.IsNullOrEmpty(sessionKey))
                {
                    _logger.EnqueueMessage($"{_wxOptions.WxAppId} {_wxOptions.WxSecretKey} {nameof(CodeToSession)}: {html}");
                }

                // Weixin bug fix, founded 2019.5.11
                // The input is not a valid Base-64 string as it contains a non-base 64 character
                // fixed in sql
                //if(sessionKey.IndexOf("\\", StringComparison.Ordinal) != -1) sessionKey = sessionKey.Replace("\\", "");

                return (sessionKey, openId);
            }
            catch(Exception ex)
            {
                _logger.EnqueueMessage($"{nameof(global::WeixinPay.Services.WeixinService)}.{nameof(CodeToSession)} error. message: {ex.Message} stackTrace: {ex.StackTrace}");
            }

            // If error, just return empty
            return (string.Empty, string.Empty);
        }
    }
}