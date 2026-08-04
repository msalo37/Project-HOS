using System;
using System.Security.Cryptography;
using System.Text;
using HOS.Domain.FileSystem;

namespace HOS.Application.Execution
{
    public readonly struct HosBinaryDescriptor
    {
        public HosBinaryDescriptor(string programId)
        {
            ProgramId = programId;
        }

        public string ProgramId { get; }
    }

    public static class HosBinaryFormat
    {
        private const byte FormatVersion = 1;
        private const int IvLength = 16;
        private const int LengthFieldSize = 2;
        private const int SignatureLength = 32;
        private const int HeaderLength = 4 + 1 + IvLength + LengthFieldSize;
        private const int MinimumCiphertextLength = 16;

        private static readonly byte[] Magic = { 0x48, 0x4f, 0x53, 0x00 };
        private static readonly byte[] EncryptionKey = DeriveKey(
            "Project HOS executable encryption key v1");
        private static readonly byte[] SignatureKey = DeriveKey(
            "Project HOS executable signature key v1");
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly char[] DisplayGlyphs = { '�', '▓', '▒', '░' };

        public const string Extension = ".hos";

        public static FileContent Create(string programId)
        {
            if (!IsValidProgramId(programId))
                throw new ArgumentException("Invalid HOS program id.", nameof(programId));

            var plaintext = StrictUtf8.GetBytes(programId);
            var ciphertext = Encrypt(plaintext, out var iv);
            if (ciphertext.Length > ushort.MaxValue)
                throw new InvalidOperationException("HOS executable payload is too large.");

            var authenticated = new byte[HeaderLength + ciphertext.Length];
            var offset = 0;
            Buffer.BlockCopy(Magic, 0, authenticated, offset, Magic.Length);
            offset += Magic.Length;
            authenticated[offset++] = FormatVersion;
            Buffer.BlockCopy(iv, 0, authenticated, offset, iv.Length);
            offset += iv.Length;
            authenticated[offset++] = (byte)(ciphertext.Length & 0xff);
            authenticated[offset++] = (byte)(ciphertext.Length >> 8);
            Buffer.BlockCopy(ciphertext, 0, authenticated, offset, ciphertext.Length);

            var signature = ComputeSignature(authenticated);
            var result = new byte[authenticated.Length + signature.Length];
            Buffer.BlockCopy(authenticated, 0, result, 0, authenticated.Length);
            Buffer.BlockCopy(signature, 0, result, authenticated.Length, signature.Length);
            return new FileContent(result);
        }

        public static bool IsBinary(FileContent content)
        {
            if (content == null)
                return false;

            var bytes = content.CopyBytes();
            if (bytes.Length < Magic.Length)
                return false;

            for (var index = 0; index < Magic.Length; index++)
            {
                if (bytes[index] != Magic[index])
                    return false;
            }

            return true;
        }

        public static string ToDisplayString(FileContent content)
        {
            if (content == null)
                return string.Empty;

            var bytes = content.CopyBytes();
            var display = new StringBuilder(bytes.Length + bytes.Length / 48 + 1);
            for (var index = 0; index < bytes.Length; index++)
            {
                if (index > 0 && index % 48 == 0)
                    display.Append('\n');
                display.Append(DisplayGlyphs[bytes[index] & 0x03]);
            }
            return display.ToString();
        }

        public static bool TryParse(
            FileContent content,
            out HosBinaryDescriptor descriptor,
            out string error)
        {
            descriptor = default;
            error = null;

            if (!IsBinary(content))
            {
                error = "missing HOS binary signature";
                return false;
            }

            var bytes = content.CopyBytes();
            if (bytes.Length < HeaderLength + MinimumCiphertextLength + SignatureLength)
            {
                error = "HOS executable is truncated";
                return false;
            }

            if (bytes[Magic.Length] != FormatVersion)
            {
                error = "unsupported HOS executable version";
                return false;
            }

            var lengthOffset = Magic.Length + 1 + IvLength;
            var ciphertextLength = bytes[lengthOffset] | (bytes[lengthOffset + 1] << 8);
            var authenticatedLength = HeaderLength + ciphertextLength;
            if (ciphertextLength < MinimumCiphertextLength ||
                ciphertextLength % MinimumCiphertextLength != 0 ||
                bytes.Length != authenticatedLength + SignatureLength)
            {
                error = "HOS executable has an invalid length";
                return false;
            }

            var authenticated = new byte[authenticatedLength];
            Buffer.BlockCopy(bytes, 0, authenticated, 0, authenticated.Length);
            var expectedSignature = ComputeSignature(authenticated);
            if (!FixedTimeEquals(bytes, authenticatedLength, expectedSignature))
            {
                error = "HOS executable signature is invalid";
                return false;
            }

            var iv = new byte[IvLength];
            Buffer.BlockCopy(bytes, Magic.Length + 1, iv, 0, iv.Length);
            var ciphertext = new byte[ciphertextLength];
            Buffer.BlockCopy(bytes, HeaderLength, ciphertext, 0, ciphertext.Length);

            string programId;
            try
            {
                programId = StrictUtf8.GetString(Decrypt(ciphertext, iv));
            }
            catch (CryptographicException)
            {
                error = "HOS executable payload is invalid";
                return false;
            }
            catch (DecoderFallbackException)
            {
                error = "HOS executable program id is invalid";
                return false;
            }

            if (!IsValidProgramId(programId))
            {
                error = "HOS executable program id is invalid";
                return false;
            }

            descriptor = new HosBinaryDescriptor(programId);
            return true;
        }

        public static bool IsValidProgramId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            for (var index = 0; index < value.Length; index++)
            {
                var character = value[index];
                if ((character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '-' || character == '_' || character == '.')
                {
                    continue;
                }

                return false;
            }

            return true;
        }

        private static byte[] Encrypt(byte[] plaintext, out byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = EncryptionKey;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.GenerateIV();
                iv = (byte[])aes.IV.Clone();
                using (var encryptor = aes.CreateEncryptor())
                    return encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
            }
        }

        private static byte[] Decrypt(byte[] ciphertext, byte[] iv)
        {
            using (var aes = Aes.Create())
            {
                aes.Key = EncryptionKey;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (var decryptor = aes.CreateDecryptor())
                    return decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            }
        }

        private static byte[] ComputeSignature(byte[] data)
        {
            using (var hmac = new HMACSHA256(SignatureKey))
                return hmac.ComputeHash(data);
        }

        private static bool FixedTimeEquals(byte[] value, int offset, byte[] expected)
        {
            if (value.Length - offset != expected.Length)
                return false;

            var difference = 0;
            for (var index = 0; index < expected.Length; index++)
                difference |= value[offset + index] ^ expected[index];
            return difference == 0;
        }

        private static byte[] DeriveKey(string purpose)
        {
            using (var sha = SHA256.Create())
                return sha.ComputeHash(Encoding.UTF8.GetBytes(purpose));
        }
    }
}
