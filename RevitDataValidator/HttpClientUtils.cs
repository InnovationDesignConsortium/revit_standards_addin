using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace RevitDataValidator
{
    public static class HttpClientUtils
    {
        public static async Task DownloadFileTaskAsync(this HttpClient client, Uri uri, string FileName)
        {
            try
            {
                using (var s = await client.GetStreamAsync(uri))
                {
                    using (var fs = new FileStream(FileName, FileMode.CreateNew))
                    {
                        await s.CopyToAsync(fs);
                    }
                }
            }
            catch (Exception ex)
            {
                Utils.LogException("DownloadFileTaskAsync", ex);
            }
        }
    }
}