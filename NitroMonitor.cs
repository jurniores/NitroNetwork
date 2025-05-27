using System;
using NitroNetwork.Core;
using TMPro;
using UnityEngine;

public class NitroMonitor : MonoBehaviour
{
    [SerializeField]
    private TextMeshProUGUI[] textBand;
    [SerializeField]
    private TextMeshProUGUI _textPingClient;
    void Awake()
    {
        NitroManager.OnBandWidth += UpdateBandWidth;
        NitroManager.OnPingClient += PingClient;
    }

    private void PingClient(int obj)
    {
        _textPingClient.text = $"{obj} ms";
    }

    private void UpdateBandWidth(NitroBandWidth width)
    {
        textBand[0].text = $"{width.BClientSent}b/s S";
        textBand[1].text = $"{width.BClientReceived}b/s R";
        textBand[2].text = $"{width.PacketSentClient} S";
        textBand[3].text = $"{width.PacketReceivedClient} R";

        textBand[4].text = $"{width.BServerSent}b/s S";
        textBand[5].text = $"{width.BServerReceived}b/s R";
        textBand[6].text = $"{width.PacketSentServer} S";
        textBand[7].text = $"{width.PacketReceivedServer} R";
    }
}
