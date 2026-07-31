using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace HOS.Domain.Identity
{
    public sealed class CredentialStore
    {
        private readonly Dictionary<UserId, PasswordRecord> passwords = new Dictionary<UserId, PasswordRecord>();

        public void SetPassword(UserId userId, string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));
            var salt = Guid.NewGuid().ToByteArray();
            passwords[userId] = new PasswordRecord(salt, Hash(salt, password));
        }

        public bool VerifyPassword(UserId userId, string password)
        {
            if (password == null || !passwords.TryGetValue(userId, out var record)) return false;
            var candidate = Hash(record.Salt, password);
            var difference = 0;
            for (var i = 0; i < candidate.Length; i++) difference |= candidate[i] ^ record.Hash[i];
            return difference == 0;
        }

        private static byte[] Hash(byte[] salt, string password)
        {
            using (var sha = SHA256.Create())
            {
                var bytes = Encoding.UTF8.GetBytes(password);
                var input = new byte[salt.Length + bytes.Length];
                Buffer.BlockCopy(salt, 0, input, 0, salt.Length);
                Buffer.BlockCopy(bytes, 0, input, salt.Length, bytes.Length);
                return sha.ComputeHash(input);
            }
        }

        private sealed class PasswordRecord
        {
            public PasswordRecord(byte[] salt, byte[] hash) { Salt = salt; Hash = hash; }
            public byte[] Salt { get; }
            public byte[] Hash { get; }
        }
    }
}
