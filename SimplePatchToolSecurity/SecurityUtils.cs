using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;

namespace SimplePatchToolSecurity
{
	public static class SecurityUtils
	{
		public const string PROJECT_RSA_PUBLIC_FILENAME = "public.key";
		public const string PROJECT_RSA_PRIVATE_FILENAME = "private.key";

		public static void CreateRSAKeyPair( out string publicKey, out string privateKey )
		{
			using( RSA rsa = RSA.Create() )
			{
				publicKey = rsa.ToXmlString( false );
				privateKey = rsa.ToXmlString( true );
			}
		}

		public static void CreateRSAKeyPairInDirectory( string directory )
		{
			if( !Directory.Exists( directory ) )
				Directory.CreateDirectory( directory );

			string publicKeyContents, privateKeyContents;
			CreateRSAKeyPair( out publicKeyContents, out privateKeyContents );

			string publicKeyPath = Path.Combine( directory, PROJECT_RSA_PUBLIC_FILENAME );
			string privateKeyPath = Path.Combine( directory, PROJECT_RSA_PRIVATE_FILENAME );

			File.WriteAllText( publicKeyPath, publicKeyContents );
			File.WriteAllText( privateKeyPath, privateKeyContents );
		}

		/// <exception cref = "ArgumentException">An argument is empty</exception>
		/// <exception cref = "FileNotFoundException">Private key does not exist</exception>
		public static void SignXMLsWithKeysInDirectory( string[] xmls, string directory )
		{
			if( xmls == null || xmls.Length == 0 )
				throw new ArgumentException( "'xmls' can not be empty!" );

			string privateKeyPath = Path.Combine( directory, PROJECT_RSA_PRIVATE_FILENAME );
			if( !File.Exists( privateKeyPath ) )
				throw new FileNotFoundException( "Key does not exist: " + privateKeyPath );

			string privateKeyContents = File.ReadAllText( privateKeyPath );
			if( string.IsNullOrEmpty( privateKeyContents ) )
				throw new ArgumentException( "Private key can not be empty!" );

			for( int i = 0; i < xmls.Length; i++ )
				XMLSigner.SignXMLFile( xmls[i], privateKeyContents );
		}

		/// <exception cref = "ArgumentException">An argument is empty</exception>
		/// <exception cref = "FileNotFoundException">Public key does not exist</exception>
		public static bool VerifyXMLsWithKeysInDirectory( string[] xmls, string directory, out string[] invalidXmls )
		{
			if( xmls == null || xmls.Length == 0 )
				throw new ArgumentException( "'xmls' can not be empty!" );

			string publicKeyPath = Path.Combine( directory, PROJECT_RSA_PUBLIC_FILENAME );
			if( !File.Exists( publicKeyPath ) )
				throw new FileNotFoundException( "Key does not exist: " + publicKeyPath );

			string publicKeyContents = File.ReadAllText( publicKeyPath );
			if( string.IsNullOrEmpty( publicKeyContents ) )
				throw new ArgumentException( "Public key can not be empty!" );

			List<string> result = new List<string>();
			for( int i = 0; i < xmls.Length; i++ )
			{
				if( !XMLSigner.VerifyXMLFile( xmls[i], publicKeyContents ) )
					result.Add( xmls[i] );
			}

			invalidXmls = result.ToArray();
			return result.Count == 0;
		}
	}
}