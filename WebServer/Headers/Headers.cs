using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WebServer
{
    /// <summary>
    /// Alle HTTP-Header, der Zugriff erfolgt case-insensitiv, denn die HTTP-Header sind nach RFC 7230 bzw. RFC 2616 case-insensitiv
    /// Über das Property <see cref="Raw"/> kann auf die Original-HTTP-Header-Schlüssel zugegriffen werden
    /// </summary>
    public abstract class Headers
    {
        #region Structs	

        public struct Result
        {
            public Result(int bufferEndPosition, int bufferReadCount)
            {
                BufferEndPosition = bufferEndPosition;
                BufferReadCount = bufferReadCount;
            }

            /// <summary>
            /// Die Position im Buffer, an der die Headerdaten aufhören und die eigentlichen Daten anfangen, unmittelbar nach Einlesen der Header aus dem Netzwerkstrom
            /// </summary>
            public int BufferEndPosition { get; private set; }
            /// <summary>
            /// Anzahl bereits einglesener Bytes im Buffer, unmittelbar nach Einlesen der Header aus dem Netzwerkstrom
            /// </summary>
            public int BufferReadCount { get; private set; }
        }

        #endregion

        #region Properties	

        public string ContentType
        {
            get
            {
                if (_ContentType == null)
                {
                    KeyValuePair<string, string> kvp;
                    if (headers.TryGetValue("content-type", out kvp))
                        _ContentType = kvp.Value;
                    else
                        _ContentType = "";
                }
                return _ContentType;
            }
        }
        string _ContentType;

        /// <summary>
        /// Zugriff auf einen Headerwert. 
        /// </summary>
        /// <param name="key">Der Schlüssel für den Zugriff auf den Header-Wert. Der Schlüssel ist case insensitiv</param>
        /// <returns>Der Wert des Headers, oder null, wenn er nicht vorhanden ist</returns>
        public string this[string key]
        {
            get
            {
                KeyValuePair<string, string> kvp;
                if (!headers.TryGetValue(key, out kvp))
                    return null;
                return kvp.Value ?? ""; // "": Unterscheidung zu key not found (null)
            }
        }

        public IEnumerable<KeyValuePair<string, KeyValuePair<string, string>>> Raw
        {
            get
            {
                return headers.AsEnumerable<KeyValuePair<string, KeyValuePair<string, string>>>();
            }
        }

        #endregion

        #region Methods	

        /// <summary>
        /// Einlesen der Header aus dem Netzwerkstrom. 
        /// </summary>
        /// <param name="networkStream">Der Netzwerkstrom, der die Headers enthält</param>
        /// <param name="buffer">Bereits eingelesene Daten aus dem Netzwerkstrom</param>
        /// <param name="recentbufferPosition">Die momentane Position im Netzwerkstrom</param>
        /// <param name="tracing"></param>
        /// <param name="sessionID"></param>
        public Result Initialize(Stream networkStream, byte[] buffer, int recentbufferPosition)
        {
            Result result;
            string headerstring = ReadHeaderFromStream(networkStream, buffer, recentbufferPosition, out result);
            string[] headerParts = headerstring.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            InternalInitialize(headerParts);

            try
            {
                var keyValues = headerParts.Skip(1).Select(s => new KeyValuePair<string, string>(s.Substring(0, s.IndexOf(": ")), s.Substring(s.IndexOf(": ") + 2)));
                foreach (var keyValue in keyValues)
                    headers[keyValue.Key] = keyValue;
            }
            catch (Exception)
            {
                headers.Clear();
                var keyValues = headerParts.Skip(1).Select(s =>
                {
                    try
                    {
                        return new KeyValuePair<string, string>(s.Substring(0, s.IndexOf(": ")), s.Substring(s.IndexOf(": ") + 2));
                    }
                    catch { return new KeyValuePair<string, string>("_OBSOLETE_", ""); }
                }).Where(n => n.Key != "_OBSOLETE_");
                foreach (var keyValue in keyValues)
                    headers[keyValue.Key] = keyValue;
            }

            return result;
        }

        protected abstract void InternalInitialize(string[] headerParts);

        string ReadHeaderFromStream(Stream networkStream, byte[] buffer, int recentbufferPosition, out Result result)
        {
            int index = 0;
            int read = recentbufferPosition;
            while (true)
            {
                for (int i = index; i < Math.Min(read + index, buffer.Length); i++)
                {
                    if (i > 4 && buffer[i] == '\n' && buffer[i - 1] == '\r' && buffer[i - 2] == '\n')
                    {
                        result = new Result(i + 1, index + read);
                        return Encoding.ASCII.GetString(buffer, 0, i - 1);
                    }
                }
                index += read;
                read = networkStream.Read(buffer, index, buffer.Length - index);
                if (read == 0)
                    throw new CloseException();
            }
        }

        #endregion

        #region Fields	

        protected Dictionary<string, KeyValuePair<string, string>> headers = new Dictionary<string, KeyValuePair<string, string>>(StringComparer.OrdinalIgnoreCase);

        #endregion
    }
}
