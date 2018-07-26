using System;

namespace Octodiff.Core
{
    public interface IDeltaReader
    {
        byte[] ExpectedHash { get; }
        IHashAlgorithm HashAlgorithm { get; }
        void Apply(
            Action<byte[]> writeData,
            Action<long, long> copy
            );
    }
}