using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Packets;
using UnityEngine;
using UnityEngine.UI;
using Steamworks;
using TMPro;

public class UIController : MonoBehaviour
{
    public Button hostGame;
    public Button joinGame;
    public TMP_Text connected;
    public TMP_Text log;

    public GameObject localPlayerPrefab;
    public GameObject networkPlayerPrefab;
    
    private LocalPlayer _localPlayer;
    private Dictionary<CSteamID, NetworkPlayer> _networkPlayers = new();

    private float _time = 0;

    // Start is called before the first frame update
    void Start()
    {
        hostGame.onClick.AddListener(HostGameClick);
        joinGame.onClick.AddListener(JoinGameClick);
        
        Multiplayer.OnLogMessage += MultiplayerOnLogMessage;
        Multiplayer.OnNewPlayer += MultiplayerOnNewPlayer;
        Multiplayer.OnDisconnectPlayer += MultiplayerOnDisconnectPlayer;
        Multiplayer.OnNetworkMessage += MultiplayerOnNetworkMessage;
    }

    private void Update()
    {
        Multiplayer.Update();

        if (Multiplayer.IsServer)
        {
            _time += Time.deltaTime;
            if (_time > 0.1f)
            {
                // Collect world state and send it to clients
                var packet = new WorldState();
                foreach (var player in _networkPlayers)
                {
                    packet.EntityStates[player.Key.m_SteamID] = player.Value.GetState();
                }

                if (_localPlayer)
                {
                    packet.EntityStates[SteamUser.GetSteamID().m_SteamID] = _localPlayer.GetState();
                }
                
                Multiplayer.SendToAll(0x04, packet, false);

                _time -= 0.1f;
            }
        }
    }

    private void MultiplayerOnDisconnectPlayer(CSteamID id)
    {
        // Only happens on the server client
        if (_networkPlayers.ContainsKey(id))
        {
            Destroy(_networkPlayers[id].gameObject);
            _networkPlayers.Remove(id);
        }
        
        // Send this to all connected clients
        Multiplayer.SendToAll(0x02, new DespawnPlayer { SteamId = id.m_SteamID });
    }

    private void MultiplayerOnNewPlayer(CSteamID id)
    {
        // Only happens on the server client
        
        // Send all current players to the new connecting client
        foreach (var player in _networkPlayers)
        {
            var packet = new SpawnPlayer
            {
                SteamId = player.Key.m_SteamID,
                Position = player.Value.transform.position
            };
            Multiplayer.SendTo(id, 0x01, packet);
        }

        if (_localPlayer)
        {
            Multiplayer.SendTo(id, 0x01, new SpawnPlayer
            {
                SteamId = SteamUser.GetSteamID().m_SteamID,
                Position = _localPlayer.transform.position
            });
        }

        var newObject = Instantiate(networkPlayerPrefab, Vector3.zero, Quaternion.identity);
        var networkPlayer = newObject.GetComponent<NetworkPlayer>();
        networkPlayer.id = id;

        _networkPlayers[id] = networkPlayer;
        
        // Send this to all connected clients
        Multiplayer.SendToAll(0x01, new SpawnPlayer { SteamId = id.m_SteamID });
    }
    
    private void MultiplayerOnNetworkMessage(ushort header, BinaryReader reader, CSteamID sender)
    {
        Debug.Log($"Received packet with header {header}");
        
        // Happens on server and client
        switch (header)
        {
            case 0x01:
            {
                var packet = new SpawnPlayer();
                packet.Deserialize(reader);

                OnSpawnPlayer(packet);
                break;
            }
            case 0x02:
            {
                var packet = new DespawnPlayer();
                packet.Deserialize(reader);

                OnDespawnPlayer(packet);
                break;
            }
            case 0x03:
            {
                var packet = new PlayerUpdate();
                packet.Deserialize(reader);

                OnPlayerUpdate(packet, sender);
                break;
            }
            case 0x04:
            {
                var packet = new WorldState();
                packet.Deserialize(reader);

                OnWorldUpdate(packet);
                break;
            }
        }
    }

    private void OnSpawnPlayer(SpawnPlayer packet)
    {
        if (Multiplayer.IsServer) return;
        
        var you = SteamUser.GetSteamID();
        Debug.Log($"Received spawn for {packet.SteamId} (you: {you.m_SteamID})");
        
        if (packet.SteamId == SteamUser.GetSteamID().m_SteamID)
        {
            // it's our own spawn!
            var player = Instantiate(localPlayerPrefab, Vector3.zero, Quaternion.identity);
            _localPlayer = player.GetComponent<LocalPlayer>();
            return;
        }
        
        var newObject = Instantiate(networkPlayerPrefab, Vector3.zero, Quaternion.identity);
        var networkPlayer = newObject.GetComponent<NetworkPlayer>();
        networkPlayer.id = new CSteamID(packet.SteamId);

        _networkPlayers[networkPlayer.id] = networkPlayer;
    }

    private void OnDespawnPlayer(DespawnPlayer packet)
    {
        if (Multiplayer.IsServer) return;
        
        if (packet.SteamId == SteamUser.GetSteamID().m_SteamID)
        {
            // it's our own despawn!
            return;
        }

        var steamId = new CSteamID(packet.SteamId);
        if (!_networkPlayers.ContainsKey(steamId)) return;
        
        Destroy(_networkPlayers[steamId]);
        _networkPlayers.Remove(steamId);
    }

    private void OnPlayerUpdate(PlayerUpdate packet, CSteamID sender)
    {
        // Should only ever received by the server
        if (!Multiplayer.IsServer) return;

        // We don't know this player
        if (!_networkPlayers.ContainsKey(sender)) return;

        var player = _networkPlayers[sender];
        player.Sync(packet.Position, packet.Rotation);
    }

    private void OnWorldUpdate(WorldState packet)
    {
        if (Multiplayer.IsServer) return;

        foreach (var state in packet.EntityStates)
        {
            var steamId = new CSteamID(state.Key);
            if(!_networkPlayers.ContainsKey(steamId)) continue; // e.g. for ourselves

            var player = _networkPlayers[steamId];
            player.Sync(state.Value.Position, state.Value.Rotation);
        }
    }

    private void MultiplayerOnLogMessage(string message)
    {
        log.text += message + "\n";
    }

    private void JoinGameClick()
    {
        if (!SteamManager.Initialized)
        {
            Debug.Log("Steam not initialized!");
            return;
        }
        
        // List all online players
        for (var i = 0; i < SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate); i++)
        {
            var friend = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            if (SteamFriends.GetFriendGamePlayed(friend, out var info) && info.m_gameID.m_GameID == 1519860)
            {
                Debug.Log("Connecting to " + SteamFriends.GetFriendPersonaName(friend));

                if (Multiplayer.ConnectToServer(friend))
                {
                    Debug.Log("Connected!");
                    
                    hostGame.gameObject.SetActive(false);
                    joinGame.gameObject.SetActive(false);
                    connected.gameObject.SetActive(true);
                    log.gameObject.SetActive(true);
                    break;
                }
                Debug.Log("Connection failed;");
            }
        }
    }

    private void HostGameClick()
    {
        if (!SteamManager.Initialized)
        {
            Debug.Log("Steam not initialized!");
            return;
        }
        
        Multiplayer.StartServer();
        hostGame.gameObject.SetActive(false);
        joinGame.gameObject.SetActive(false);
        connected.gameObject.SetActive(true);
        log.gameObject.SetActive(true);

        var player = Instantiate(localPlayerPrefab, Vector3.zero, Quaternion.identity);
        _localPlayer = player.GetComponent<LocalPlayer>();
    }
}
