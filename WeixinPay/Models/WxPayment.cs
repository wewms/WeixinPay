namespace WeixinPay.Models
{
    public class WxPayment
    {
        public string TimeStamp { get; set; }
        public string NonceStr { get; set; }
        public string Package { get; set; }
        public string SignType { get; set; }
        public string PaySign { get; set; }
    }
}