using System;

namespace Octodiff.Core
{
    public class CompatibilityException : Exception
    {
        public CompatibilityException(string message) : base(message)
        {
            
        }
    }
}