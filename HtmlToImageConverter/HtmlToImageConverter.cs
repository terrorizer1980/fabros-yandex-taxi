using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NReco.ImageGenerator
{
  /// <summary>
  /// Html to Image converter (wrapper for WkHtmlToImage command line tool)
  /// </summary>
  public class HtmlToImageConverter
  {
    private static object globalObj = new object();
    private static string[] ignoreWkHtmlErrLines = new string[6]
    {
      "Exit with code 1 due to network error: ContentNotFoundError",
      "QFont::setPixelSize: Pixel size <= 0",
      "Exit with code 1 due to network error: ProtocolUnknownError",
      "Exit with code 1 due to network error: HostNotFoundError",
      "Exit with code 1 due to network error: ContentOperationNotPermittedError",
      "Exit with code 1 due to network error: UnknownContentError"
    };

    /// <summary>Get or set path where WkHtmlToImage tool is located</summary>
    /// <remarks>
    /// By default this property initialized with application assemblies folder.
    /// If WkHtmlToImage tool file is not present it is extracted automatically from DLL resources.
    /// </remarks>
    public string ToolPath { get; set; }

    /// <summary>
    /// Get or set WkHtmlToImage tool executive file name ('wkhtmltoimage.exe' by default)
    /// </summary>
    public string WkHtmlToImageExeName { get; set; }

    /// <summary>Get or set zoom factor</summary>
    public float Zoom { get; set; }

    /// <summary>Get or set minimum image width</summary>
    public int Width { get; set; }

    /// <summary>
    /// Get or set minimum image height (default 0: in this case height is calculated automatically)
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Get or set custom WkHtmlToImage command line arguments
    /// </summary>
    public string CustomArgs { get; set; }

    /// <summary>
    /// Get or set WkHtmlToImage process priority (Normal by default)
    /// </summary>
    public ProcessPriorityClass ProcessPriority { get; set; }

    /// <summary>
    /// Get or set maximum execution time for running WkHtmlToImage process (null is by default = no timeout)
    /// </summary>
    public TimeSpan? ExecutionTimeout { get; set; }

    /// <summary>
    /// Occurs when log line is received from WkHtmlToImage process.
    /// </summary>
    /// <remarks>
    /// Quiet mode should be disabled if you want to get wkhtmltopdf info/debug messages
    /// </remarks>
    public event EventHandler<DataReceivedEventArgs> LogReceived;

    /// <summary>
    /// Create new instance of <see cref="T:NReco.ImageGenerator.HtmlToImageConverter" />.
    /// </summary>
    public HtmlToImageConverter()
    {
      ToolPath = Directory.GetCurrentDirectory();
      WkHtmlToImageExeName = "wkhtmltoimage.exe";
      ProcessPriority = ProcessPriorityClass.Normal;
      Zoom = 1f;
      Width = 0;
      Height = 0;
    }

    private void EnsureWkHtmlToImage()
    {
    }

    /// <summary>Generate image by specifed HTML content</summary>
    /// <param name="htmlContent">HTML content</param>
    /// <param name="imageFormat">resulting image format (see <seealso cref="T:NReco.ImageGenerator.ImageFormat" />)</param>
    /// <returns>image bytes</returns>
    public byte[] GenerateImage(string htmlContent, string imageFormat)
    {
      var memoryStream = new MemoryStream();
      GenerateImage(htmlContent, imageFormat, memoryStream);
      return memoryStream.ToArray();
    }

    /// <summary>
    /// Generate image by specfied HTML content and write output into output stream
    /// </summary>
    /// <param name="htmlContent">HTML document</param>
    /// <param name="imageFormat">resulting image format (see <seealso cref="T:NReco.ImageGenerator.ImageFormat" />)</param>
    /// <param name="outputStream">output stream for resulting image</param>
    public void GenerateImage(string htmlContent, string imageFormat, Stream outputStream)
    {
      if (htmlContent == null)
        throw new ArgumentNullException(nameof (htmlContent));
      if (imageFormat == null)
        throw new ArgumentNullException(nameof (imageFormat));
      GenerateImageInternal("-", Encoding.UTF8.GetBytes(htmlContent), "-", outputStream, imageFormat);
    }

    /// <summary>Generate image for specified HTML file path or URL</summary>
    /// <param name="htmlFilePath">path to HTML file or absolute URL</param>
    /// <param name="imageFormat">resulting image format (see <seealso cref="T:NReco.ImageGenerator.ImageFormat" />)</param>
    /// <returns>image bytes</returns>
    public byte[] GenerateImageFromFile(string htmlFilePath, string imageFormat)
    {
      if (imageFormat == null)
        throw new ArgumentNullException(nameof (imageFormat));
      var memoryStream = new MemoryStream();
      GenerateImageInternal(htmlFilePath, null, "-", memoryStream, imageFormat);
      return memoryStream.ToArray();
    }

    /// <summary>
    /// Generate image for specified HTML file or URL and write resulting image to output stream
    /// </summary>
    /// <param name="htmlFilePath">path to HTML file or absolute URL</param>
    /// <param name="imageFormat">resulting image format (see <seealso cref="T:NReco.ImageGenerator.ImageFormat" />)</param>
    /// <param name="outputStream">output stream for resulting image</param>
    public void GenerateImageFromFile(string htmlFilePath, string imageFormat, Stream outputStream)
    {
      if (imageFormat == null)
        throw new ArgumentNullException(nameof (imageFormat));
      GenerateImageInternal(htmlFilePath, null, "-", outputStream, imageFormat);
    }

    /// <summary>
    /// Generate image for specified HTML file or URL and write resulting image to output file
    /// </summary>
    /// <param name="htmlFilePath">path to HTML file or absolute URL</param>
    /// <param name="imageFormat">resulting image format (see <seealso cref="T:NReco.ImageGenerator.ImageFormat" />). If imageFormat=null format is suggested by output file extension.</param>
    /// <param name="outputPdfFilePath">path to output image file</param>
    public void GenerateImageFromFile(
      string htmlFilePath,
      string imageFormat,
      string outputImageFilePath)
    {
      if (File.Exists(outputImageFilePath))
        File.Delete(outputImageFilePath);
      GenerateImageInternal(htmlFilePath, null, outputImageFilePath, null, imageFormat);
    }

    private void GenerateImageInternal(
      string htmlFilePath,
      byte[] inputBytes,
      string outputImgFilePath,
      Stream outputStream,
      string imageFormat)
    {
      EnsureWkHtmlToImage();
      var asyncStreamCopyTo = (AsyncStreamCopyTo) null;
      try
      {
        var str = Path.Combine(ToolPath, WkHtmlToImageExeName);
        if (!File.Exists(str))
          throw new FileNotFoundException("Cannot find WkHtmlToImage: " + str);
        var stringBuilder = new StringBuilder();
        stringBuilder.Append(" -q ");
        if (Zoom != 1.0)
          stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " --zoom {0} ", Zoom);
        if (Width > 0)
          stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " --width {0} ", Width);
        if (Height > 0)
          stringBuilder.AppendFormat(CultureInfo.InvariantCulture, " --height {0} ", Height);
        if (!string.IsNullOrEmpty(imageFormat))
          stringBuilder.AppendFormat(" -f {0} ", imageFormat);
        if (!string.IsNullOrEmpty(CustomArgs))
          stringBuilder.AppendFormat(" {0} ", CustomArgs);
        stringBuilder.AppendFormat(" \"{0}\" ", htmlFilePath);
        stringBuilder.AppendFormat(" \"{0}\" ", outputImgFilePath);
        var proc = Process.Start(new ProcessStartInfo(str, stringBuilder.ToString())
        {
          WindowStyle = ProcessWindowStyle.Hidden,
          CreateNoWindow = true,
          UseShellExecute = false,
          WorkingDirectory = Path.GetDirectoryName(ToolPath),
          RedirectStandardInput = inputBytes != null,
          RedirectStandardOutput = outputStream != null,
          RedirectStandardError = true
        });
        if (ProcessPriority != ProcessPriorityClass.Normal)
          proc.PriorityClass = ProcessPriority;
        var lastErrorLine = string.Empty;
        proc.ErrorDataReceived += (DataReceivedEventHandler) ((o, args) =>
        {
          if (args.Data == null)
            return;
          if (!string.IsNullOrEmpty(args.Data))
            lastErrorLine = args.Data;
          if (LogReceived == null)
            return;
          LogReceived(this, args);
        });
        proc.BeginErrorReadLine();
        if (inputBytes != null)
        {
          proc.StandardInput.BaseStream.Write(inputBytes, 0, inputBytes.Length);
          proc.StandardInput.BaseStream.Flush();
          proc.StandardInput.Close();
        }
        long num = 0;
        if (outputStream != null)
          asyncStreamCopyTo = new AsyncStreamCopyTo(proc.StandardOutput.BaseStream, outputStream);
        WaitProcessForExit(proc);
        if (outputStream == null)
        {
          if (File.Exists(outputImgFilePath))
            num = new FileInfo(outputImgFilePath).Length;
        }
        else
        {
          asyncStreamCopyTo.Wait(ExecutionTimeout);
          asyncStreamCopyTo.Dispose();
          num = asyncStreamCopyTo.TotalRead;
        }
        CheckExitCode(proc.ExitCode, lastErrorLine, num > 0L);
        proc.Close();
      }
      catch (Exception ex)
      {
        asyncStreamCopyTo?.Dispose();
        throw new Exception("Image generation failed: " + ex.Message, ex);
      }
    }

    private void WaitProcessForExit(Process proc)
    {
      if (ExecutionTimeout.HasValue)
      {
        if (!proc.WaitForExit((int) ExecutionTimeout.Value.TotalMilliseconds))
        {
          if (!proc.HasExited)
          {
            try
            {
              proc.Kill();
              proc.Close();
            }
            catch (Exception ex)
            {
            }
          }
          throw new WkHtmlToImageException(-2, string.Format("WkHtmlToImage process exceeded execution timeout ({0}) and was aborted", ExecutionTimeout));
        }
      }
      else
        proc.WaitForExit();
    }

    private void CheckExitCode(int exitCode, string lastErrLine, bool outputNotEmpty)
    {
      int num1;
      switch (exitCode)
      {
        case 0:
          return;
        case 1:
          num1 = Array.IndexOf(ignoreWkHtmlErrLines, lastErrLine.Trim()) >= 0 ? 1 : 0;
          break;
        default:
          num1 = 0;
          break;
      }
      var num2 = outputNotEmpty ? 1 : 0;
      if ((num1 & num2) == 0)
        throw new WkHtmlToImageException(exitCode, lastErrLine);
    }

    internal class AsyncStreamCopyTo : IDisposable
    {
      private Stream FromStream;
      private Stream ToStream;
      private CancellationTokenSource cancelTokenSource;
      private Task copyToTask;

      public int TotalRead { get; }

      internal AsyncStreamCopyTo(Stream fromStream, Stream toStream)
      {
        FromStream = fromStream;
        ToStream = toStream;
        TotalRead = 0;
        var bufferSize = 32768;
        cancelTokenSource = new CancellationTokenSource();
        copyToTask = FromStream.CopyToAsync(toStream, bufferSize, cancelTokenSource.Token);
      }

      public void Dispose()
      {
        if (FromStream != null)
        {
          FromStream.Dispose();
          FromStream = null;
        }
        if (cancelTokenSource == null)
          return;
        cancelTokenSource.Cancel(true);
        cancelTokenSource = null;
        copyToTask = null;
      }

      public void Wait(TimeSpan? timeout)
      {
        if (timeout.HasValue)
        {
          if (!copyToTask.Wait(timeout.Value))
            throw new WkHtmlToImageException(-2, string.Format("WkHtmlToImage output read operation exceedes the timeout ({0}) and was aborted", timeout));
        }
        else
          copyToTask.Wait();
      }
    }
  }
}
