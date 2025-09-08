using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Net
{
    public struct HttpResponse
    {
        public long statusCode;
        public string body;
        public UnityWebRequest.Result result;
        public string error;
    }

    public static class HttpRequest
    {
        /// <summary>
        /// Send an HTTP(S) request.
        /// method: "GET", "POST", "PATCH", "PUT", "DELETE"
        /// url: full URL to send request to
        /// onComplete: callback with HttpResponse (statusCode, body, result, error)
        /// bodyJson: optional JSON string to send as request body
        /// headers: optional headers to add (e.g., Authorization, Content-Type)
        /// cert: optional certificate handler
        /// timeoutSeconds: request timeout in seconds (default 10)
        /// </summary>
        public static IEnumerator Send(
            string method,
            string url,
            Action<HttpResponse> onComplete,
            string bodyJson = null,
            Dictionary<string, string> headers = null,
            CertificateHandler cert = null,
            int timeoutSeconds = 10
        )
        {
            using (var request = new UnityWebRequest(url, method))
            {
                // Setup download handler
                request.downloadHandler = new DownloadHandlerBuffer();

                // Setup upload handler if we have a body
                if (!string.IsNullOrEmpty(bodyJson))
                {
                    byte[] bodyRaw = Encoding.UTF8.GetBytes(bodyJson);
                    request.uploadHandler = new UploadHandlerRaw(bodyRaw);
                    // Default content type to JSON if not provided
                    request.SetRequestHeader("Content-Type", "application/json");
                }

                // Apply custom headers (will override Content-Type if provided)
                if (headers != null)
                {
                    foreach (var kv in headers)
                    {
                        if (!string.IsNullOrEmpty(kv.Key))
                        {
                            request.SetRequestHeader(kv.Key, kv.Value ?? string.Empty);
                        }
                    }
                }

                // Certificate handler and timeout
                if (cert != null)
                {
                    request.certificateHandler = cert;
                }
                request.timeout = Mathf.Max(1, timeoutSeconds);

                // Send
                yield return request.SendWebRequest();

                var resp = new HttpResponse
                {
                    statusCode = request.responseCode,
                    body = request.downloadHandler != null ? request.downloadHandler.text : null,
                    result = request.result,
                    error = request.error,
                };

                onComplete?.Invoke(resp);
            }
        }
    }
}
