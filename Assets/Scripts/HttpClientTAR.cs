using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace _Scripts
{
    public static class HttpClientTAR
    {
        public static async Task<T> Get<T>(string endpoint)
        {
            var getRequest = CreateRequest(endpoint);
            _ = getRequest.SendWebRequest();

            while (!getRequest.isDone)
                await Task.Delay(10);
            return JsonUtility.FromJson<T>(getRequest.downloadHandler.text);
        }

        public static async Task<string> Post(string endpoint, object payload)
        {
            // Debug.Log($"Posting to {endpoint} with payload: {JsonUtility.ToJson(payload, true)}");
            var postRequest = CreateRequest(endpoint, RequestType.POST, payload);
            _ = postRequest.SendWebRequest();

            // Debug.Log("Postrequest: " + postRequest);

            while (!postRequest.isDone)
                await Task.Delay(10);
            string responseText = postRequest.downloadHandler.text;
            // Debug.Log("Post response: " + responseText);
            return responseText;
        }

        private static UnityWebRequest CreateRequest(
            string path,
            RequestType type = RequestType.GET,
            object data = null
        )
        {
            var request = new UnityWebRequest(path, type.ToString());
            // Debug.Log($"Creating request to {path} with type {type}");

            if (data != null)
            {
                var bodyRaw = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
                // Debug.Log($"Request body: {JsonUtility.ToJson(data)}");
                request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            }

            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            // Debug.Log("Request: " + request);

            return request;
        }

        private static void AttachHeader(UnityWebRequest request, string key, string value)
        {
            request.SetRequestHeader(key, value);
        }
    }

    public enum RequestType
    {
        GET = 0,
        POST = 1,
        PUT = 2,
    }
}
