﻿using System;
using System.Reflection;
using System.Security.Cryptography;

namespace PerplexMail.SecurityDrivenDotNet
{
    internal class HMAC2 : HMAC
	{
		public HMAC2(Func<HashAlgorithm> hashFactory)
		{
			var h1 = hashFactory();
			var h2 = hashFactory();

			m_hash1(this, h1);
			m_hash2(this, h2);

			this.HashSizeValue = h1.HashSize;
			this.BlockSizeValue = h1.InputBlockSize;
			if (this.BlockSizeValue == 1) // i.e. virtual "InputBlockSize" was not properly overridden & set
			{
				if (h1 is SHA512 || h1 is SHA384)
					this.BlockSizeValue = 128;
				else if (h1 is SHA256 || h1 is SHA1 || h1 is MD5 || h1 is RIPEMD160)
					this.BlockSizeValue = 64;
			}
		}

		public HMAC2(Func<HashAlgorithm> hashFactory, byte[] key) : this(hashFactory) { this.Key = key; }
		public override int InputBlockSize { get { return this.BlockSizeValue; } }
		public override int OutputBlockSize { get { return this.HashSize / 8; } }

		static readonly Type thisType = typeof(HMAC2);
		static readonly Action<HMAC2, HashAlgorithm> m_hash1 = thisType.GetField("m_hash1", BindingFlags.NonPublic | BindingFlags.Instance).CreateSetter<HMAC2, HashAlgorithm>();
		static readonly Action<HMAC2, HashAlgorithm> m_hash2 = thisType.GetField("m_hash2", BindingFlags.NonPublic | BindingFlags.Instance).CreateSetter<HMAC2, HashAlgorithm>();
	}// HMAC2 class
}//ns