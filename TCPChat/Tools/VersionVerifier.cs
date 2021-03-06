﻿using System;
using System.Security.Cryptography;
using System.Text;
using System.IO;
using System.Linq;

namespace TCPChat.Tools
{
    public static class VersionVerifier
    {
        public static bool Verify(byte[] remoteHash)
        {
            var localHash = GetHash();

            if (localHash.Length != remoteHash.Length) return false;

            return !localHash.Where((t, i) => t != remoteHash[i]).Any();
        }

        public static void PrintHash()
        {
            byte[] Data = GetHash();

            StringBuilder sOutput = new StringBuilder(Data.Length);
            int i;
            for (i = 0; i < Data.Length; i++)
            {
                sOutput.Append(Data[i].ToString("X2"));
            }
            Console.WriteLine(sOutput.ToString());
        }

        public static string GetStringHash()
        {
            byte[] Data = GetHash();

            StringBuilder sOutput = new StringBuilder(Data.Length);
            int i;
            for (i = 0; i < Data.Length; i++)
            {
                sOutput.Append(Data[i].ToString("X2"));
            }

            return sOutput.ToString();
        }

        public static byte[] GetHash()
        {
            using var stream = new FileStream("TCPChat.dll", FileMode.Open, FileAccess.Read, FileShare.Read);
            var hash = new MD5CryptoServiceProvider().ComputeHash(stream);

            return hash;
        }
    }
}