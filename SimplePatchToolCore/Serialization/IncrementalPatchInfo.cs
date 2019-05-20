using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;

namespace SimplePatchToolCore
{
	[XmlRoot( "PatchInfo" )]
	public class IncrementalPatchInfo
	{
		[XmlIgnore]
		public VersionCode FromVersion;

		[XmlIgnore]
		public VersionCode ToVersion;

		[XmlIgnore]
		public string DownloadURL;

		[XmlIgnore]
		public long CompressedFileSize;

		[XmlIgnore]
		public string CompressedMd5Hash;

		public List<PatchRenamedItem> RenamedFiles;
		public List<PatchItem> Files;

		public IncrementalPatchInfo()
		{
			RenamedFiles = new List<PatchRenamedItem>();
			Files = new List<PatchItem>();
		}
	}

	public class PatchRenamedItem
	{
		[XmlAttribute]
		public string BeforePath;

		[XmlAttribute]
		public string AfterPath;

		public PatchRenamedItem()
		{
			BeforePath = "";
			AfterPath = "";
		}
	}

	public class PatchItem
	{
		[XmlAttribute]
		public string Path;

		[XmlAttribute]
		public long BeforeFileSize;

		[XmlAttribute]
		public string BeforeMd5Hash;

		[XmlAttribute]
		public long AfterFileSize;

		[XmlAttribute]
		public string AfterMd5Hash;

		public PatchItem()
		{
			Path = "";
			BeforeFileSize = 0L;
			BeforeMd5Hash = "";
			AfterFileSize = 0L;
			AfterMd5Hash = "";
		}

		public PatchItem( string relativePath, FileInfo before, FileInfo after )
		{
			Path = relativePath;

			if( before != null )
			{
				BeforeFileSize = before.Length;
				BeforeMd5Hash = before.Md5Hash();
			}
			else
			{
				BeforeFileSize = 0L;
				BeforeMd5Hash = "";
			}

			AfterFileSize = after.Length;
			AfterMd5Hash = after.Md5Hash();
		}
	}
}