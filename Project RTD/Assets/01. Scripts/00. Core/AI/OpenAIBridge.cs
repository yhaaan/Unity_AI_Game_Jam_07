using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace RTD.AI
{
    public sealed class OpenAIBridge : MonoBehaviour
    {
        [SerializeField]
        private string endpoint = "http://127.0.0.1:8787/v1/game-response";

        [SerializeField, Min(1)]
        private int timeoutSeconds = 30;

        public IEnumerator Request(
            string input,
            Action<string> onSuccess,
            Action<string> onError)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                onError?.Invoke("OpenAI request input is empty.");
                yield break;
            }

            var requestBody = JsonUtility.ToJson(new ProxyRequest { input = input });
            using var request = new UnityWebRequest(endpoint, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(requestBody)),
                downloadHandler = new DownloadHandlerBuffer(),
                timeout = timeoutSeconds,
            };
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                var proxyError = TryReadError(request.downloadHandler.text);
                onError?.Invoke(string.IsNullOrWhiteSpace(proxyError)
                    ? request.error
                    : proxyError);
                yield break;
            }

            var response = JsonUtility.FromJson<ProxyResponse>(request.downloadHandler.text);
            if (response == null || string.IsNullOrWhiteSpace(response.text))
            {
                onError?.Invoke("OpenAI proxy returned no text.");
                yield break;
            }

            onSuccess?.Invoke(response.text);
        }

        private static string TryReadError(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return string.Empty;
            }

            try
            {
                return JsonUtility.FromJson<ProxyError>(json)?.error ?? string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
        }

        [Serializable]
        private sealed class ProxyRequest
        {
            public string input;
        }

        [Serializable]
        private sealed class ProxyResponse
        {
            public string text;
        }

        [Serializable]
        private sealed class ProxyError
        {
            public string error;
        }
    }
}

