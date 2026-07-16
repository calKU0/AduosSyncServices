using AduosSyncServices.Contracts.Settings;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace AduosSyncServices.Infrastructure.Clients
{
    public static class GaskaApiAuthHelper
    {
        public static string ComputeSignature(GaskaApiCredentials credentials)
        {
            string body = $"acronym={credentials.Acronym}&person={credentials.Person}&password={credentials.Password}&key={credentials.Key}";
            using var sha = SHA256.Create();
            byte[] bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(body));
            var builder = new StringBuilder();
            foreach (var b in bytes) builder.Append(b.ToString("x2"));
            return builder.ToString();
        }

        public static void ApplyAuthHeaders(HttpClient client, GaskaApiCredentials credentials)
        {
            var basicCredentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credentials.Acronym}|{credentials.Person}:{credentials.Password}"));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basicCredentials);
            client.DefaultRequestHeaders.Remove("X-Signature");
            client.DefaultRequestHeaders.Add("X-Signature", ComputeSignature(credentials));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }
    }
}
