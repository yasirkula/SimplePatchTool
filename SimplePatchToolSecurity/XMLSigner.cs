using System;
using System.Security.Cryptography;
using System.Security.Cryptography.Xml;
using System.Xml;

namespace SimplePatchToolSecurity
{
	public static class XMLSigner
	{
		// Credit: https://docs.microsoft.com/en-us/dotnet/standard/security/how-to-sign-xml-documents-with-digital-signatures
		public static void SignXMLFile( string xmlPath, string rsaPrivateKey )
		{
			using( RSA rsa = RSA.Create() )
			{
				rsa.FromXmlString( rsaPrivateKey );

				XmlDocument xmlDoc = new XmlDocument() { PreserveWhitespace = true };
				xmlDoc.Load( xmlPath );

				XmlNodeList nodeList = xmlDoc.GetElementsByTagName( "Signature" );
				for( int i = nodeList.Count - 1; i >= 0; i-- )
					xmlDoc.DocumentElement.RemoveChild( nodeList[i] );

				SignedXml signedXml = new SignedXml( xmlDoc ) { SigningKey = rsa };
				Reference reference = new Reference( "" );
				reference.AddTransform( new XmlDsigEnvelopedSignatureTransform() );
				signedXml.AddReference( reference );
				signedXml.ComputeSignature();

				xmlDoc.DocumentElement.AppendChild( xmlDoc.ImportNode( signedXml.GetXml(), true ) );
				xmlDoc.Save( xmlPath );
			}
		}

		public static bool VerifyXMLFile( string xmlPath, string rsaPublicKey )
		{
			XmlDocument xmlDoc = new XmlDocument() { PreserveWhitespace = true };
			xmlDoc.Load( xmlPath );

			return VerifyXMLInternal( xmlDoc, rsaPublicKey );
		}

		public static bool VerifyXMLContents( string xml, string rsaPublicKey )
		{
			XmlDocument xmlDoc = new XmlDocument() { PreserveWhitespace = true };
			xmlDoc.LoadXml( xml );

			return VerifyXMLInternal( xmlDoc, rsaPublicKey );
		}

		// Credit: https://docs.microsoft.com/en-us/dotnet/standard/security/how-to-verify-the-digital-signatures-of-xml-documents
		private static bool VerifyXMLInternal( XmlDocument xmlDoc, string rsaPublicKey )
		{
			using( RSA rsa = RSA.Create() )
			{
				rsa.FromXmlString( rsaPublicKey );

				XmlNodeList nodeList = xmlDoc.GetElementsByTagName( "Signature" );
				if( nodeList.Count != 1 )
					return false;

				try
				{
					SignedXml signedXml = new SignedXml( xmlDoc );
					signedXml.LoadXml( (XmlElement) nodeList[0] );
					return signedXml.CheckSignature( rsa );
				}
				catch( FormatException )
				{
					return false;
				}
				catch( CryptographicException )
				{
					return false;
				}
			}
		}
	}
}