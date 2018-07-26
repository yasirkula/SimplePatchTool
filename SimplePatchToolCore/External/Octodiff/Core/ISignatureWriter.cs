namespace Octodiff.Core
{
    public interface ISignatureWriter
    {
        void WriteMetadata(IHashAlgorithm hashAlgorithm, IRollingChecksum rollingChecksumAlgorithm);
        void WriteChunk(ChunkSignature signature);
    }
}