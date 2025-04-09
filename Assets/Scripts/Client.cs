using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Net.Sockets;
using TMPro;

class MyHTTPClient : MonoBehaviour
{
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

    private void Start()
    {
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
        string server = "httpgroupwork.free.beeceptor.com";
        int port = 80;

        string requestMethod = "POST";
        if (m_Method != "")
            requestMethod = m_Method;

        string url = "/newValue";
        if (m_URL != "")
            url = m_URL;

        string httpVersion = "HTTP/1.1";

        string headers = "";
        if (m_Headers != "")
            headers = m_Headers;

        string body = m_Body;
        
        string request = $"{requestMethod} {url} {httpVersion}\r\nHost: {server}\r\nConnection: close\r\nContent-Type: application/json\r\nContent-Length: {body.Length}\r\n{headers}\r\n{body}";
            
        // ------------------------------------------------------------------------------------------------------

        try
        {
            using (TcpClient client = new TcpClient(server, port))
            using (NetworkStream stream = client.GetStream())
            {
                byte[] requestBytes = Encoding.ASCII.GetBytes(request);
                stream.Write(requestBytes, 0, requestBytes.Length);

                byte[] buffer = new byte[1024];
                int bytesRead;
                using (MemoryStream responseStream = new MemoryStream())
                {
                    while ((bytesRead = stream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        responseStream.Write(buffer, 0, bytesRead);
                    }
                    string response = Encoding.ASCII.GetString(responseStream.ToArray());
                    Debug.Log(response);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
        }
    }
}