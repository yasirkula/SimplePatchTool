using System;

namespace Octodiff.Core
{
    public class CorruptFileFormatException : Exception
    {
        public CorruptFileFormatException(string message) : base(message)
        {
        }
    }
}