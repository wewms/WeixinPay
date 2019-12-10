using System;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WeixinPay.Extensions;
using WeixinPay.Logging;
using WeixinPay.Models;

namespace WeixinPay
{
    public class WeixinPay
    {
        private static readonly Regex PrepayIdPattern = new Regex(@"<prepay_id><!\[CDATA\[([^\]]+)\]\]></prepay_id>", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private readonly HttpClient _client;

        private readonly AppLogger _logger;
        private readonly WxOptions _options;

        public WeixinPay(HttpClient client, AppLogger logger, IOptions<WxOptions> options)
        {
            _client = client;
            _logger = logger;
            _options = options.Value;
        }

        /// <summary>
        ///     weixin preplace
        /// </summary>
        /// <param name="orderPaymentId"></param>
        /// <param name="amount"></param>
        /// <param name="wxOpenId"></param>
        /// <param name="billCreationIp"></param>
        /// <param name="attach"></param>
        /// <returns></returns>
        public async Task<WxPayment> Preplace(string orderPaymentId, decimal amount, string wxOpenId, string billCreationIp, string attach = "")
        {
            var nonceStr = orderPaymentId.GetHashUTF8();
            var prepayId = await UnifiedOrder(nonceStr, orderPaymentId, amount, wxOpenId, billCreationIp, attach);
            if(string.IsNullOrEmpty(prepayId)) return null;

            string timeStamp = Convert.ToInt64(DateTime.UtcNow.Subtract(DateTime.MinValue.AddYears(1969)).TotalSeconds).ToString();
            return new WxPayment {SignType = "MD5", NonceStr = nonceStr, Package = $"prepay_id={prepayId}", PaySign = $"appId={_options.WxAppId}&nonceStr={nonceStr}&package=prepay_id={prepayId}&signType=MD5&timeStamp={timeStamp}&key={_options.WxMchKey}".GetHashUTF8(), TimeStamp = timeStamp};
        }

        /// <summary>
        ///     Get weixin prepayId
        /// </summary>
        /// <param name="nonceStr"></param>
        /// <param name="orderPaymentId"></param>
        /// <param name="amount"></param>
        /// <param name="wxOpenId"></param>
        /// <param name="attach"></param>
        /// <param name="billCreationIp"></param>
        /// <returns></returns>
        private async Task<string> UnifiedOrder(string nonceStr, string orderPaymentId, decimal amount, string wxOpenId, string billCreationIp, string attach)
        {
            UnifiedOrder unifiedOrder = new UnifiedOrder {
                MchId = _options.WxMchId,
                AppId = _options.WxAppId,
                NotifyUrl = _options.WxPaymentNotifyUrl,
                SignType = "MD5",
                TradeType = "JSAPI",
                Body = $"{_options.WxPlatformName}订单",

                NonceStr = nonceStr,
                OutTradeNo = orderPaymentId,
                TotalFee = Math.Round(amount * 100).ToString(), // unit: fen max scale is 2
                SPBillCreateIP = billCreationIp,
                OpenId = wxOpenId,
                Attach = attach
            };

            // Sign
            unifiedOrder.Sign = BuildSignString(unifiedOrder);

            // Xml
            string requestXml = BuildRequestXml(unifiedOrder);
            if(_options.WxIsDebug) _logger.EnqueueMessage($"UnifiedOrder: {requestXml}");

            try
            {
                HttpContent content = new ByteArrayContent(Encoding.UTF8.GetBytes(requestXml));
                var res = await _client.PostAsync("https://api.mch.weixin.qq.com/pay/unifiedorder", content);
                string xml = await res.Content.ReadAsStringAsync();
                if(_options.WxIsDebug) _logger.EnqueueMessage(xml);

                //bool isReturnSuccess = xml.IndexOf("<return_code><![CDATA[SUCCESS]]></return_code>", StringComparison.Ordinal) != -1;
                bool isReturnSuccess = xml.StartsWith("<xml><return_code><![CDATA[SUCCESS]]></return_code>", StringComparison.Ordinal);
                if(!isReturnSuccess) return string.Empty;

                bool isResultSuccess = xml.IndexOf("<result_code><![CDATA[SUCCESS]]></result_code>", StringComparison.Ordinal) != -1;
                return isResultSuccess ? PrepayIdPattern.Match(xml).Groups[1].Value : string.Empty /*error*/;
            }
            catch(Exception ex)
            {
                _logger.EnqueueMessage($"{nameof(WeixinPay)}.{nameof(UnifiedOrder)} error. Message: {ex.Message} InnerMessage: {ex.InnerException?.Message} StackTrace: {ex.StackTrace}");
            }

            return string.Empty;

            string BuildRequestXml(UnifiedOrder order)
            {
                StringBuilder xmlBuilder = new StringBuilder("<xml>");

                xmlBuilder.Append($"<appid>{order.AppId}</appid>");

                if(!string.IsNullOrEmpty(order.Attach)) xmlBuilder.Append($"<attach>{order.Attach}</attach>");
                if(!string.IsNullOrEmpty(order.Body)) xmlBuilder.Append($"<body>{order.Body}</body>");
                if(!string.IsNullOrEmpty(order.Detail)) xmlBuilder.Append($"<detail>{order.Detail}</detail>");
                if(!string.IsNullOrEmpty(order.DeviceInfo)) xmlBuilder.Append($"<device_info>{order.DeviceInfo}</device_info>");
                if(!string.IsNullOrEmpty(order.FeeType)) xmlBuilder.Append($"<fee_type>{order.FeeType}</fee_type>");
                if(!string.IsNullOrEmpty(order.GoodsTag)) xmlBuilder.Append($"<goods_tag>{order.GoodsTag}</goods_tag>");
                if(!string.IsNullOrEmpty(order.LimitPay)) xmlBuilder.Append($"<limit_pay>{order.LimitPay}</limit_pay>");
                if(!string.IsNullOrEmpty(order.MchId)) xmlBuilder.Append($"<mch_id>{order.MchId}</mch_id>");
                if(!string.IsNullOrEmpty(order.NonceStr)) xmlBuilder.Append($"<nonce_str>{order.NonceStr}</nonce_str>");
                if(!string.IsNullOrEmpty(order.NotifyUrl)) xmlBuilder.Append($"<notify_url>{order.NotifyUrl}</notify_url>");
                if(!string.IsNullOrEmpty(order.OpenId)) xmlBuilder.Append($"<openid>{order.OpenId}</openid>");
                if(!string.IsNullOrEmpty(order.OutTradeNo)) xmlBuilder.Append($"<out_trade_no>{order.OutTradeNo}</out_trade_no>");
                if(!string.IsNullOrEmpty(order.ProductId)) xmlBuilder.Append($"<product_id>{order.ProductId}</product_id>");
                if(!string.IsNullOrEmpty(order.Receipt)) xmlBuilder.Append($"<receipt>{order.Receipt}</receipt>");
                if(!string.IsNullOrEmpty(order.SceneInfo)) xmlBuilder.Append($"<scene_info>{order.SceneInfo}</scene_info>");
                if(!string.IsNullOrEmpty(order.Sign)) xmlBuilder.Append($"<sign>{order.Sign}</sign>");
                if(!string.IsNullOrEmpty(order.SignType)) xmlBuilder.Append($"<sign_type>{order.SignType}</sign_type>");
                if(!string.IsNullOrEmpty(order.SPBillCreateIP)) xmlBuilder.Append($"<spbill_create_ip>{order.SPBillCreateIP}</spbill_create_ip>");
                if(!string.IsNullOrEmpty(order.TimeExpire)) xmlBuilder.Append($"<time_expire>{order.TimeExpire}</time_expire>");
                if(!string.IsNullOrEmpty(order.TimeStart)) xmlBuilder.Append($"<time_start>{order.TimeStart}</time_start>");
                if(!string.IsNullOrEmpty(order.TotalFee)) xmlBuilder.Append($"<total_fee>{order.TotalFee}</total_fee>");
                if(!string.IsNullOrEmpty(order.TradeType)) xmlBuilder.Append($"<trade_type>{order.TradeType}</trade_type>");

                return xmlBuilder.Append("</xml>").ToString();
            }

            string BuildSignString(UnifiedOrder order)
            {
                StringBuilder signBuilder = new StringBuilder();

                signBuilder.Append($"appid={order.AppId}");

                if(!string.IsNullOrEmpty(order.Attach)) signBuilder.Append($"&attach={order.Attach}");
                if(!string.IsNullOrEmpty(order.Body)) signBuilder.Append($"&body={order.Body}");
                if(!string.IsNullOrEmpty(order.Detail)) signBuilder.Append($"&detail={order.Detail}");
                if(!string.IsNullOrEmpty(order.DeviceInfo)) signBuilder.Append($"&device_info={order.DeviceInfo}");
                if(!string.IsNullOrEmpty(order.FeeType)) signBuilder.Append($"&fee_type={order.FeeType}");
                if(!string.IsNullOrEmpty(order.GoodsTag)) signBuilder.Append($"&goods_tag={order.GoodsTag}");
                if(!string.IsNullOrEmpty(order.LimitPay)) signBuilder.Append($"&limit_pay={order.LimitPay}");
                if(!string.IsNullOrEmpty(order.MchId)) signBuilder.Append($"&mch_id={order.MchId}");
                if(!string.IsNullOrEmpty(order.NonceStr)) signBuilder.Append($"&nonce_str={order.NonceStr}");
                if(!string.IsNullOrEmpty(order.NotifyUrl)) signBuilder.Append($"&notify_url={order.NotifyUrl}");
                if(!string.IsNullOrEmpty(order.OpenId)) signBuilder.Append($"&openid={order.OpenId}");
                if(!string.IsNullOrEmpty(order.OutTradeNo)) signBuilder.Append($"&out_trade_no={order.OutTradeNo}");
                if(!string.IsNullOrEmpty(order.ProductId)) signBuilder.Append($"&product_id={order.ProductId}");
                if(!string.IsNullOrEmpty(order.Receipt)) signBuilder.Append($"&receipt={order.Receipt}");
                if(!string.IsNullOrEmpty(order.SceneInfo)) signBuilder.Append($"&scene_info={order.SceneInfo}");
                if(!string.IsNullOrEmpty(order.SignType)) signBuilder.Append($"&sign_type={order.SignType}");
                if(!string.IsNullOrEmpty(order.SPBillCreateIP)) signBuilder.Append($"&spbill_create_ip={order.SPBillCreateIP}");
                if(!string.IsNullOrEmpty(order.TimeExpire)) signBuilder.Append($"&time_expire={order.TimeExpire}");
                if(!string.IsNullOrEmpty(order.TimeStart)) signBuilder.Append($"&time_start={order.TimeStart}");
                if(!string.IsNullOrEmpty(order.TotalFee)) signBuilder.Append($"&total_fee={order.TotalFee}");
                if(!string.IsNullOrEmpty(order.TradeType)) signBuilder.Append($"&trade_type={order.TradeType}");

                signBuilder.Append($"&key={_options.WxMchKey}");
                return signBuilder.ToString().GetHashUTF8().ToUpperInvariant();
            }
        }

        /// <summary>
        ///     Verify payment callback
        /// </summary>
        /// <param name="xml"></param>
        /// <returns></returns>
        public (string TransactionId, string OrderId, int Status, string errDesc, string storeIdStr) VerifyPaymentCallback(string xml)
        {
            if(_options.WxIsDebug) _logger.EnqueueMessage($"WxPayCallback: {xml}");

            var signBuilder = new StringBuilder();

            var appid = Regex.Match(xml, @"<appid><\!\[CDATA\[([^\]]+)\]\]></appid>").Groups[1].Value;
            if(!string.IsNullOrEmpty(appid)) signBuilder.Append($"appid={appid}");

            var attach = Regex.Match(xml, @"<attach><\!\[CDATA\[([^\]]+)\]\]></attach>").Groups[1].Value;
            if(!string.IsNullOrEmpty(attach)) signBuilder.Append($"&attach={attach}");

            var bank_type = Regex.Match(xml, @"<bank_type><\!\[CDATA\[([^\]]+)\]\]></bank_type>").Groups[1].Value;
            if(!string.IsNullOrEmpty(bank_type)) signBuilder.Append($"&bank_type={bank_type}");

            var cash_fee = Regex.Match(xml, @"<cash_fee>([^<]+)</cash_fee>").Groups[1].Value;
            if(string.IsNullOrEmpty(cash_fee)) cash_fee = Regex.Match(xml, @"<cash_fee><\!\[CDATA\[([^\]]+)\]\]></cash_fee>").Groups[1].Value;
            if(!string.IsNullOrEmpty(cash_fee)) signBuilder.Append($"&cash_fee={cash_fee}");

            var cash_fee_type = Regex.Match(xml, @"<cash_fee_type><\!\[CDATA\[([^\]]+)\]\]></cash_fee_type>").Groups[1].Value;
            if(!string.IsNullOrEmpty(cash_fee_type)) signBuilder.Append($"&cash_fee_type={cash_fee_type}");

            var coupon_count = Regex.Match(xml, @"<coupon_count>([^<]+)</coupon_count>").Groups[1].Value;
            if(string.IsNullOrEmpty(coupon_count)) coupon_count = Regex.Match(xml, @"<coupon_count><\!\[CDATA\[([^\]]+)\]\]></coupon_count>").Groups[1].Value;
            if(!string.IsNullOrEmpty(coupon_count) && int.TryParse(coupon_count, out int couponCount))
            {
                // coupon_count
                signBuilder.Append($"&coupon_count={coupon_count}");

                // coupon_fee
                var coupon_fee = Regex.Match(xml, @"<coupon_fee>([^<]+)</coupon_fee>").Groups[1].Value;
                if(string.IsNullOrEmpty(coupon_fee)) coupon_fee = Regex.Match(xml, @"<coupon_fee><\!\[CDATA\[([^\]]+)\]\]></coupon_fee>").Groups[1].Value;
                if(!string.IsNullOrEmpty(coupon_fee)) signBuilder.Append($"&coupon_fee={coupon_fee}");

                for(int i = 0; i < couponCount; i++)
                {
                    // coupon_fee_$n
                    var coupon_fee_n = Regex.Match(xml, $@"<coupon_fee_{i}>([^<]+)</coupon_fee_{i}>").Groups[1].Value;
                    if(string.IsNullOrEmpty(coupon_fee_n)) coupon_fee_n = Regex.Match(xml, $@"<coupon_fee_{i}><\!\[CDATA\[([^\]]+)\]\]></coupon_fee_{i}>").Groups[1].Value;
                    if(!string.IsNullOrEmpty(coupon_fee_n)) signBuilder.Append($"&coupon_fee_{i}={coupon_fee_n}");

                    // coupon_id_$n
                    var coupon_id_n = Regex.Match(xml, $@"<coupon_id_{i}>([^<]+)</coupon_id_{i}>").Groups[1].Value;
                    if(string.IsNullOrEmpty(coupon_id_n)) coupon_id_n = Regex.Match(xml, $@"<coupon_id_{i}><\!\[CDATA\[([^\]]+)\]\]></coupon_id_{i}>").Groups[1].Value;
                    if(!string.IsNullOrEmpty(coupon_id_n)) signBuilder.Append($"&coupon_id_{i}={coupon_id_n}");

                    // coupon_type_$n
                    var coupon_type_n = Regex.Match(xml, $@"<coupon_type_{i}>([^<]+)</coupon_type_{i}>").Groups[1].Value;
                    if(string.IsNullOrEmpty(coupon_type_n)) coupon_type_n = Regex.Match(xml, $@"<coupon_type_{i}><\!\[CDATA\[([^\]]+)\]\]></coupon_type_{i}>").Groups[1].Value;
                    if(!string.IsNullOrEmpty(coupon_type_n)) signBuilder.Append($"&coupon_type_{i}={coupon_type_n}");
                }
            }

            var device_info = Regex.Match(xml, @"<device_info><\!\[CDATA\[([^\]]+)\]\]></device_info>").Groups[1].Value;
            if(!string.IsNullOrEmpty(device_info)) signBuilder.Append($"&device_info={device_info}");

            var err_code = Regex.Match(xml, @"<err_code><\!\[CDATA\[([^\]]+)\]\]></err_code>").Groups[1].Value;
            if(!string.IsNullOrEmpty(err_code)) signBuilder.Append($"&err_code={err_code}");

            var err_code_des = Regex.Match(xml, @"<err_code_des><\!\[CDATA\[([^\]]+)\]\]></err_code_des>").Groups[1].Value;
            if(!string.IsNullOrEmpty(err_code_des)) signBuilder.Append($"&err_code_des={err_code_des}");

            var fee_type = Regex.Match(xml, @"<fee_type><\!\[CDATA\[([^\]]+)\]\]></fee_type>").Groups[1].Value;
            if(!string.IsNullOrEmpty(fee_type)) signBuilder.Append($"&fee_type={fee_type}");

            var is_subscribe = Regex.Match(xml, @"<is_subscribe><\!\[CDATA\[([^\]]+)\]\]></is_subscribe>").Groups[1].Value;
            if(!string.IsNullOrEmpty(is_subscribe)) signBuilder.Append($"&is_subscribe={is_subscribe}");

            var mch_id = Regex.Match(xml, @"<mch_id><\!\[CDATA\[([^\]]+)\]\]></mch_id>").Groups[1].Value;
            if(!string.IsNullOrEmpty(mch_id)) signBuilder.Append($"&mch_id={mch_id}");

            var nonce_str = Regex.Match(xml, @"<nonce_str><\!\[CDATA\[([^\]]+)\]\]></nonce_str>").Groups[1].Value;
            if(!string.IsNullOrEmpty(nonce_str)) signBuilder.Append($"&nonce_str={nonce_str}");

            var openid = Regex.Match(xml, @"<openid><\!\[CDATA\[([^\]]+)\]\]></openid>").Groups[1].Value;
            if(!string.IsNullOrEmpty(openid)) signBuilder.Append($"&openid={openid}");

            var out_trade_no = Regex.Match(xml, @"<out_trade_no><\!\[CDATA\[([^\]]+)\]\]></out_trade_no>").Groups[1].Value;
            if(!string.IsNullOrEmpty(out_trade_no)) signBuilder.Append($"&out_trade_no={out_trade_no}");

            var result_code = Regex.Match(xml, @"<result_code><\!\[CDATA\[([^\]]+)\]\]></result_code>").Groups[1].Value;
            if(!string.IsNullOrEmpty(result_code)) signBuilder.Append($"&result_code={result_code}");

            var return_code = Regex.Match(xml, @"<return_code><\!\[CDATA\[([^\]]+)\]\]></return_code>").Groups[1].Value;
            if(!string.IsNullOrEmpty(return_code)) signBuilder.Append($"&return_code={return_code}");

            var return_msg = Regex.Match(xml, @"<return_msg><\!\[CDATA\[([^\]]+)\]\]></return_msg>").Groups[1].Value;
            if(!string.IsNullOrEmpty(return_msg)) signBuilder.Append($"&return_msg={return_msg}");

            var settlement_total_fee = Regex.Match(xml, @"<settlement_total_fee>([^<]+)</settlement_total_fee>").Groups[1].Value;
            if(!string.IsNullOrEmpty(settlement_total_fee)) signBuilder.Append($"&settlement_total_fee={settlement_total_fee}");

            var sign = Regex.Match(xml, @"<sign><\!\[CDATA\[([^\]]+)\]\]></sign>").Groups[1].Value;
            //if(!string.IsNullOrEmpty(sign)) signBuilder.Append($"&sign={sign}");

            var sign_type = Regex.Match(xml, @"<sign_type><\!\[CDATA\[([^\]]+)\]\]></sign_type>").Groups[1].Value;
            if(!string.IsNullOrEmpty(sign_type)) signBuilder.Append($"&sign_type={sign_type}");

            var time_end = Regex.Match(xml, @"<time_end><\!\[CDATA\[([^\]]+)\]\]></time_end>").Groups[1].Value;
            if(!string.IsNullOrEmpty(time_end)) signBuilder.Append($"&time_end={time_end}");

            var total_fee = Regex.Match(xml, @"<total_fee>([^<]+)</total_fee>").Groups[1].Value;
            if(string.IsNullOrEmpty(total_fee)) total_fee = Regex.Match(xml, @"<total_fee><\!\[CDATA\[([^\]]+)\]\]></total_fee>").Groups[1].Value;
            if(!string.IsNullOrEmpty(total_fee)) signBuilder.Append($"&total_fee={total_fee}");

            var trade_type = Regex.Match(xml, @"<trade_type><\!\[CDATA\[([^\]]+)\]\]></trade_type>").Groups[1].Value;
            if(!string.IsNullOrEmpty(trade_type)) signBuilder.Append($"&trade_type={trade_type}");

            var transaction_id = Regex.Match(xml, @"<transaction_id><\!\[CDATA\[([^\]]+)\]\]></transaction_id>").Groups[1].Value;
            if(!string.IsNullOrEmpty(transaction_id)) signBuilder.Append($"&transaction_id={transaction_id}");

            signBuilder.Append($"&key={_options.WxMchKey}");

            var signString = signBuilder.ToString() /*.Substring(1)*/; // remove first char &
            var hash = signString.GetHashUTF8().ToUpperInvariant();
            var isSucceeded = return_code == "SUCCESS" && result_code == "SUCCESS";

            if(_options.WxIsDebug) _logger.EnqueueMessage($"WxPayCallback SignString: {signString} Hash: {hash}");

            return hash.Equals(sign, StringComparison.Ordinal) ? (transaction_id, out_trade_no, isSucceeded ? 1 : 2, isSucceeded ? "" : err_code_des, attach) : ("", "", 0, "", "");
        }
    }
}