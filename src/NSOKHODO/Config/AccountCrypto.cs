using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace NSOKHODO.Config
{
    /// <summary>
    /// Ma hoa NOI DUNG file danh sach tai khoan (AES-256-CBC). Xem
    /// docs/features/MA_HOA_PASS.md va BAO_MAT_SRC.md §5.1.
    ///
    /// <para><b>Muc tieu:</b> zip thu muc build gui cho nguoi khac thi mat khau khong nam tho tren
    /// dia. <b>KHONG</b> chong duoc nguoi dich nguoc binary - key nam trong chinh app, day la danh
    /// doi da biet va user chap nhan (chot 2026-09-11).</para>
    ///
    /// <para><b>Vi sao KHONG dung DPAPI (ProtectedData):</b> hai ly do doc lap. (1) DPAPI khoa theo
    /// may/user -> be cum exe + Data len VPS la mat sach mat khau, pha vo tinh portable ma
    /// <see cref="AppPaths"/> co y giu. (2) ProtectedData CHI co tren Windows - ban Android dung
    /// chung nguon nay se nem loi luc chay.</para>
    ///
    /// <para><b>Vi sao dan key bang SHA-256 chu khong PBKDF2:</b> PBKDF2 sinh ra de lam CHAM viec
    /// do mat khau NGUOI DUNG. O day secret la hang so nam san trong binary - ke tan cong doc thang
    /// ra chu khong do, nen so vong lap khong them an toan that. Doi lai, cac constructor
    /// Rfc2898DeriveBytes khong chi dinh thuat toan bam da bi danh dau obsolete (SYSLIB0041) tren
    /// .NET moi -> ban Android se canh bao. Dung SHA-256 thi sach o ca net452 lan net9.0-android.</para>
    /// </summary>
    internal static class AccountCrypto
    {
        /// <summary>Dong dau file da ma hoa. File khong bat dau bang day nay = plaintext ban cu.</summary>
        public const string MAGIC = "NSOENC1";

        // Secret nhung + salt co dinh. Doi hai gia tri nay = moi file acc da ma hoa THANH RAC,
        // nguoi dung mat het mat khau -> KHONG bao gio doi sau khi da phat hanh.
        private const string SECRET = "NSOKHODO/acc/v1/6f2b9c41-tungvz";
        private static readonly byte[] SALT =
        {
            0x4E, 0x53, 0x4F, 0x4C, 0x49, 0x54, 0x45, 0x50,
            0x52, 0x4F, 0x2D, 0x61, 0x63, 0x63, 0x2D, 0x31
        };

        private static byte[] Key()
        {
            using (var sha = SHA256.Create())
            {
                var seed = Encoding.UTF8.GetBytes(SECRET);
                var buf = new byte[seed.Length + SALT.Length];
                Buffer.BlockCopy(seed, 0, buf, 0, seed.Length);
                Buffer.BlockCopy(SALT, 0, buf, seed.Length, SALT.Length);
                return sha.ComputeHash(buf);   // 32 byte = AES-256
            }
        }

        /// <summary>Noi dung nay da ma hoa chua? (nhan dien bang dong magic o dau file)</summary>
        public static bool IsEncrypted(string text)
        {
            return text != null && text.StartsWith(MAGIC, StringComparison.Ordinal);
        }

        /// <summary>Ma hoa -> "NSOENC1\n" + base64(iv + ciphertext).</summary>
        public static string Encrypt(string plain)
        {
            if (plain == null) plain = "";
            using (var aes = Aes.Create())
            {
                aes.Key = Key();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();

                byte[] body = Encoding.UTF8.GetBytes(plain);
                byte[] cipher;
                using (var enc = aes.CreateEncryptor())
                    cipher = enc.TransformFinalBlock(body, 0, body.Length);

                var all = new byte[aes.IV.Length + cipher.Length];
                Buffer.BlockCopy(aes.IV, 0, all, 0, aes.IV.Length);
                Buffer.BlockCopy(cipher, 0, all, aes.IV.Length, cipher.Length);

                return MAGIC + Environment.NewLine + Convert.ToBase64String(all);
            }
        }

        /// <summary>
        /// Giai ma. Tra <c>null</c> neu hong (file cut, sai key, base64 rac) - de goi y BAO LOI
        /// chu KHONG duoc im lang coi nhu "khong co tai khoan nao", vi buoc tiep theo la GHI DE.
        /// </summary>
        public static string Decrypt(string blob)
        {
            if (!IsEncrypted(blob)) return null;
            try
            {
                string b64 = blob.Substring(MAGIC.Length).Trim();
                byte[] all = Convert.FromBase64String(b64);
                if (all.Length <= 16) return null;

                using (var aes = Aes.Create())
                {
                    aes.Key = Key();
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;

                    var iv = new byte[16];
                    Buffer.BlockCopy(all, 0, iv, 0, 16);
                    aes.IV = iv;

                    using (var dec = aes.CreateDecryptor())
                    {
                        byte[] plain = dec.TransformFinalBlock(all, 16, all.Length - 16);
                        return Encoding.UTF8.GetString(plain);
                    }
                }
            }
            catch { return null; }
        }
    }
}
