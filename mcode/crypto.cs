using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace mcode
{
    struct signal_levels
    {
        public double BroadcastSignalLevel;
        public double Ecio;
    }
    class crypto
    {
        static private System.DateTime ToDateTime(long seconds)
        {
            System.DateTime dt1970 = new System.DateTime(1970, 1, 1);
            return dt1970.AddSeconds(seconds);
        }

        public static long ToSecondsSince1970(System.DateTime time)
        {
            System.DateTime dt1970 = new System.DateTime(1970, 1, 1);
            TimeSpan span = time - dt1970;
            return (long)span.TotalSeconds;
        }

        public static string Encrypt(signal_levels sls, Int64 utc, Int64 nanoseconds)
        {
            var iv = GenerateIv(utc, nanoseconds);
            var key = GenerateKey(0x77, 0xe2); // Keeping the encryption intact as the business is long defunct (except for Tom who isn't using encryption). 

            string pretty_cipher;

            using (RijndaelManaged rijAlg = new RijndaelManaged())
            {
                InitializeRijndael(iv, key, rijAlg);

                // Create a encrytor to perform the stream transform.
                ICryptoTransform encryptor = rijAlg.CreateEncryptor(rijAlg.Key, rijAlg.IV);

                // Create the streams used for encryption. 
                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        var size = sizeof(double);
                        byte[] buffer = new byte[size * 2];
                        Buffer.BlockCopy(BitConverter.GetBytes(sls.BroadcastSignalLevel), 0, buffer, 0, size);
                        Buffer.BlockCopy(BitConverter.GetBytes(sls.Ecio), 0, buffer, size, size);

                        //Write all data to the stream.
                        csEncrypt.Write(buffer, 0, buffer.Length);

                        pretty_cipher = Convert.ToBase64String(msEncrypt.ToArray());
                    }
                }
            }

            // Return the encrypted bytes from the memory stream. 
            return pretty_cipher;
        }
        private static void InitializeRijndael(byte[] iv, byte[] key, RijndaelManaged rijAlg)
        {
            if(iv.Length != 16)
                throw new ArgumentOutOfRangeException("IV should be 16 bytes long.");
            if(key.Length != 16)
                throw new ArgumentOutOfRangeException("Key should be 16 bytes long.");
            rijAlg.BlockSize = 128;
            rijAlg.KeySize = 128;
            rijAlg.Mode = CipherMode.CBC;
            rijAlg.Padding = PaddingMode.None;
            rijAlg.Key = key;
            rijAlg.IV = iv;
        }

        private static byte[] GenerateIv(Int64 utc, Int64 nanoseconds) 
        {
            var size = sizeof(Int64);
            byte[] buffer = new byte[size*2];
            Buffer.BlockCopy(BitConverter.GetBytes(utc), 0, buffer, 0, size);
            Buffer.BlockCopy(BitConverter.GetBytes(nanoseconds), 0, buffer, size, size);
            var iv = SHA256.Create().ComputeHash(buffer);
            Array.Resize(ref iv, 16);
            return iv;
        }

        private static byte[] GenerateKey(byte a, byte b) 
        {
            byte[] buffer = new byte[128 / 8];
            buffer[0] = (byte)(a ^ 0x32);
            buffer[1] = (byte)(b ^ 0xe1);
            for (int i = 2; i < buffer.Length; i += 2)
            {
                buffer[i] = (byte)(buffer[i - 2] ^ buffer[i - 1]);
                buffer[i + 1] = (byte)(buffer[i] << 4 | buffer[i - 2] >> 4);
            }
            var key = SHA256.Create().ComputeHash(buffer);
            Array.Resize(ref key, 16);
            return key;
        }

        static public signal_levels Decrypt(string pretty_cipher, long utc, long nanoseconds)
        {
            signal_levels sls;

            var iv = GenerateIv(utc, nanoseconds);
            var key = GenerateKey(0x77, 0xe2);
            
            // Create an RijndaelManaged object 
            // with the specified key and IV. 
            using (RijndaelManaged rijAlg = new RijndaelManaged())
            {
                InitializeRijndael(iv, key, rijAlg);

                // Create a decrytor to perform the stream transform.
                ICryptoTransform decryptor = rijAlg.CreateDecryptor(rijAlg.Key, rijAlg.IV);

                byte[] cipher = Convert.FromBase64String(pretty_cipher);

                // Create the streams used for decryption. 
                using (MemoryStream msDecrypt = new MemoryStream(cipher))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (BinaryReader srDecrypt = new BinaryReader(csDecrypt))
                        {
                            var size = sizeof(double);
                            var buffer = srDecrypt.ReadBytes(size * 2);
                            sls.BroadcastSignalLevel = BitConverter.ToDouble(buffer, 0);
                            sls.Ecio = BitConverter.ToDouble(buffer, size);
                        }
                    }
                }
            }
            return sls;
        }
    }
}
