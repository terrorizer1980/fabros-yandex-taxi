using System;

namespace NReco.ImageGenerator
{
    /// <summary>
    /// The exception that is thrown when WkHtmlToImage process returns non-zero error exit code
    /// </summary>
    public class WkHtmlToImageException : Exception
    {
        /// <summary>Get WkHtmlToImage process error code</summary>
        public int ErrorCode { get; private set; }

        public WkHtmlToImageException(int errCode, string message)
            : base(message)
            => this.ErrorCode = errCode;

        public WkHtmlToImageException(int errCode, string message, Exception innerEx)
            : base(message, innerEx)
            => this.ErrorCode = errCode;
    }
}