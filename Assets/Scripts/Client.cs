using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using TMPro;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

class MyHTTPClient : MonoBehaviour
{
    public static MyHTTPClient m_Instance;

    [SerializeField]
    private string m_Hostname = "localhost";
    [SerializeField]
    private int m_Port = 443;
    [SerializeField]
    private string m_HttpVersion = "HTTP/1.1";

    private string m_Method = "";
    private string m_URL = "";
    private string m_Headers = "";
    private string m_Body = "";

    [SerializeField]
    private TMP_Dropdown m_MethodDropdown;
    [SerializeField]
    private TMP_InputField m_URLInputField;
    [SerializeField]
    private TMP_InputField m_HeadersInputField;
    [SerializeField]
    private TMP_InputField m_BodyInputField;

    private int m_UserID = -1;
    private string m_AuthenticationKey = "";

    private void Awake()
    {
        if (m_Instance == null)
        {
            m_Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (m_MethodDropdown != null)
            SetMethod();
    }

    public void SetMethod()
    {
        m_Method = m_MethodDropdown.options[m_MethodDropdown.value].text;
    }

    public void SetURL()
    {
        m_URL = m_URLInputField.text;
    }

    public void SetHeaders()
    {
        m_Headers = m_HeadersInputField.text;
    }

    public void SetBody()
    {
        m_Body = m_BodyInputField.text;
    }

    public void SendRequest()
    {
        RequestToServer();
    }

    public void RequestToServer()
    {
        string requestMethod = "POST";
        if (m_Method != "")
            requestMethod = m_Method;

        string url = "/newValue";
        if (m_URL != "")
            url = m_URL;


        string headers = "";
        if (m_Headers != "")
            headers = m_Headers + "\r\n";

        string body = m_Body;
        
        SendRequestToServer(requestMethod, url, headers, body:body);
    }

    public static void SetUserID(int value) { m_Instance.m_UserID = value; }
    public static void SetAuthenticationKey(string value) { m_Instance.m_AuthenticationKey = value; }

    public static ParsedHTTPResponse SendRequestToServer(string requestMethod, string url, string headers = "", string contentType = "text/plain", string body = "")
    {
        if (m_Instance.m_UserID > 0 && m_Instance.m_AuthenticationKey.Length == 6)
            headers = $"userID:{m_Instance.m_UserID}\r\nauthenticationKey:{m_Instance.m_AuthenticationKey}\r\n" + headers;

        string request = $"{requestMethod} {url} {m_Instance.m_HttpVersion}\r\nHost: {m_Instance.m_Hostname}\r\nConnection: close\r\nContent-Type: {contentType}\r\nContent-Length: {body.Length}\r\n{headers}\r\n{body}";

        Debug.Log(request);

        try
        {
            using (TcpClient client = new TcpClient(m_Instance.m_Hostname, m_Instance.m_Port))
            using (SslStream sslStream = new SslStream(client.GetStream(), false, ValidateServerCertificate))
            {
                sslStream.AuthenticateAsClient(m_Instance.m_Hostname);

                byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                sslStream.Write(requestBytes, 0, requestBytes.Length);

                byte[] buffer = new byte[4096];
                int bytesRead;

                using (MemoryStream responseStream = new MemoryStream())
                {
                    while ((bytesRead = sslStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        responseStream.Write(buffer, 0, bytesRead);
                    }

                    byte[] response = responseStream.ToArray();

                    return Parse(response);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }

        return null;
    }

    private static int FindSequence(byte[] source, byte[] sequence)
    {
        for (int i = 0; i <= source.Length - sequence.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < sequence.Length; j++)
            {
                if (source[i + j] != sequence[j])
                {
                    match = false;
                    break;
                }
            }
            if (match) return i;
        }
        return -1;
    }

    public static ParsedHTTPResponse Parse(byte[] rawResponseBytes)
    {
        byte[] delimiter = new byte[] { 13, 10, 13, 10 }; // \r\n\r\n
        int delimiterIndex = FindSequence(rawResponseBytes, delimiter);

        if (delimiterIndex == -1)
            throw new ArgumentException("Invalid HTTP response - no header-body delimiter");

        // Split headers and body
        byte[] headerBytes = new byte[delimiterIndex];
        Array.Copy(rawResponseBytes, headerBytes, delimiterIndex);
        byte[] bodyBytes = new byte[rawResponseBytes.Length - delimiterIndex - delimiter.Length];
        Array.Copy(rawResponseBytes, delimiterIndex + delimiter.Length, bodyBytes, 0, bodyBytes.Length);

        // Parse headers
        string headerSection = Encoding.ASCII.GetString(headerBytes);
        string[] headerLines = headerSection.Split(new[] { "\r\n" }, StringSplitOptions.None);

        if (headerLines.Length == 0)
            throw new ArgumentException("Invalid HTTP response - no headers found");

        // Parse status line
        string[] statusParts = headerLines[0].Split(new[] { ' ' }, 3);
        if (statusParts.Length < 2)
            throw new ArgumentException("Invalid status line");

        string protocol = statusParts[0];
        if (!int.TryParse(statusParts[1], out int statusCode))
            throw new ArgumentException("Invalid status code");
        string statusMessage = statusParts.Length > 2 ? statusParts[2] : "";

        // Parse headers
        Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 1; i < headerLines.Length; i++)
        {
            string line = headerLines[i];
            int colonIndex = line.IndexOf(':');
            if (colonIndex < 0) continue;

            string key = line.Substring(0, colonIndex).Trim();
            string value = line.Substring(colonIndex + 1).Trim();
            headers[key] = value;
        }

        ParsedHTTPResponse newResponse = new ParsedHTTPResponse(protocol, statusCode, statusMessage, headers, bodyBytes);

        if (newResponse.m_Headers["Content-Type"] != "image/png")
            newResponse.m_Body = newResponse.GetBodyString();

        return newResponse;
    }

    private static bool ValidateServerCertificate(
        object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors sslPolicyErrors)
    {
        const string expectedThumbprint = "9930AFAB45763E10920F23C26379A241193BC41B";

        if (sslPolicyErrors == SslPolicyErrors.None)
            return true;

        try
        {
            using (X509Certificate2 serverCert = new X509Certificate2(certificate))
            {
                //Debug.Log($"Server Certificate Details:\n"
                //+ $"Subject: {serverCert.Subject}\n"
                //+ $"Thumbprint: {serverCert.Thumbprint}\n"
                //+ $"Expires: {serverCert.GetExpirationDateString()}");

                // Check thumbprint match
                bool thumbprintValid = serverCert.Thumbprint?.Equals(
                    expectedThumbprint,
                    StringComparison.OrdinalIgnoreCase
                ) ?? false;

                // Check subject/domain
                bool subjectValid = serverCert.Subject.Contains("CN=localhost");

                // Check expiration
                bool dateValid = DateTime.Now >= serverCert.NotBefore &&
                               DateTime.Now <= serverCert.NotAfter;

                bool isValid = thumbprintValid && subjectValid && dateValid;

                if (!isValid)
                {
                    Debug.LogError($"Certificate validation failed. "
                        + $"Thumbprint match: {thumbprintValid}, "
                        + $"Subject valid: {subjectValid}, "
                        + $"Date valid: {dateValid}");
                }

                return isValid;
            }
        }
        catch
        {
            return false;
        }
    }
}

public class ParsedHTTPResponse
{
    public string m_Protocol;
    public int m_StatusCode;
    public string m_StatusMessage;

    public Dictionary<string, string> m_Headers;

    public byte[] m_BodyBytes;
    public string m_Body;

    public ParsedHTTPResponse(string protocol, int statusCode, string statusMessage, Dictionary<string, string> headers, byte[] bodyBytes)
    {
        m_Protocol = protocol;
        m_StatusCode = statusCode;
        m_StatusMessage = statusMessage;
        m_Headers = headers;
        m_BodyBytes = bodyBytes;
    }

    public string GetBodyString()
    {
        return Encoding.ASCII.GetString(m_BodyBytes);
    }
}