using System;
using NitroNetwork.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro; // Importação necessária para TextMeshPro

public class NitroConnect : MonoBehaviour
{
    [SerializeField]
    private Button btnStartServer, btnStartClient;
    [SerializeField]
    private TMP_InputField inputAddress, inputPort; // Alterado para TMP_InputField
    private TextMeshProUGUI txtServer, txtClient; // Alterado para TextMeshProUGUI

    void Start()
    {
        NitroManager.OnDisconnectInstance += OnDisconnectConn;
        btnStartServer.onClick.AddListener(ConnectServer);
        btnStartClient.onClick.AddListener(ConnectClient);
        txtServer = btnStartServer.GetComponentInChildren<TextMeshProUGUI>();
        txtClient = btnStartClient.GetComponentInChildren<TextMeshProUGUI>();
        inputAddress.text = NitroManager.Instance.address;
        inputPort.text = NitroManager.Instance.port.ToString();
    }

    private void OnDisconnectConn()
    {
        Destroy(NitroManager.Instance.gameObject);
        SceneManager.LoadScene(0);
        txtServer.text = "Start Server";
        txtClient.text = "Start Client";
    }

    void ConnectServer()
    {
        if (txtServer.text == "Start Server")
        {
            NitroManager.ConnectServer(int.Parse(inputPort.text));
            txtServer.text = "Stop Server";
            return;
        }
        else
        {
            NitroManager.Disconnect();
        }
    }

    void ConnectClient()
    {
        if (txtClient.text == "Start Client")
        {
            NitroManager.ConnectClient(inputAddress.text, int.Parse(inputPort.text));
            txtClient.text = "Stop Client";
            return;
        }
        else
        {
            NitroManager.Disconnect();
        }
    }
}
