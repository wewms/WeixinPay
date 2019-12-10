namespace WeixinPay.Models
{
    public class WxAccessToken
    {
        // {"access_token": "ACCESS_TOKEN", "expires_in": 7200} or {"errcode": 40013, "errmsg": "invalid appid"}
        public string access_token { get; set; }
        public double expires_in { get; set; }
    }
}