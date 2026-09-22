using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

public class LoginScene : MonoBehaviour
{
    public TMP_InputField teamField;
    public TMP_InputField nameField;
    public TMP_InputField nickNameField;
    public Button checkButton;

    bool checkButtonClicked;

    void Start()
    {
        checkButton.onClick.AddListener(() =>
        {
            if (!string.IsNullOrEmpty(teamField.text) &&
                !string.IsNullOrEmpty(nameField.text) &&
                !string.IsNullOrEmpty(nickNameField.text))
            {
                if (!checkButtonClicked)
                {
                    StartCoroutine(CheckNickname());
                }
            }
        });
    }

    IEnumerator CheckNickname()
    {
        checkButtonClicked = true;
        string url = $"http://localhost:3000/players/check?nickname={nickNameField.text}";

        var www = UnityWebRequest.Get(url);
        www.timeout = 5;
        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            print(www.downloadHandler.text);
        }
        www.Dispose();
    }
}
