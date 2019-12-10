using System;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WeixinPay.Extensions;
using WeixinPay.Logging;
using WeixinPay.Models;

namespace WeixinPay
{
    public class WeixinWithdrawRefund
    {
        private static readonly Regex RefundIdPattern = new Regex(@"<refund_id><!\[CDATA\[([^\]]+)\]\]></refund_id>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex RefundStatusPattern = new Regex(@"<refund_status_0><!\[CDATA\[([^\]]+)\]\]></refund_status_0>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly HttpClient _client;

        private readonly AppLogger _logger;
        private readonly WxOptions _options;

        public WeixinWithdrawRefund(HttpClient client, AppLogger logger, IOptions<WxOptions> options)
        {
            _client = client;
            _logger = logger;
            _options = options.Value;

            //var clientHandler = new HttpClientHandler {
            //    AllowAutoRedirect = true,
            //    AutomaticDecompression = DecompressionMethods.GZip,
            //    ClientCertificateOptions = ClientCertificateOption.Manual,
            //    //ServerCertificateCustomValidationCallback = delegate { return true; }
            //};
            //var cert = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _options.WxApiCert);

            //X509Certificate2 x509Certificate2 = new X509Certificate2(cert, _options.WxMchId);
            //clientHandler.ClientCertificates.Add(x509Certificate2);

            //_client = new HttpClient(clientHandler);
        }

        /// <summary>
        ///     Weixin refund
        /// </summary>
        /// <param name="device_info1">设备号 否 终端设备号</param>
        /// <param name="out_refund_no1">
        ///     商户退款单号	out_refund_no	是	String(64)	1217752501201407033233368018
        ///     商户系统内部的退款单号，商户系统内部唯一，只能是数字、大小写字母_-|*@ ，同一退款单号多次请求只退一笔。
        /// </param>
        /// <param name="out_trade_no1">
        ///     商户订单号	out_trade_no	String(32)	1217752501201407033233368018
        ///     商户系统内部订单号，要求32个字符内，只能是数字、大小写字母_-|*@ ，且在同一个商户号下唯一。
        /// </param>
        /// <param name="refund_account1">
        ///     退款资金来源	refund_account	否	String(30)	REFUND_SOURCE_RECHARGE_FUNDS
        ///     仅针对老资金流商户使用
        ///     REFUND_SOURCE_UNSETTLED_FUNDS---未结算资金退款（默认使用未结算资金退款）
        ///     REFUND_SOURCE_RECHARGE_FUNDS---可用余额退款
        /// </param>
        /// <param name="refund_desc1">
        ///     退款原因	refund_desc	否	String(80)	商品已售完
        ///     若商户传入，会在下发给用户的退款消息中体现退款原因
        ///     注意：若订单退款金额≤1元，且属于部分退款，则不会在退款消息中体现退款原因
        /// </param>
        /// <param name="refund_fee1">退款金额	refund_fee	是	Int	100	退款总金额，订单总金额，单位为分，只能为整数，详见支付金额</param>
        /// <param name="refund_fee_type1">货币种类	refund_fee_type	否	String(8)	CNY	货币类型，符合ISO 4217标准的三位字母代码，默认人民币：CNY，其他值列表详见货币类型</param>
        /// <param name="total_fee1">订单金额	total_fee	是	Int	100	订单总金额，单位为分，只能为整数，详见支付金额</param>
        /// <param name="transaction_id1">微信订单号	transaction_id	二选一	String(32)	1217752501201407033233368018	微信生成的订单号，在支付通知中有返回</param>
        /// <returns>微信退款单号</returns>
        public async Task<string> Refund(string out_refund_no1, string out_trade_no1, string total_fee1, string transaction_id1, string refund_fee1, string refund_account1 = "", string refund_desc1 = "", string refund_fee_type1 = "CNY", string device_info1 = "")
        {
            string nonce_str1 = out_refund_no1.GetHashUTF8(); // 随机字符串，不长于32位
            var hash = BuildSignString(_options.WxAppId, device_info1, _options.WxMchId, nonce_str1, _options.WxRefundNotifyUrl, out_refund_no1, out_trade_no1, refund_account1, refund_desc1, refund_fee1, refund_fee_type1, "MD5", total_fee1, transaction_id1);
            var requestXml = BuildRequestXml(_options.WxAppId, device_info1, _options.WxMchId, nonce_str1, _options.WxRefundNotifyUrl, out_refund_no1, out_trade_no1, refund_account1, refund_desc1, refund_fee1, refund_fee_type1, hash, "MD5", total_fee1, transaction_id1);
            if(_options.WxIsDebug) _logger.EnqueueMessage($"WxRefundXml: {requestXml}");

            try
            {
                HttpContent content = new ByteArrayContent(Encoding.UTF8.GetBytes(requestXml));
                var res = await _client.PostAsync("https://api.mch.weixin.qq.com/secapi/pay/refund", content);
                var xml = await res.Content.ReadAsStringAsync();
                if(_options.WxIsDebug) _logger.EnqueueMessage($"WxRefundRes: {xml}");

                // no sign validation note: must be validated before using
                bool isReturnSuccess = xml.IndexOf("<return_code><![CDATA[SUCCESS]]></return_code>", StringComparison.Ordinal) != -1;
                //bool isReturnSuccess = xml.StartsWith("<xml><return_code><![CDATA[SUCCESS]]></return_code>", StringComparison.Ordinal);
                if(!isReturnSuccess) return string.Empty;

                bool isResultSuccess = xml.IndexOf("<result_code><![CDATA[SUCCESS]]></result_code>", StringComparison.Ordinal) != -1;
                return isResultSuccess ? RefundIdPattern.Match(xml).Groups[1].Value : string.Empty /*error*/;
            }
            catch(Exception ex)
            {
                _logger.EnqueueMessage($"{nameof(WeixinPay)}.{nameof(Refund)} error. Message: {ex.Message} InnerMessage: {ex.InnerException?.Message} StackTrace: {ex.StackTrace}");
            }

            return string.Empty;

            string BuildSignString(string appid, string device_info, string mch_id, string nonce_str, string notify_url, string out_refund_no, string out_trade_no, string refund_account, string refund_desc, string refund_fee, string refund_fee_type, string sign_type, string total_fee, string transaction_id)
            {
                StringBuilder signBuilder = new StringBuilder();

                signBuilder.Append($"appid={appid}");

                if(!string.IsNullOrEmpty(device_info)) signBuilder.Append($"&device_info={device_info}");
                if(!string.IsNullOrEmpty(mch_id)) signBuilder.Append($"&mch_id={mch_id}");
                if(!string.IsNullOrEmpty(nonce_str)) signBuilder.Append($"&nonce_str={nonce_str}");
                if(!string.IsNullOrEmpty(notify_url)) signBuilder.Append($"&notify_url={notify_url}");
                if(!string.IsNullOrEmpty(out_refund_no)) signBuilder.Append($"&out_refund_no={out_refund_no}");
                if(!string.IsNullOrEmpty(out_trade_no)) signBuilder.Append($"&out_trade_no={out_trade_no}");
                if(!string.IsNullOrEmpty(refund_account)) signBuilder.Append($"&refund_account={refund_account}");
                if(!string.IsNullOrEmpty(refund_desc)) signBuilder.Append($"&refund_desc={refund_desc}");
                if(!string.IsNullOrEmpty(refund_fee)) signBuilder.Append($"&refund_fee={refund_fee}");
                if(!string.IsNullOrEmpty(refund_fee_type)) signBuilder.Append($"&refund_fee_type={refund_fee_type}");
                if(!string.IsNullOrEmpty(sign_type)) signBuilder.Append($"&sign_type={sign_type}");
                if(!string.IsNullOrEmpty(total_fee)) signBuilder.Append($"&total_fee={total_fee}");
                if(!string.IsNullOrEmpty(transaction_id)) signBuilder.Append($"&transaction_id={transaction_id}");

                signBuilder.Append($"&key={_options.WxMchKey}");
                return signBuilder.ToString().GetHashUTF8().ToUpperInvariant();
            }

            string BuildRequestXml(string appid, string device_info, string mch_id, string nonce_str, string notify_url, string out_refund_no, string out_trade_no, string refund_account, string refund_desc, string refund_fee, string refund_fee_type, string sign, string sign_type, string total_fee, string transaction_id)
            {
                StringBuilder xmlBuilder = new StringBuilder("<xml>");

                xmlBuilder.Append($"<appid>{appid}</appid>");

                if(!string.IsNullOrEmpty(device_info)) xmlBuilder.Append($"<device_info>{device_info}</device_info>");
                if(!string.IsNullOrEmpty(mch_id)) xmlBuilder.Append($"<mch_id>{mch_id}</mch_id>");
                if(!string.IsNullOrEmpty(nonce_str)) xmlBuilder.Append($"<nonce_str>{nonce_str}</nonce_str>");
                if(!string.IsNullOrEmpty(notify_url)) xmlBuilder.Append($"<notify_url>{notify_url}</notify_url>");
                if(!string.IsNullOrEmpty(out_refund_no)) xmlBuilder.Append($"<out_refund_no>{out_refund_no}</out_refund_no>");
                if(!string.IsNullOrEmpty(out_trade_no)) xmlBuilder.Append($"<out_trade_no>{out_trade_no}</out_trade_no>");
                if(!string.IsNullOrEmpty(refund_account)) xmlBuilder.Append($"<refund_account>{refund_account}</refund_account>");
                if(!string.IsNullOrEmpty(refund_desc)) xmlBuilder.Append($"<refund_desc>{refund_desc}</refund_desc>");
                if(!string.IsNullOrEmpty(refund_fee)) xmlBuilder.Append($"<refund_fee>{refund_fee}</refund_fee>");
                if(!string.IsNullOrEmpty(refund_fee_type)) xmlBuilder.Append($"<refund_fee_type>{refund_fee_type}</refund_fee_type>");
                if(!string.IsNullOrEmpty(sign)) xmlBuilder.Append($"<sign>{sign}</sign>");
                if(!string.IsNullOrEmpty(sign_type)) xmlBuilder.Append($"<sign_type>{sign_type}</sign_type>");
                if(!string.IsNullOrEmpty(total_fee)) xmlBuilder.Append($"<total_fee>{total_fee}</total_fee>");
                if(!string.IsNullOrEmpty(transaction_id)) xmlBuilder.Append($"<transaction_id>{transaction_id}</transaction_id>");

                return xmlBuilder.Append("</xml>").ToString();
            }
        }

        /// <summary>
        ///     Weixin refund callback
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public (string RefundId, string RefundRecvAccount, string RefundStatus, int Status) VerifyRefundCallback(string xml)
        {
            if(_options.WxIsDebug) _logger.EnqueueMessage($"WxRefundCallback: {xml}");

            var signBuilder = new StringBuilder();

            var appid = Regex.Match(xml, @"<appid><\!\[CDATA\[([^\]]+)\]\]></appid>").Groups[1].Value;
            if(!string.IsNullOrEmpty(appid)) signBuilder.Append($"appid={appid}");

            var mch_id = Regex.Match(xml, @"<mch_id><\!\[CDATA\[([^\]]+)\]\]></mch_id>").Groups[1].Value;
            if(!string.IsNullOrEmpty(mch_id)) signBuilder.Append($"&mch_id={mch_id}");

            var nonce_str = Regex.Match(xml, @"<nonce_str><\!\[CDATA\[([^\]]+)\]\]></nonce_str>").Groups[1].Value;
            if(!string.IsNullOrEmpty(nonce_str)) signBuilder.Append($"&nonce_str={nonce_str}");

            var req_info = Regex.Match(xml, @"<req_info><\!\[CDATA\[([^\]]+)\]\]></req_info>").Groups[1].Value;
            if(!string.IsNullOrEmpty(req_info)) signBuilder.Append($"&req_info={req_info}");

            var return_code = Regex.Match(xml, @"<return_code><\!\[CDATA\[([^\]]+)\]\]></return_code>").Groups[1].Value;
            if(!string.IsNullOrEmpty(return_code)) signBuilder.Append($"&return_code={return_code}");

            var return_msg = Regex.Match(xml, @"<return_msg><\!\[CDATA\[([^\]]+)\]\]></return_msg>").Groups[1].Value;
            if(!string.IsNullOrEmpty(return_msg)) signBuilder.Append($"&return_msg={return_msg}");

            // Process
            var info = DecryptReqInfo(req_info, _options.WxMchKey);
            return ParseReqInfo(info);

            (string RefundId, string RefundRecvAccount, string RefundStatus, int Status) ParseReqInfo(string xml01)
            {
                if(string.IsNullOrEmpty(xml01)) return default;

                //string out_refund_no = Regex.Match(xml01, @"<out_refund_no><\!\[CDATA\[([^\]]+)\]\]></out_refund_no>").Groups[1].Value;
                //string out_trade_no = Regex.Match(xml01, @"<out_trade_no><\!\[CDATA\[([^\]]+)\]\]></out_trade_no>").Groups[1].Value;
                //string refund_account = Regex.Match(xml01, @"<refund_account><\!\[CDATA\[([^\]]+)\]\]></refund_account>").Groups[1].Value;
                //string refund_fee = Regex.Match(xml01, @"<refund_fee><\!\[CDATA\[([^\]]+)\]\]></refund_fee>").Groups[1].Value;
                string refund_id = Regex.Match(xml01, @"<refund_id><\!\[CDATA\[([^\]]+)\]\]></refund_id>").Groups[1].Value;
                string refund_recv_accout = Regex.Match(xml01, @"<refund_recv_accout><\!\[CDATA\[([^\]]+)\]\]></refund_recv_accout>").Groups[1].Value;
                //string refund_request_source = Regex.Match(xml01, @"<refund_request_source><\!\[CDATA\[([^\]]+)\]\]></refund_request_source>").Groups[1].Value;
                string refund_status = Regex.Match(xml01, @"<refund_status><\!\[CDATA\[([^\]]+)\]\]></refund_status>").Groups[1].Value;
                //string settlement_refund_fee = Regex.Match(xml01, @"<settlement_refund_fee><\!\[CDATA\[([^\]]+)\]\]></settlement_refund_fee>").Groups[1].Value;
                //string settlement_total_fee = Regex.Match(xml01, @"<settlement_total_fee><\!\[CDATA\[([^\]]+)\]\]></settlement_total_fee>").Groups[1].Value;
                //string success_time = Regex.Match(xml01, @"<success_time><\!\[CDATA\[([^\]]+)\]\]></success_time>").Groups[1].Value;
                //string total_fee = Regex.Match(xml01, @"<total_fee><\!\[CDATA\[([^\]]+)\]\]></total_fee>").Groups[1].Value;
                //string transaction_id = Regex.Match(xml01, @"<transaction_id><\!\[CDATA\[([^\]]+)\]\]></transaction_id>").Groups[1].Value;

                var status = refund_status switch {
                    "SUCCESS" => 1,
                    "CHANGE" => 2,
                    "REFUNDCLOSE" => 3,
                    _ => 4 /*unknow*/
                };

                return (refund_id, refund_recv_accout, refund_status, status);
            }

            string DecryptReqInfo(string reqInfo, string mchKey)
            {
                //（1）对加密串A做base64解码，得到加密串B
                //（2）对商户key做md5，得到32位小写key* ( key设置路径：微信商户平台(pay.weixin.qq.com)-->账户设置-->API安全-->密钥设置 )
                //（3）用key*对加密串B做AES-256-ECB解密（PKCS7Padding）
                var key = mchKey.GetHashUTF8().ToLowerInvariant();
                var encryptedDataBytes = Convert.FromBase64String(reqInfo);

                try
                {
                    AesCryptoServiceProvider aes = new AesCryptoServiceProvider {
                        Mode = CipherMode.ECB,
                        BlockSize = 128, // if BlockSize is 256, an error occurred. Specified block size is not valid for this algorithm
                        Padding = PaddingMode.PKCS7,
                        Key = Encoding.UTF8.GetBytes(key)
                    };

                    ICryptoTransform transform = aes.CreateDecryptor();
                    byte[] decryptedBytes = transform.TransformFinalBlock(encryptedDataBytes, 0, encryptedDataBytes.Length);
                    return Encoding.UTF8.GetString(decryptedBytes);
                }
                catch(Exception ex)
                {
                    _logger.EnqueueMessage($"{nameof(WeixinWithdrawRefund)}.{nameof(DecryptReqInfo)} error. Message: {ex.Message} InnerMessage: {ex.InnerException?.Message} StackTrace: {ex.StackTrace}");
                }

                return string.Empty;
            }
        }

        /// <summary>
        ///     Query weixin refund status
        /// </summary>
        /// <param name="wxRefundId">微信退款单号</param>
        /// <returns>1: success -1: communicate error -2: result error -3: exception</returns>
        public async Task<(string Status, int Code)> QueryRefund(string wxRefundId)
        {
            var nonceStr1 = wxRefundId.GetHashUTF8();
            var hash = BuildSignString(_options.WxAppId, _options.WxMchId, _options.WxMchKey, nonceStr1, wxRefundId);
            var requestXml = BuildRequestXml(_options.WxAppId, _options.WxMchId, nonceStr1, wxRefundId, hash);

            try
            {
                HttpContent content = new ByteArrayContent(Encoding.UTF8.GetBytes(requestXml));
                var res = await _client.PostAsync("https://api.mch.weixin.qq.com/pay/refundquery", content);
                var xml = await res.Content.ReadAsStringAsync();

                // no sign validation note: must be validated before using
                bool isReturnSuccess = xml.IndexOf("<return_code><![CDATA[SUCCESS]]></return_code>", StringComparison.Ordinal) != -1;
                //bool isReturnSuccess = xml.StartsWith("<xml><return_code><![CDATA[SUCCESS]]></return_code>", StringComparison.Ordinal);
                if(!isReturnSuccess)
                {
                    _logger.EnqueueMessage(xml);
                    return (string.Empty, -1);
                }

                bool isResultSuccess = xml.IndexOf("<result_code><![CDATA[SUCCESS]]></result_code>", StringComparison.Ordinal) != -1;
                if(isResultSuccess) return (RefundStatusPattern.Match(xml).Groups[1].Value, 1);

                _logger.EnqueueMessage(xml);

                // error
                return (string.Empty, -2);
            }
            catch(Exception ex)
            {
                _logger.EnqueueMessage($"{nameof(WeixinWithdrawRefund)}.{nameof(QueryRefund)} error. details: {ex.Message} stackTrace: {ex.StackTrace}");
            }

            // exception
            return (string.Empty, -3);

            string BuildSignString(string appid1, string mch_id1, string mch_key1, string nonce_str1, string refund_id1, string offset1 = "", string out_refund_no1 = "", string out_trade_no1 = "", string sign_type1 = "MD5", string transaction_id1 = "")
            {
                StringBuilder signBuilder = new StringBuilder();

                signBuilder.Append($"appid={appid1}");

                if(!string.IsNullOrEmpty(mch_id1)) signBuilder.Append($"&mch_id={mch_id1}");
                if(!string.IsNullOrEmpty(nonce_str1)) signBuilder.Append($"&nonce_str={nonce_str1}");
                if(!string.IsNullOrEmpty(offset1)) signBuilder.Append($"&offset={offset1}");
                if(!string.IsNullOrEmpty(out_refund_no1)) signBuilder.Append($"&out_refund_no={out_refund_no1}");
                if(!string.IsNullOrEmpty(out_trade_no1)) signBuilder.Append($"&out_trade_no={out_trade_no1}");
                if(!string.IsNullOrEmpty(refund_id1)) signBuilder.Append($"&refund_id={refund_id1}");
                if(!string.IsNullOrEmpty(sign_type1)) signBuilder.Append($"&sign_type={sign_type1}");
                if(!string.IsNullOrEmpty(transaction_id1)) signBuilder.Append($"&transaction_id={transaction_id1}");

                signBuilder.Append($"&key={mch_key1}");
                return signBuilder.ToString().GetHashUTF8().ToUpperInvariant();
            }

            string BuildRequestXml(string appid1, string mch_id1, string nonce_str1, string refund_id1, string sign1, string offset1 = "", string out_refund_no1 = "", string out_trade_no1 = "", string sign_type1 = "MD5", string transaction_id1 = "")
            {
                StringBuilder xmlBuilder = new StringBuilder("<xml>");

                xmlBuilder.Append($"<appid>{appid1}</appid>");

                if(!string.IsNullOrEmpty(mch_id1)) xmlBuilder.Append($"<mch_id>{mch_id1}</mch_id>");
                if(!string.IsNullOrEmpty(nonce_str1)) xmlBuilder.Append($"<nonce_str>{nonce_str1}</nonce_str>");
                if(!string.IsNullOrEmpty(offset1)) xmlBuilder.Append($"<offset>{offset1}</offset>");
                if(!string.IsNullOrEmpty(out_refund_no1)) xmlBuilder.Append($"<out_refund_no>{out_refund_no1}</out_refund_no>");
                if(!string.IsNullOrEmpty(out_trade_no1)) xmlBuilder.Append($"<out_trade_no>{out_trade_no1}</out_trade_no>");
                if(!string.IsNullOrEmpty(refund_id1)) xmlBuilder.Append($"<refund_id>{refund_id1}</refund_id>");
                if(!string.IsNullOrEmpty(sign1)) xmlBuilder.Append($"<sign>{sign1}</sign>");
                if(!string.IsNullOrEmpty(sign_type1)) xmlBuilder.Append($"<sign_type>{sign_type1}</sign_type>");
                if(!string.IsNullOrEmpty(transaction_id1)) xmlBuilder.Append($"<transaction_id>{transaction_id1}</transaction_id>");

                return xmlBuilder.Append("</xml>").ToString();
            }
        }
    }
}