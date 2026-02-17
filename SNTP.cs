using NitroNetwork.Core;
using UnityEngine;
using System;
using System.Collections;

public partial class SNTP : NitroBehaviour
{
    [Header("SNTP Configuration")]
    [SerializeField] private float tickRate = 60f; 
    [SerializeField] private int maxSyncAttempts = 5;

    // Variáveis de sincronização
    private double serverTime = 0;
    private double clientTimeOffset = 0;
    private bool isTimeSynced = false;
    private int syncAttempts = 0;

    // Sistema de ticks calculado
    private ulong baseTickCount = 0;
    private double baseTimestamp = 0;
    private double tickInterval;
    private ulong lastCalculatedTick = 0;
    public bool OnGui;
    // Eventos
    public static event Action<ulong> OnTick;
    public static event Action<double> OnTimeSynced;

    void Start()
    {
        tickInterval = 1.0 / tickRate;
        
        // Registra eventos
        NitroManager.OnServerConnected += OnServerStart;
        NitroManager.OnClientConnected += OnClientStart;
        
        // Se já estiver conectado, determina comportamento baseado na prioridade
        if (IsServer && IsClient)
        {
            OnServerStart(); // Força modo servidor para host
        }
        else if (IsClient && !IsServer)
        {
            StartCoroutine(DelayedSyncStart());
        }
        else if (IsServer && !IsClient)
        {
            OnServerStart();
        }
    }

    private void OnServerStart()
    {
        // Se já está sincronizado como servidor, não faz nada
        if (isTimeSynced)
        {
            return;
        }
        
        serverTime = GetUnixTimestamp();
        isTimeSynced = true;
        baseTimestamp = serverTime;
        baseTickCount = 0;
    }

    private void OnClientStart()
    {
        
        // Se é HOST (cliente + servidor), prioriza servidor e cancela sincronização de cliente
        if (IsServer && IsClient)
        {
            return; // Não faz sincronização de cliente se é host
        }
        
        // Se é cliente puro, inicia sincronização
        if (IsClient && !IsServer)
        {
            isTimeSynced = false;
            syncAttempts = 0;
            
            // Aguarda um frame para garantir que a conexão está estável
            StartCoroutine(DelayedSyncStart());
        }
    }

    private IEnumerator DelayedSyncStart()
    {
        yield return new WaitForSeconds(0.5f); // Aguarda meio segundo
        StartCoroutine(SyncWithServer());
    }

    void Update()
    {
        if (isTimeSynced)
        {
            ulong calculatedTick = GetCalculatedTick();
            
            if (calculatedTick > lastCalculatedTick)
            {
                lastCalculatedTick = calculatedTick;
                OnTick?.Invoke(calculatedTick);
                
                if (calculatedTick % 60 == 0 && calculatedTick > 0)
                {
                }
            }
        }
    }

    private ulong GetCalculatedTick()
    {
        if (!isTimeSynced) return 0;
        
        double currentTime = GetSynchronizedTime();
        double elapsed = currentTime - baseTimestamp;
        
        // Evita valores negativos
        if (elapsed < 0) elapsed = 0;
        
        ulong calculatedTicks = baseTickCount + (ulong)(elapsed * tickRate);
        
        if (calculatedTicks % 60 == 0 && calculatedTicks != lastCalculatedTick)
        {
        }
        
        return calculatedTicks;
    }

    private IEnumerator SyncWithServer()
    {
        
        // Primeiro loop: tenta sincronizar até conseguir (máximo 5 tentativas rápidas)
        while (!isTimeSynced && syncAttempts < maxSyncAttempts)
        {
            yield return new WaitForSeconds(1f);
            
            // Verifica se virou host durante o processo
            if (IsServer && IsClient)
            {
                OnServerStart();
                yield break;
            }
            
            
            if (IsClient && !IsServer) // Só sincroniza se for cliente puro
            {
                syncAttempts++;
                RequestTimeSync();
                
                // Aguarda resposta por 3 segundos
                float timeout = 3f;
                bool wasTimeSynced = isTimeSynced;
                
                while (timeout > 0 && !isTimeSynced)
                {
                    timeout -= 0.1f;
                    yield return new WaitForSeconds(0.1f);
                    
                    // Verifica novamente se virou host
                    if (IsServer && IsClient)
                    {
                        OnServerStart();
                        yield break;
                    }
                }
                
                if (isTimeSynced && !wasTimeSynced)
                {
                    StartCoroutine(PeriodicResync());
                    yield break; // Sai da corrotina
                }
                else if (timeout <= 0)
                {
                }
            }
            else
            {
                yield return new WaitForSeconds(2f);
            }
        }
        
        // Se não conseguiu sincronizar nas primeiras tentativas, continua tentando devagar
        if (!isTimeSynced && IsClient && !IsServer)
        {
            StartCoroutine(SlowRetrySync());
        }
    }

    private IEnumerator SlowRetrySync()
    {
        while (!isTimeSynced)
        {
            yield return new WaitForSeconds(5f); // Tenta a cada 5 segundos
            
            // Verifica se virou host
            if (IsServer && IsClient)
            {
                OnServerStart();
                yield break;
            }
            
            if (IsClient && !IsServer) // Só tenta se for cliente puro
            {
                RequestTimeSync();
                
                // Aguarda resposta por 3 segundos
                float timeout = 3f;
                bool wasTimeSynced = isTimeSynced;
                
                while (timeout > 0 && !isTimeSynced)
                {
                    timeout -= 0.1f;
                    yield return new WaitForSeconds(0.1f);
                    
                    // Verifica se virou host durante timeout
                    if (IsServer && IsClient)
                    {
                        OnServerStart();
                        yield break;
                    }
                }
                
                if (isTimeSynced && !wasTimeSynced)
                {
                    StartCoroutine(PeriodicResync());
                    yield break;
                }
            }
            else
            {
                yield break;
            }
        }
    }

    private IEnumerator PeriodicResync()
    {
        while (isTimeSynced && IsClient && !IsServer) // Só continua se for cliente puro
        {
            yield return new WaitForSeconds(30f); // Ressincroniza a cada 30 segundos
            
            // Verifica se ainda é cliente puro
            if (IsServer && IsClient)
            {
                yield break;
            }
            
            RequestTimeSync();
        }
        
    }



    private void RequestTimeSync()
    {
        
        // Só envia RPC se for cliente puro (não host)
        if (IsClient && !IsServer)
        {
            double clientRequestTime = GetUnixTimestamp();
            
            try
            {
                CallRequestTimeSyncSNTP(clientRequestTime);
            }
            catch (System.Exception e)
            {
            }
        }
        else if (IsServer && IsClient)
        {
        }
        else
        {
        }
    }

    private double GetUnixTimestamp()
    {
        return (DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
    }

    private double GetSynchronizedTime()
    {
        if (IsServer)
        {
            // Servidor sempre usa tempo atual
            double currentTime = GetUnixTimestamp();
            return currentTime;
        }
        else
        {
            // Cliente usa tempo com offset
            return GetUnixTimestamp() + clientTimeOffset;
        }
    }

    // === RPCs ===
    [NitroRPC(Server)]
    private void RequestTimeSyncSNTP(double clientRequestTime)
    {
        double serverCurrentTime = GetUnixTimestamp();
        ulong serverCurrentTick = GetCalculatedTick(); // Pega o tick atual do servidor
        
        
        // Envia tempo E tick do servidor para o cliente
        CallRespondTimeSyncSNTPClient(serverCurrentTime, clientRequestTime, serverCurrentTick);
    }

    [NitroRPC(Client)]
    private void RespondTimeSyncSNTPClient(double serverTime, double originalClientTime, ulong serverTick)
    {
        
        double clientReceiveTime = GetUnixTimestamp();
        double roundTripTime = clientReceiveTime - originalClientTime;
        double networkDelay = roundTripTime / 2.0;

        clientTimeOffset = serverTime + networkDelay - clientReceiveTime;

        if (!isTimeSynced)
        {
            isTimeSynced = true;
            
            // PRIMEIRA SINCRONIZAÇÃO: Sincroniza com o tick atual do servidor
            baseTimestamp = GetSynchronizedTime();
            baseTickCount = serverTick; // Começa com o tick atual do servidor!
            
            
            OnTimeSynced?.Invoke(clientTimeOffset);
        }
        else
        {
            // RESSINCRONIZAÇÃO: Ajusta a base para manter continuidade dos ticks
            double newSyncTime = GetSynchronizedTime();
            ulong currentClientTick = GetCalculatedTick(); // Tick atual do cliente
            
            // Ajusta base para que o próximo cálculo resulte no tick do servidor
            baseTimestamp = newSyncTime;
            baseTickCount = serverTick;
            
        }

        syncAttempts = 0; // Reset tentativas após sucesso
    }

    // === MÉTODOS PÚBLICOS ===
    public static double GetCurrentTime()
    {
        var instance = FindFirstObjectByType<SNTP>();
        return instance?.GetSynchronizedTime() ?? 0;
    }

    public static ulong GetCurrentTick()
    {
        var instance = FindFirstObjectByType<SNTP>();
        return instance?.GetCalculatedTick() ?? 0;
    }

    public static bool IsTimeSync()
    {
        var instance = FindFirstObjectByType<SNTP>();
        return instance?.isTimeSynced ?? false;
    }

    public static double GetTickRate()
    {
        var instance = FindFirstObjectByType<SNTP>();
        return instance?.tickRate ?? 60f;
    }

    public static double GetSyncOffset()
    {
        var instance = FindFirstObjectByType<SNTP>();
        return instance?.clientTimeOffset ?? 0;
    }

    public static ulong TimeToTick(double timeInSeconds)
    {
        var instance = FindFirstObjectByType<SNTP>();
        if (instance == null) return 0;
        return instance.GetCalculatedTick() + (ulong)(timeInSeconds * instance.tickRate);
    }

    public static double TickToTime(ulong targetTick)
    {
        var instance = FindFirstObjectByType<SNTP>();
        if (instance == null) return 0; 
        long tickDifference = (long)(targetTick - instance.GetCalculatedTick());
        return tickDifference / instance.tickRate;
    }

    public static void ForceSyncronization()
    {
        var instance = FindFirstObjectByType<SNTP>();
        if (instance != null && instance.IsClient)
        {
            instance.isTimeSynced = false;
            instance.syncAttempts = 0;
            instance.RequestTimeSync();
        }
    }

    public static string GetSyncStats()
    {
        var instance = FindFirstObjectByType<SNTP>();
        if (instance == null) return "SNTP não encontrado";

        return $"Tick: {instance.GetCalculatedTick()}, " +
               $"Sincronizado: {instance.isTimeSynced}, " +
               $"Offset: {instance.clientTimeOffset:F3}s, " +
               $"Tentativas: {instance.syncAttempts}";
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }

    void OnGUI()
    {
        if (Application.isPlaying && OnGui)
        {
            GUILayout.BeginArea(new Rect(0, Screen.height - 240, 500, 230));
            
            // Header com cor e tipo específico
            if (IsClient && IsServer)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("=== SNTP HOST (Client + Server) ===");
                GUI.color = Color.white;
            }
            else if (IsServer)
            {
                GUI.color = Color.green;
                GUILayout.Label("=== SNTP SERVER ===");
                GUI.color = Color.white;
            }
            else if (IsClient)
            {
                GUI.color = Color.cyan;
                GUILayout.Label("=== SNTP CLIENT ===");
                GUI.color = Color.white;
            }
            else
            {
                GUI.color = Color.gray;
                GUILayout.Label("=== SNTP INATIVO ===");
                GUI.color = Color.white;
            }
            
            // Status de rede
            GUILayout.Label($"IsClient: {IsClient} | IsServer: {IsServer}");
            
            ulong currentTick = GetCalculatedTick();
            GUILayout.Label($"Tick Atual: {currentTick}");
            GUILayout.Label($"Tempo Sync: {GetSynchronizedTime():F3}s");
            GUILayout.Label($"Base Timestamp: {baseTimestamp:F3}s");
            GUILayout.Label($"Base Tick: {baseTickCount}");
            
            if (isTimeSynced)
            {
                double elapsed = GetSynchronizedTime() - baseTimestamp;
                GUILayout.Label($"Elapsed: {elapsed:F3}s | Tick Rate: {tickRate}");
            }
            
            // Status de sincronização com cor
            GUI.color = isTimeSynced ? Color.green : Color.red;
            GUILayout.Label($"Status: {(isTimeSynced ? "SINCRONIZADO" : "NÃO SINCRONIZADO")}");
            GUI.color = Color.white;
            
            if (IsClient && !IsServer) // Só mostra controles para clientes puros
            {
                GUILayout.Label($"Offset: {clientTimeOffset:F3}s");
                GUILayout.Label($"Tentativas: {syncAttempts}/{maxSyncAttempts}");
                
                if (GUILayout.Button("Forçar Sincronização"))
                {
                    ForceSyncronization();
                }
                
                {
                    RequestTimeSync();
                }
            }
            else if (IsServer && !IsClient) // Servidor puro
            {
                GUILayout.Label($"Tempo Unix: {GetUnixTimestamp():F3}s");
            }
            else if (IsClient && IsServer) // Host
            {
                GUI.color = Color.yellow;
                GUILayout.Label("🏠 HOST - Usa tempo local do servidor");
                GUI.color = Color.white;
                GUILayout.Label($"Tempo Unix: {GetUnixTimestamp():F3}s");
            }
            else
            {
                GUI.color = Color.yellow;
                GUILayout.Label("⚠️ NÃO É CLIENTE NEM SERVIDOR");
                GUI.color = Color.white;
            }
            
            GUILayout.EndArea();
        }
    }
}
