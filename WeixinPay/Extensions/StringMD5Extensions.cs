using System;
using System.Security.Cryptography;
using System.Text;

namespace WeixinPay.Extensions
{
    public static class StringMD5Extensions
    {
        public static string GetHashUTF8(this string input)
        {
            return input == null ? throw new ArgumentNullException(nameof(input)) : GetHashInternal(input, Encoding.UTF8);
        }

        public static string GetHashEncoding(this string input, Encoding encoding)
        {
            if(encoding == null) throw new ArgumentNullException(nameof(encoding));

            return input == null ? throw new ArgumentNullException(nameof(input)) : GetHashInternal(input, encoding);
        }

        private static string GetHashInternal(string input, Encoding encoding)
        {
            using MD5 md5Hash = MD5.Create();
            byte[] data = md5Hash.ComputeHash(encoding.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes 
            // and create a string.
            var sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data  
            // and format each one as a hexadecimal string. 
            //foreach (byte t in data) sBuilder.Append(t.ToString("x2"));
            for(int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string. 
            return sBuilder.ToString();
        }

        // Verify a hash against a string. 
        public static bool VerifyHashUTF8(this string input, string compareToHash)
        {
            if(input == null) throw new ArgumentNullException(nameof(input));

            // Hash the input. 
            string hashOfInput = input.GetHashUTF8();
            return hashOfInput.Equals(compareToHash, StringComparison.OrdinalIgnoreCase);
        }

        public static bool VerifyHashEncoding(this string input, string compareToHash, Encoding encoding)
        {
            if(input == null) throw new ArgumentNullException(nameof(input));
            if(encoding == null) throw new ArgumentNullException(nameof(encoding));

            // Hash the input. 
            string hashOfInput = input.GetHashEncoding(encoding);
            return hashOfInput.Equals(compareToHash, StringComparison.OrdinalIgnoreCase);
        }
    }
}