using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using System;
using UnityEngine.SceneManagement;

public class LoginManager : MonoBehaviour
{
    [SerializeField]
    private TMP_InputField m_UsernameIF;
    [SerializeField]
    private TMP_InputField m_PasswordIF;
    [SerializeField]
    private TextMeshProUGUI m_OutputText;

    private void Start()
    {
        m_OutputText.text = string.Empty;
    }

    public void SignUp()
    {
        string username = m_UsernameIF.text;
        string password = m_PasswordIF.text;

        if (username == string.Empty || password == string.Empty)
            return;

        string headers = $"username:{username}\r\npassword:{password}";

        ParsedHTTPResponse response = MyHTTPClient.SendRequestToServer("POST", "/signUp", headers);

        if (response.m_StatusCode == 201)
        {
            MyHTTPClient.SetUserID(int.TryParse(response.m_Headers["userID"], out int i) ? i : -1);
            MyHTTPClient.SetAuthenticationKey(response.m_Headers["authenticationKey"]);

            SceneManager.LoadScene("ShopScene");
        }
        else if (response.m_StatusCode == 412)
        {
            if (response.m_Headers["Content-Type"] == "text/plain")
                m_OutputText.text = response.m_Body;
        }
        else
        {
            Debug.LogWarning(response.m_StatusMessage);
        }
    }

    public void Login()
    {
        string username = m_UsernameIF.text;
        string password = m_PasswordIF.text;

        if (username == string.Empty || password == string.Empty)
            return;

        string headers = $"username:{username}\r\npassword:{password}";

        ParsedHTTPResponse response = MyHTTPClient.SendRequestToServer("PUT", "/login", headers);

        if (response.m_StatusCode == 214)
        {
            MyHTTPClient.SetUserID(int.TryParse(response.m_Headers["userID"], out int i) ? i : -1);
            MyHTTPClient.SetAuthenticationKey(response.m_Headers["authenticationKey"]);

            SceneManager.LoadScene("ShopScene");
        }
        else if (response.m_StatusCode == 412)
        {
            if (response.m_Headers["Content-Type"] == "text/plain")
                m_OutputText.text = response.m_Body;
        }
        else
        {
            Debug.LogWarning(response.m_StatusMessage);
        }
    }
}
