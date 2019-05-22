using System.Security.Cryptography;

namespace Octodiff.Core
{
    public static class SupportedAlgorithms
    {
        public static class Hashing
        {
            public static IHashAlgorithm Sha1()
            {
                return new HashAlgorithmWrapper("SHA1", SHA1.Create());
            }

            public static IHashAlgorithm Default()
            {
                return Sha1();
            }

            public static IHashAlgorithm Create(string algorithm)
            {
                if (algorithm == "SHA1")
                    return Sha1();

                throw new CompatibilityException(
                    $"The hash algorithm '{algorithm}' is not supported in this version of Octodiff");
            }
        }

        public static class Checksum
        {
#pragma warning disable 618
            public static IRollingChecksum Adler32Rolling(bool useV2 = false)
            {
                if (useV2)
                    return new Adler32RollingChecksumV2();

                return new Adler32RollingChecksum();
            }
#pragma warning restore 618

            public static IRollingChecksum Default()
            {
                return Adler32Rolling();
            }

            public static IRollingChecksum Create(string algorithm)
            {
                switch (algorithm)
                {
                    case "Adler32":
                        return Adler32Rolling();
                    case "Adler32V2":
                        return Adler32Rolling(true);
                }
                throw new CompatibilityException(
                    $"The rolling checksum algorithm '{algorithm}' is not supported in this version of Octodiff");
            }
        }
    }
}