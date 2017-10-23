namespace WebServer
{
    public class RequestHeaders : Headers, IRequestHeaders
    {
        #region Properties	

        public Method Method { get; private set; } = Method.UNDEFINED;
        public string Url { get; private set; }
        public string Host
        {
            get
            {
                if (_host == null)
                {
                    try
                    {
                        _host = headers["host"].Value;
                    }
                    catch { _host = ""; }
                }
                return _host;
            }
        }
        string _host;

        public string Http { get; private set; }

        public bool Http10 { get; private set; }

        public ContentEncoding ContentEncoding
        {
            get
            {
                if (_ContentEncoding == null)
                {
                    _ContentEncoding = ContentEncoding.None;
                    var acceptEncoding = this["accept-encoding"];
                    if (acceptEncoding?.Contains("deflate") ?? false)
                        _ContentEncoding = ContentEncoding.Deflate;
                    else if (acceptEncoding?.Contains("gzip") ?? false)
                        _ContentEncoding = ContentEncoding.GZip;
                }
                return _ContentEncoding ?? ContentEncoding.None;
            }
        }
        ContentEncoding? _ContentEncoding;

        #endregion

        #region Methods	

        public void ChangeUrl(string newUrl)
        {
            Url = newUrl;
        }

        protected override void InternalInitialize(string[] headerParts)
        {
            if (headerParts[0].StartsWith("GET"))
                Method = Method.GET;
            else if (headerParts[0].StartsWith("POST"))
                Method = Method.POST;
            else if (headerParts[0].StartsWith("HEAD"))
                Method = Method.HEAD;
            int start = headerParts[0].IndexOf(' ') + 1;
            Url = headerParts[0].Substring(start, headerParts[0].IndexOf(" HTTP") - start);
            start = headerParts[0].IndexOf(' ', start) + 1;
            Http = headerParts[0].Substring(start);
            Http10 = string.Compare(Http, "http/1.0", true) == 0;
        }

        #endregion
    }
}
