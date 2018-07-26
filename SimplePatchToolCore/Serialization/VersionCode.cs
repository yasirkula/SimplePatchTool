using System;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

namespace SimplePatchToolCore
{
	public struct VersionCode : IXmlSerializable, IComparable<VersionCode>
	{
		private int[] parts;

		public bool IsValid { get { return parts != null && parts.Length > 0; } }
		public int Length { get { return parts.Length; } }
		public int this[int index] { get { return parts[index]; } }

		public VersionCode( params int[] parts )
		{
			if( parts != null )
			{
				for( int i = 0; i < parts.Length; i++ )
				{
					if( parts[i] < 0 )
					{
						this.parts = null;
						return;
					}
				}
			}

			this.parts = parts;
		}

		public VersionCode( string versionStr )
		{
			parts = ParseVersionString( versionStr );
		}

		public XmlSchema GetSchema()
		{
			return null;
		}

		public void ReadXml( XmlReader reader )
		{
			parts = ParseVersionString( reader.ReadString() );
			reader.Read(); // Read the closing tag
		}

		public void WriteXml( XmlWriter writer )
		{
			writer.WriteString( ToString() );
		}

		public int CompareTo( VersionCode other )
		{
			return CompareVersions( this, other );
		}

		public override int GetHashCode()
		{
			if( !IsValid )
				return 0;

			return parts.GetHashCode();
		}

		public override string ToString()
		{
			if( !IsValid )
				return string.Empty;
			else
			{
				StringBuilder versionStr = new StringBuilder( parts.Length * 3 ).Append( parts[0] );
				for( int i = 1; i < parts.Length; i++ )
					versionStr.Append( '.' ).Append( parts[i] );

				return versionStr.ToString();
			}
		}

		public override bool Equals( object obj )
		{
			if( obj is VersionCode )
				return CompareVersions( this, (VersionCode) obj, -1 ) == 0;

			return false;
		}

		public static implicit operator VersionCode( string versionStr )
		{
			return new VersionCode( versionStr );
		}

		public static implicit operator string( VersionCode version )
		{
			return version.ToString();
		}

		public static bool operator ==( VersionCode v1, VersionCode v2 )
		{
			return CompareVersions( v1, v2, -1 ) == 0;
		}

		public static bool operator !=( VersionCode v1, VersionCode v2 )
		{
			return CompareVersions( v1, v2 ) != 0;
		}

		public static bool operator <( VersionCode v1, VersionCode v2 )
		{
			return CompareVersions( v1, v2 ) < 0;
		}

		public static bool operator >( VersionCode v1, VersionCode v2 )
		{
			return CompareVersions( v1, v2 ) > 0;
		}

		public static bool operator <=( VersionCode v1, VersionCode v2 )
		{
			return CompareVersions( v1, v2, 1 ) <= 0;
		}

		public static bool operator >=( VersionCode v1, VersionCode v2 )
		{
			return CompareVersions( v1, v2, -1 ) >= 0;
		}

		private static int[] ParseVersionString( string versionStr )
		{
			if( versionStr == null )
				return null;

			versionStr = versionStr.Trim();
			if( versionStr.Length == 0 )
				return null;

			int i, numberOfParts = 1;
			for( i = 0; i < versionStr.Length; i++ )
			{
				if( versionStr[i] == '.' )
					numberOfParts++;
			}

			int[] parts = new int[numberOfParts];
			int partIndex = 0, partValue = 0;
			for( i = 0; i < versionStr.Length; i++ )
			{
				char ch = versionStr[i];
				if( ch == '.' )
				{
					parts[partIndex++] = partValue;
					partValue = 0;
				}
				else if( ch >= '0' && ch <= '9' )
					partValue = partValue * 10 + ( ch - '0' );
				else
					return null;
			}

			parts[partIndex] = partValue;
			return parts;
		}

		private static int CompareVersions( VersionCode v1, VersionCode v2, int invalidValue = 0 )
		{
			if( !v1.IsValid || !v2.IsValid )
				return !v1.IsValid && !v2.IsValid ? 0 : invalidValue;

			int i;
			for( i = 0; i < v1.parts.Length && i < v2.parts.Length; i++ )
			{
				int comparison = v1.parts[i] - v2.parts[i];
				if( comparison != 0 )
					return comparison;
			}

			for( ; i < v1.parts.Length; i++ )
			{
				if( v1.parts[i] > 0 )
					return 1;
			}

			for( ; i < v2.parts.Length; i++ )
			{
				if( v2.parts[i] > 0 )
					return -1;
			}

			return 0;
		}
	}
}