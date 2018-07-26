using System.Security.Cryptography;

namespace SimplePatchToolSecurity
{
	public static class SecurityUtils
	{
		public static void CreateRSAKeyPair( out string publicKey, out string privateKey )
		{
			using( RSA rsa = RSA.Create() )
			{
				publicKey = rsa.ToXmlString( false );
				privateKey = rsa.ToXmlString( true );
			}
		}
	}
}