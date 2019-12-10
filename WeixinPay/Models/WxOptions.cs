namespace WeixinPay.Models
{
    public class WxOptions
    {
        public string WxSecretKey { get; set; }
        public string WxAppId { get; set; }
        public string WxMchId { get; set; }
        public string WxMchKey { get; set; }
        public string WxApiCert { get; set; }

        /// <summary>
        ///     payment callback url
        /// </summary>
        public string WxPaymentNotifyUrl { get; set; }

        /// <summary>
        ///     refund callback url
        /// </summary>
        public string WxRefundNotifyUrl { get; set; }

        public string WxPlatformName { get; set; }
        public bool WxIsDebug { get; set; }
    }
}